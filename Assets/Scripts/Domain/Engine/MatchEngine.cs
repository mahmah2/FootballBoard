using System;
using System.Collections.Generic;
using System.Linq;
using FootballBoard.Domain.Config;

namespace FootballBoard.Domain
{
    /// <summary>
    /// The single entry point for changing a match. Human input and the AI both submit commands
    /// here. A rejected command never changes the state. Outcomes are decided here, before any
    /// animation, and reported as an ordered list of <see cref="GameEvent"/>s.
    ///
    /// Turn flow: the turn team submits its plan (moves + carrier option) first and it is locked
    /// hidden; then the other team submits its moves. Both plans then resolve together:
    /// simultaneous movement, loose-ball claims, tackles (arrangement step), then the declared
    /// pass or shot (action step). Each step uses clock.secondsPerStep of match time.
    /// With turns.possessionTeamPlaysFirst the next turn goes to the team holding the ball
    /// (the same team while the ball is loose, the conceding team after a goal).
    /// </summary>
    public sealed class MatchEngine
    {
        private readonly IRandomSource _random;

        public MatchState State { get; }

        private RulesConfig Rules => State.Rules;
        private Pitch Pitch => State.Pitch;

        private MatchEngine(MatchState state, IRandomSource random)
        {
            State = state;
            _random = random;
        }

        /// <summary>
        /// Creates a match in the <see cref="MatchPhase.Setup"/> phase with both teams in their kick-off formation.
        /// The rules are copied, so later edits to <paramref name="rules"/> do not affect this match.
        /// </summary>
        public static MatchEngine Create(RulesConfig rules = null, MatchSettings settings = null, IRandomSource random = null)
        {
            rules = rules == null ? RulesConfig.CreateDefault() : RulesConfigLoader.Clone(rules);
            var errors = rules.Validate();
            if (errors.Count > 0) throw new RulesConfigException(errors);

            settings = settings ?? new MatchSettings();
            var settingErrors = settings.Validate(rules);
            if (settingErrors.Count > 0) throw new ArgumentException(string.Join(" ", settingErrors), nameof(settings));

            random = random ?? new SeededRandom(settings.Seed ?? (ulong)DateTime.UtcNow.Ticks);

            var state = new MatchState(
                rules,
                settings.Mode,
                settings.HumanTeam,
                settings.PlayersPerTeam ?? rules.Teams.PlayersPerTeam,
                rules.Goalkeeper.MaxMoveCells,
                (settings.DurationMinutes ?? rules.Clock.DefaultMinutes) * 60);

            var engine = new MatchEngine(state, random);
            engine.CreatePlayers();
            return engine;
        }

        // ------------------------------------------------------------------ commands

        /// <summary>Setup only: place an outfield player anywhere in its own half.</summary>
        public CommandResult PlacePlayer(int playerId, Cell cell)
        {
            if (State.IsPaused) return CommandResult.Reject("The match is paused.");
            if (State.Phase != MatchPhase.Setup) return CommandResult.Reject("Players can only be placed before kick-off.");
            var player = State.GetPlayer(playerId);
            if (player == null) return CommandResult.Reject("Unknown player.");
            if (player.IsGoalkeeper) return CommandResult.Reject("Goalkeepers start in the middle of their goal.");
            if (!Pitch.InOwnHalf(player.Team, cell)) return CommandResult.Reject("Players must start in their own half.");
            if (player.Position != cell && State.OccupancyAt(cell) >= Pitch.MaxPlayersPerSquare)
                return CommandResult.Reject("That square is full.");

            var events = new List<GameEvent>();
            Relocate(player, cell, events);
            return CommandResult.Ok(events);
        }

        /// <summary>Ends setup. Without <paramref name="kickoffTeam"/> a coin toss decides who kicks off (and plays first).</summary>
        public CommandResult StartMatch(Team? kickoffTeam = null)
        {
            if (State.IsPaused) return CommandResult.Reject("The match is paused.");
            if (State.Phase != MatchPhase.Setup) return CommandResult.Reject("The match has already started.");

            var team = kickoffTeam ?? (_random.NextInt(2) == 0 ? Team.Red : Team.Blue);
            var events = new List<GameEvent> { new CoinTossed(team, kickoffTeam == null) };
            PlaceKicker(team, events);
            BeginTurn(team, events);
            return CommandResult.Ok(events);
        }

        /// <summary>
        /// Submit the plan of <see cref="MatchState.PlanningTeam"/>. The turn team's plan is stored;
        /// the second plan triggers resolution of the whole team turn.
        /// </summary>
        public CommandResult SubmitPlan(Team team, TeamPlan plan)
        {
            if (State.IsPaused) return CommandResult.Reject("The match is paused.");
            if (State.Phase != MatchPhase.Planning) return CommandResult.Reject("No plan is expected now.");
            if (team != State.PlanningTeam) return CommandResult.Reject($"It is {State.PlanningTeam}'s turn to plan.");

            var error = ValidatePlan(team, plan);
            if (error != null) return CommandResult.Reject(error);

            var copy = plan.Copy();
            if (team == State.TurnTeam)
            {
                var carrier = State.BallCarrier;
                State.PendingTurnPlan = copy;
                State.PendingCarrierId = carrier != null && carrier.Team == team ? carrier.Id : (int?)null;
                RecordShotBlocker(copy);
                State.PlanningTeam = team.Opponent();
                return CommandResult.Ok(new GameEvent[] { new PlanLocked(team, team.Opponent()) });
            }

            var events = new List<GameEvent>();
            ResolveTurn(State.PendingTurnPlan, copy, events);
            return CommandResult.Ok(events);
        }

        public CommandResult SubmitTieChoice(Team team, TieChoice choice)
        {
            if (State.IsPaused) return CommandResult.Reject("The match is paused.");
            if (State.Phase != MatchPhase.AwaitingTieDecision) return CommandResult.Reject("No tie decision is needed.");
            if (State.TieChoices.ContainsKey(team)) return CommandResult.Reject($"{team} has already chosen.");

            State.TieChoices[team] = choice;
            if (State.TieChoices.Count < 2) return CommandResult.Ok();

            var golden = Rules.Ties.GoldenGoalRequiresBothTeams
                ? State.TieChoices.Values.All(c => c == TieChoice.GoldenGoal)
                : State.TieChoices.Values.Any(c => c == TieChoice.GoldenGoal);

            var events = new List<GameEvent>();
            if (golden)
            {
                State.IsGoldenGoal = true;
                events.Add(new GoldenGoalStarted());
                BeginTurn(State.TurnTeam, events);
            }
            else
            {
                EndMatch(events);
            }
            return CommandResult.Ok(events);
        }

        /// <summary>Pausing blocks every command except <see cref="Resume"/>. The clock is step-based, so no time passes.</summary>
        public CommandResult Pause()
        {
            if (State.Phase == MatchPhase.MatchOver) return CommandResult.Reject("The match is over.");
            if (State.IsPaused) return CommandResult.Reject("The match is already paused.");
            State.IsPaused = true;
            return CommandResult.Ok();
        }

        public CommandResult Resume()
        {
            if (!State.IsPaused) return CommandResult.Reject("The match is not paused.");
            State.IsPaused = false;
            return CommandResult.Ok();
        }

        // ------------------------------------------------------------------ queries for UI and AI

        /// <summary>Returns null if the plan is legal for <paramref name="team"/> right now, otherwise the reason it is not.</summary>
        public string ValidatePlan(Team team, TeamPlan plan)
        {
            if (plan == null) return "A plan is required.";
            var moves = plan.Moves ?? new List<MoveOrder>();
            var isTurnTeam = team == State.TurnTeam;
            var carrier = State.BallCarrier;
            var teamHasBall = carrier != null && carrier.Team == team;
            var action = plan.Action;

            if (action != null)
            {
                if (!isTurnTeam) return "Only the team taking its turn can choose a ball-carrier option.";
                if (!teamHasBall) return "Your team does not have the ball.";
                var actionError = ValidateAction(carrier, action);
                if (actionError != null) return actionError;
            }

            CarrierOption? option = isTurnTeam && teamHasBall ? action?.Option ?? CarrierOption.Move : (CarrierOption?)null;
            var isPass = option == CarrierOption.StraightPass || option == CarrierOption.LongPass;
            var seen = new HashSet<int>();

            foreach (var move in moves)
            {
                if (move == null) return "A move entry is empty.";
                var player = State.GetPlayer(move.PlayerId);
                if (player == null || player.Team != team) return $"Player {move.PlayerId} is not in your team.";
                if (!player.Active) return $"{Name(player)} is not active.";
                if (!seen.Add(player.Id)) return $"{Name(player)} has more than one move.";
                if (!Pitch.InBounds(move.Target)) return $"{Name(player)} cannot leave the pitch.";

                var distance = player.Position.DistanceTo(move.Target);
                if (distance == 0) return $"{Name(player)} is already on that square.";
                if (player.IsGoalkeeper)
                {
                    var range = GoalkeeperRange(player);
                    if (distance > range)
                        return range < State.GoalkeeperMoveCells
                            ? $"The ball is near the box: goalkeepers move at most {range} cell(s) this turn."
                            : $"Goalkeepers move at most {range} cell(s) per turn.";
                    if (!Pitch.InPenaltyBox(team, move.Target)) return "Goalkeepers must stay inside their penalty box.";
                }
                else if (distance != 1)
                {
                    return "Players move one cell per turn.";
                }

                if (carrier != null && player.Id == carrier.Id)
                {
                    var mayMove = isTurnTeam
                        ? option == CarrierOption.Move
                        : Rules.Carrier.CanMoveOnOpponentTurn;
                    if (!mayMove)
                        return isTurnTeam
                            ? "The ball carrier can only move when the Move option is chosen."
                            : "The ball carrier cannot move on the other team's turn.";
                }
                if (isPass && player.Id == action.ReceiverId)
                    return "The pass receiver cannot move this turn.";
                if (move.Tackle)
                {
                    if (!Rules.Tackle.Enabled) return "Tackles are disabled.";
                    if (carrier != null && player.Id == carrier.Id) return "The ball carrier cannot tackle.";
                }
            }
            return null;
        }

        /// <summary>Cells a player may be ordered to (occupancy is settled when moves resolve).</summary>
        public IReadOnlyList<Cell> GetMoveTargets(int playerId)
        {
            var player = State.GetPlayer(playerId);
            var result = new List<Cell>();
            if (player == null || !player.Active) return result;
            var range = player.IsGoalkeeper ? GoalkeeperRange(player) : 1;
            for (var dx = -range; dx <= range; dx++)
            for (var dy = -range; dy <= range; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var cell = player.Position.Offset(dx, dy);
                if (!Pitch.InBounds(cell)) continue;
                if (player.IsGoalkeeper && !Pitch.InPenaltyBox(player.Team, cell)) continue;
                result.Add(cell);
            }
            return result;
        }

        /// <summary>
        /// How far a goalkeeper may move this turn: the full range normally, but less while the ball is
        /// inside its penalty box or within <c>NearBallRings</c> rings of it (so it cannot dive across the goal).
        /// </summary>
        public int GoalkeeperRange(Player keeper)
        {
            var g = Rules.Goalkeeper;
            var ring = Pitch.RingsOutsidePenaltyBox(keeper.Team, State.BallPosition);
            return ring <= g.NearBallRings ? Math.Min(State.GoalkeeperMoveCells, g.MaxMoveCellsWhenBallNear) : State.GoalkeeperMoveCells;
        }

        /// <summary>
        /// Chance a shot from <paramref name="from"/> is on target: full inside the opponent's penalty box,
        /// then a growing drop per ring of squares around the box (5%, 6%, 7%...), never below the minimum.
        /// </summary>
        public double ShotAccuracy(Team shooter, Cell from)
        {
            var r = Rules.Shooting;
            var ring = Pitch.RingsOutsidePenaltyBox(shooter.Opponent(), from);
            var drop = ring * r.AccuracyDropPerRing + r.AccuracyDropIncreasePerRing * ring * (ring - 1) / 2.0;
            return Math.Max(r.MinimumAccuracy, r.AccuracyInsideBox - drop);
        }

        /// <summary>Goal chance for an on-target shot, given the goalkeeper's distance from the target cell.</summary>
        public double GoalChance(int keeperDistance)
        {
            var reach = Rules.Goalkeeper.DiveReachCells;
            if (reach >= 0 && keeperDistance > reach) return 1.0; // out of the keeper's reach
            var table = Rules.Goalkeeper.GoalChanceByDistance;
            return table[Math.Min(Math.Max(keeperDistance, 0), table.Count - 1)];
        }

        // ------------------------------------------------------------------ validation helpers

        private string ValidateAction(Player carrier, CarrierAction action)
        {
            switch (action.Option)
            {
                case CarrierOption.Move:
                    return null;
                case CarrierOption.Shot:
                    if (action.TargetCell.HasValue && !IsGoalCell(carrier.Team.Opponent(), action.TargetCell.Value))
                        return "Aim the shot at one of the squares inside the opponent's goal.";
                    return null;
                case CarrierOption.LongPass when action.ReceiverId == null:
                    if (action.TargetCell == null) return "Choose a teammate or a square for the long pass.";
                    var target = action.TargetCell.Value;
                    if (!Pitch.InBounds(target)) return "The long pass must land on the pitch.";
                    if (target == carrier.Position) return "Choose a square away from the carrier.";
                    if (!Rules.Passing.AllowForwardPassFromSharedSquare &&
                        State.OccupancyAt(carrier.Position) > 1 && IsForward(carrier, target))
                        return "A carrier sharing a square can only pass sideways or backwards.";
                    return null;
                case CarrierOption.StraightPass:
                case CarrierOption.LongPass:
                    if (action.ReceiverId == null) return "Choose a teammate to receive the pass.";
                    var receiver = State.GetPlayer(action.ReceiverId.Value);
                    if (receiver == null || receiver.Team != carrier.Team || !receiver.Active)
                        return "The receiver must be an active teammate.";
                    if (receiver.Id == carrier.Id) return "The carrier cannot pass to themselves.";
                    if (!Rules.Passing.AllowForwardPassFromSharedSquare &&
                        State.OccupancyAt(carrier.Position) > 1 && IsForward(carrier, receiver.Position))
                        return "A carrier sharing a square can only pass sideways or backwards.";
                    if (action.Option == CarrierOption.StraightPass && !IsStraightPassInRange(carrier.Position, receiver.Position))
                        return $"Too far for a pass (at most {MaxStraightPassCells} squares): play a long pass.";
                    if (action.Option == CarrierOption.StraightPass && !State.IsLaneClear(carrier.Position, receiver.Position))
                        return "The passing lane is blocked.";
                    return null;
                default:
                    return "Unknown ball-carrier option.";
            }
        }

        /// <summary>Longest straight pass in squares (a fraction of the pitch length, see rules.json).</summary>
        public int MaxStraightPassCells => Math.Max(1, (int)Math.Floor(Pitch.Columns * Rules.Passing.MaxStraightPassFieldFraction + 1e-9));

        public bool IsStraightPassInRange(Cell from, Cell to) => from.DistanceTo(to) <= MaxStraightPassCells;

        private static bool IsForward(Player from, Cell to) => (to.X - from.Position.X) * from.Team.AttackDirection() > 0;

        /// <summary>True for the goal-line squares inside the goal defended by <paramref name="defending"/>.</summary>
        public bool IsGoalCell(Team defending, Cell cell) =>
            cell.X == Pitch.GoalLineX(defending) && cell.Y >= Pitch.GoalTop && cell.Y <= Pitch.GoalBottom;

        /// <summary>The goal square a shot aims at: the chosen one, or the one in the shooter's row.</summary>
        public Cell ShotTargetFor(Player shooter, CarrierAction action) =>
            action?.TargetCell ?? Pitch.ShotTarget(shooter.Team, shooter.Position);

        private static string Name(Player p) => $"{p.Team} #{p.Number}";

        /// <summary>Shot blocking is judged on the board as it stands when the shot is declared.</summary>
        private void RecordShotBlocker(TeamPlan plan)
        {
            State.PendingShotBlockerId = null;
            if (plan.Action == null || plan.Action.Option != CarrierOption.Shot) return;

            var shooter = State.BallCarrier;
            var target = ShotTargetFor(shooter, plan.Action);
            int stepX = Math.Sign(target.X - shooter.Position.X), stepY = Math.Sign(target.Y - shooter.Position.Y);
            if (stepX == 0 && stepY == 0) return;

            var blockCell = shooter.Position.Offset(stepX, stepY);
            var blocker = State.PlayersAt(blockCell)
                .Where(p => p.Team != shooter.Team && !p.IsGoalkeeper)
                .OrderBy(p => p.Id)
                .FirstOrDefault();
            if (blocker == null) return;
            State.PendingShotBlockerId = blocker.Id;
            State.PendingBlockCell = blockCell;
        }

        // ------------------------------------------------------------------ turn resolution

        private void ResolveTurn(TeamPlan turnPlan, TeamPlan otherPlan, List<GameEvent> events)
        {
            var turnTeam = State.TurnTeam;
            var moves = turnPlan.Moves.Concat(otherPlan.Moves).ToList();
            Team? conceded = null;
            _plannedFrom = State.Players.ToDictionary(p => p.Id, p => p.Position);

            if (Rules.Turns.ActionBeforeMovement)
            {
                // Pass or shot first, on the board both teams planned against; then everyone moves.
                conceded = ActionStep(turnPlan, events);
                if (!State.ClockExpired)
                {
                    if (conceded.HasValue) events.Add(new StepStarted(StepKind.Arrangement)); // kick-off reset replaces the moves
                    else ArrangementStep(moves, events);
                    if (conceded.HasValue) AdvanceClock(events);
                }
            }
            else
            {
                // Spec v1.3 order: everyone moves, then the declared pass or shot.
                ArrangementStep(moves, events);
                if (!State.ClockExpired) conceded = ActionStep(turnPlan, events);
            }

            ClearPending();

            if (conceded.HasValue && State.IsGoldenGoal)
            {
                EndMatch(events);
                return;
            }

            var nextTeam = turnTeam.Opponent();
            if (conceded.HasValue)
            {
                ResetForKickoff(conceded.Value, events);
                nextTeam = conceded.Value;
            }
            else if (Rules.Turns.PossessionTeamPlaysFirst)
            {
                // The team holding the ball keeps taking the turns until the other team really owns it.
                // A loose ball (an off-target long pass, say) does not switch the turn.
                nextTeam = State.PossessionTeam ?? turnTeam;
            }

            State.TurnTeam = nextTeam;
            if (State.ClockExpired)
            {
                EndOfRegulation(events);
                return;
            }
            BeginTurn(nextTeam, events);
        }

        /// <summary>Movement, loose-ball claims and tackles; one clock step.</summary>
        private void ArrangementStep(List<MoveOrder> moves, List<GameEvent> events)
        {
            events.Add(new StepStarted(StepKind.Arrangement));
            var executed = ResolveMovement(moves, events);
            var clashed = ClaimLooseBall(events);
            ResolveTackles(executed, clashed, events);
            AdvanceClock(events);
        }

        /// <summary>The declared pass or shot; one clock step. Returns the conceding team after a goal.</summary>
        private Team? ActionStep(TeamPlan turnPlan, List<GameEvent> events)
        {
            Team? conceded = null;
            var action = turnPlan.Action;
            var declaredCarrierId = State.PendingCarrierId;
            events.Add(new StepStarted(StepKind.Action));
            if (declaredCarrierId.HasValue && action != null && action.Option != CarrierOption.Move)
            {
                var carrier = State.BallCarrier;
                if (carrier == null || carrier.Id != declaredCarrierId.Value)
                    events.Add(new ActionCancelled(declaredCarrierId.Value, action.Option, "The carrier lost the ball before the action."));
                else
                    conceded = ResolveAction(carrier, action, events);
            }
            AdvanceClock(events);
            return conceded;
        }

        /// <summary>
        /// All moves happen at once. Any square that would end up over the limit cancels every move
        /// into it; this repeats until no square is over the limit. A player the action already
        /// relocated (throw-in taker, goal-kick keeper, blocker) keeps its new square instead.
        /// </summary>
        private Dictionary<int, Cell> _plannedFrom; // positions the plans were made from

        private List<MoveOrder> ResolveMovement(List<MoveOrder> moves, List<GameEvent> events)
        {
            var relocated = moves.Where(m => _plannedFrom != null && _plannedFrom.TryGetValue(m.PlayerId, out var from)
                                             && State.GetPlayer(m.PlayerId).Position != from).ToList();
            foreach (var m in relocated) events.Add(new MoveCancelled(m.PlayerId, m.Target));
            moves = moves.Except(relocated).ToList();

            var byPlayer = moves.ToDictionary(m => m.PlayerId);
            var cancelled = new HashSet<int>();

            while (true)
            {
                var counts = new Dictionary<Cell, int>();
                foreach (var p in State.Players.Where(p => p.Active))
                {
                    var cell = byPlayer.TryGetValue(p.Id, out var m) && !cancelled.Contains(p.Id) ? m.Target : p.Position;
                    counts[cell] = counts.TryGetValue(cell, out var n) ? n + 1 : 1;
                }

                var overfull = new HashSet<Cell>(counts.Where(kv => kv.Value > Pitch.MaxPlayersPerSquare).Select(kv => kv.Key));
                if (overfull.Count == 0) break;

                var changed = false;
                foreach (var m in moves)
                {
                    if (cancelled.Contains(m.PlayerId) || !overfull.Contains(m.Target)) continue;
                    cancelled.Add(m.PlayerId);
                    changed = true;
                }
                if (!changed) break;
            }

            var executed = new List<MoveOrder>();
            foreach (var m in moves)
            {
                if (cancelled.Contains(m.PlayerId)) events.Add(new MoveCancelled(m.PlayerId, m.Target));
                else executed.Add(m);
            }
            foreach (var m in executed) Relocate(State.GetPlayer(m.PlayerId), m.Target, events);
            return executed;
        }

        /// <summary>Whoever stands on a loose ball after movement owns it; two arrivals clash 50/50.</summary>
        private HashSet<int> ClaimLooseBall(List<GameEvent> events)
        {
            var contenders = new HashSet<int>();
            if (!State.Ball.IsLoose) return contenders;

            var arrivals = State.PlayersAt(State.Ball.LoosePosition).OrderBy(p => p.Id).ToList();
            if (arrivals.Count == 0) return contenders;

            var winner = Pick(arrivals);
            GiveBall(winner);
            events.Add(new LooseBallClaimed(winner.Id, arrivals.Select(p => p.Id).ToList()));
            if (arrivals.Count > 1)
                foreach (var p in arrivals) contenders.Add(p.Id);
            return contenders;
        }

        private void ResolveTackles(List<MoveOrder> executed, HashSet<int> clashed, List<GameEvent> events)
        {
            var carrier = State.BallCarrier;
            if (carrier == null) return;

            foreach (var m in executed)
            {
                if (!m.Tackle || clashed.Contains(m.PlayerId)) continue;
                var tackler = State.GetPlayer(m.PlayerId);
                if (tackler.Team == carrier.Team || tackler.Position != carrier.Position) continue;

                var success = _random.Chance(Rules.Tackle.SuccessChance);
                events.Add(new TackleResolved(tackler.Id, carrier.Id, success));
                if (success) GiveBall(tackler);
                return; // one tackle per turn: at most one opponent can share the carrier's square anyway
            }
        }

        private Team? ResolveAction(Player carrier, CarrierAction action, List<GameEvent> events)
        {
            switch (action.Option)
            {
                case CarrierOption.StraightPass:
                    ResolveStraightPass(carrier, State.GetPlayer(action.ReceiverId.Value), events);
                    return null;
                case CarrierOption.LongPass:
                    var receiver = action.ReceiverId.HasValue ? State.GetPlayer(action.ReceiverId.Value) : null;
                    ResolveLongPass(carrier, receiver, receiver?.Position ?? action.TargetCell.Value, events);
                    return null;
                case CarrierOption.Shot:
                    return ResolveShot(carrier, ShotTargetFor(carrier, action), events);
                default:
                    return null;
            }
        }

        private void ResolveStraightPass(Player passer, Player receiver, List<GameEvent> events)
        {
            if (Rules.Passing.RevalidateLaneAfterMovement && !State.IsLaneClear(passer.Position, receiver.Position))
            {
                events.Add(new ActionCancelled(passer.Id, CarrierOption.StraightPass, "The passing lane was blocked after movement."));
                return;
            }
            Deliver(PassKind.Straight, passer, receiver, events);
        }

        /// <summary>
        /// Long pass to a teammate or to a square. On target (longPassSuccessChance) the ball arrives at the
        /// target; otherwise it lands on one of the 8 squares around it.
        /// </summary>
        private void ResolveLongPass(Player passer, Player receiver, Cell target, List<GameEvent> events)
        {
            var receiverId = receiver?.Id;
            if (_random.Chance(Rules.Passing.LongPassSuccessChance))
            {
                if (receiver != null)
                {
                    Deliver(PassKind.Long, passer, receiver, events);
                    return;
                }
                ArriveAtSquare(passer, target, events);
                return;
            }

            var neighbours = Pitch.Neighbours(target);
            var landing = neighbours[_random.NextInt(neighbours.Count)];

            if (!Pitch.InBounds(landing))
            {
                State.LastTouch = passer.Team;
                events.Add(new PassResolved(PassKind.Long, passer.Id, receiverId, PassOutcome.OutOfPlay, null, landing, false, target));
                HandleOutOfPlay(landing, passer.Team, events);
                return;
            }

            var occupants = State.PlayersAt(landing).OrderBy(p => p.Id).ToList();
            var opponents = occupants.Where(p => p.Team != passer.Team).ToList();
            if (opponents.Count > 0)
            {
                var taker = Pick(opponents);
                GiveBall(taker);
                events.Add(new PassResolved(PassKind.Long, passer.Id, receiverId, PassOutcome.LandedOnOpponent, taker.Id, landing, false, target));
            }
            else if (occupants.Count > 0)
            {
                var taker = Pick(occupants);
                GiveBall(taker);
                events.Add(new PassResolved(PassKind.Long, passer.Id, receiverId, PassOutcome.LandedOnTeammate, taker.Id, landing, false, target));
            }
            else
            {
                SetLoose(landing, passer.Team);
                events.Add(new PassResolved(PassKind.Long, passer.Id, receiverId, PassOutcome.LooseBall, null, landing, false, target));
            }
        }

        /// <summary>
        /// An accurate long pass into a chosen square. Empty: the ball waits there for whoever runs onto it.
        /// Teammates only: a teammate has it. Opponents only: an opponent has it. Both: 50/50 (contested).
        /// </summary>
        private void ArriveAtSquare(Player passer, Cell square, List<GameEvent> events)
        {
            var occupants = State.PlayersAt(square).OrderBy(p => p.Id).ToList();
            var mates = occupants.Where(p => p.Team == passer.Team).ToList();
            var opponents = occupants.Where(p => p.Team != passer.Team).ToList();

            if (occupants.Count == 0)
            {
                SetLoose(square, passer.Team);
                events.Add(new PassResolved(PassKind.Long, passer.Id, null, PassOutcome.ReachedSquare, null, square, false, square));
                return;
            }

            var contested = mates.Count > 0 && opponents.Count > 0;
            var ours = mates.Count > 0 && (!contested || _random.Chance(Rules.Passing.ContestedDestinationSuccessChance));
            var taker = Pick(ours ? mates : opponents);
            GiveBall(taker);
            events.Add(new PassResolved(PassKind.Long, passer.Id, null, ours ? PassOutcome.Completed : PassOutcome.Intercepted,
                taker.Id, square, contested, square));
        }

        private void SetLoose(Cell cell, Team lastTouch)
        {
            State.Ball.OwnerId = null;
            State.Ball.LoosePosition = cell;
            State.LastTouch = lastTouch;
        }

        /// <summary>Ball arrives at the receiver; an opponent on the same square contests it.</summary>
        private void Deliver(PassKind kind, Player passer, Player receiver, List<GameEvent> events)
        {
            var opponents = State.PlayersAt(receiver.Position).Where(p => p.Team != receiver.Team).OrderBy(p => p.Id).ToList();
            if (opponents.Count == 0)
            {
                GiveBall(receiver);
                events.Add(new PassResolved(kind, passer.Id, receiver.Id, PassOutcome.Completed, receiver.Id, receiver.Position, false));
                return;
            }

            if (_random.Chance(Rules.Passing.ContestedDestinationSuccessChance))
            {
                GiveBall(receiver);
                events.Add(new PassResolved(kind, passer.Id, receiver.Id, PassOutcome.Completed, receiver.Id, receiver.Position, true));
            }
            else
            {
                var interceptor = Pick(opponents);
                GiveBall(interceptor);
                events.Add(new PassResolved(kind, passer.Id, receiver.Id, PassOutcome.Intercepted, interceptor.Id, receiver.Position, true));
            }
        }

        /// <summary>Returns the conceding team if a goal was scored.</summary>
        private Team? ResolveShot(Player shooter, Cell target, List<GameEvent> events)
        {
            var defending = shooter.Team.Opponent();

            if (State.PendingShotBlockerId.HasValue)
            {
                var blocker = State.GetPlayer(State.PendingShotBlockerId.Value);
                var blockCell = State.PendingBlockCell;
                if (blocker.Position != blockCell && State.OccupancyAt(blockCell) < Pitch.MaxPlayersPerSquare)
                    Relocate(blocker, blockCell, events);
                GiveBall(blocker);
                events.Add(new ShotResolved(shooter.Id, ShotOutcome.Blocked, target, blocker.Id, 0, 0));
                return null;
            }

            var challenger = State.PlayersAt(shooter.Position).Where(p => p.Team != shooter.Team).OrderBy(p => p.Id).FirstOrDefault();
            if (challenger != null && !_random.Chance(Rules.Shooting.ContestedShooterWinChance))
            {
                GiveBall(challenger);
                events.Add(new ShotResolved(shooter.Id, ShotOutcome.LostContest, target, challenger.Id, 0, 0));
                return null;
            }

            var accuracy = ShotAccuracy(shooter.Team, shooter.Position);
            var keeper = State.Goalkeeper(defending);
            var goalChance = GoalChance(target.DistanceTo(keeper.Position));

            if (!_random.Chance(accuracy))
            {
                State.LastTouch = shooter.Team;
                events.Add(new ShotResolved(shooter.Id, ShotOutcome.Missed, target, null, accuracy, goalChance));
                AwardGoalKick(defending, events);
                return null;
            }

            if (!_random.Chance(goalChance))
            {
                GiveBall(keeper);
                events.Add(new ShotResolved(shooter.Id, ShotOutcome.Saved, target, keeper.Id, accuracy, goalChance));
                return null;
            }

            State.AddGoal(shooter.Team);
            events.Add(new ShotResolved(shooter.Id, ShotOutcome.Goal, target, keeper.Id, accuracy, goalChance));
            events.Add(new GoalScored(shooter.Team, State.Score(Team.Red), State.Score(Team.Blue)));
            return defending;
        }

        // ------------------------------------------------------------------ restarts

        private void HandleOutOfPlay(Cell exit, Team lastTouch, List<GameEvent> events)
        {
            if (exit.X < 0 || exit.X >= Pitch.Columns)
            {
                AwardGoalKick(exit.X < 0 ? Team.Red : Team.Blue, events);
                return;
            }
            var spot = new Cell(exit.X, Math.Min(Math.Max(exit.Y, 0), Pitch.Rows - 1));
            AwardThrowIn(lastTouch.Opponent(), spot, events);
        }

        private void AwardGoalKick(Team team, List<GameEvent> events)
        {
            var keeper = State.Goalkeeper(team);
            var centre = Pitch.GoalCentre(team);
            if (keeper.Position != centre && State.OccupancyAt(centre) < Pitch.MaxPlayersPerSquare)
                Relocate(keeper, centre, events);
            GiveBall(keeper);
            events.Add(new RestartAwarded(RestartKind.GoalKick, team, keeper.Id, keeper.Position, false));
        }

        private void AwardThrowIn(Team team, Cell spot, List<GameEvent> events)
        {
            var candidates = State.TeamPlayers(team).Where(p => p.Active && !p.IsGoalkeeper).OrderBy(p => p.Id).ToList();
            var best = candidates.Min(p => p.Position.DistanceTo(spot));
            var nearest = candidates.Where(p => p.Position.DistanceTo(spot) == best).ToList();
            var taker = Pick(nearest);

            Relocate(taker, NearestCellWithRoom(spot, taker), events);
            GiveBall(taker);
            events.Add(new RestartAwarded(RestartKind.ThrowIn, team, taker.Id, taker.Position, nearest.Count > 1));
        }

        private Cell NearestCellWithRoom(Cell spot, Player mover)
        {
            var maxRadius = Math.Max(Pitch.Rows, Pitch.Columns);
            for (var r = 0; r <= maxRadius; r++)
            for (var y = spot.Y - r; y <= spot.Y + r; y++)
            for (var x = spot.X - r; x <= spot.X + r; x++)
            {
                var cell = new Cell(x, y);
                if (cell.DistanceTo(spot) != r || !Pitch.InBounds(cell)) continue;
                var others = State.PlayersAt(cell).Count(p => p.Id != mover.Id);
                if (others < Pitch.MaxPlayersPerSquare) return cell;
            }
            return mover.Position;
        }

        private void ResetForKickoff(Team kicking, List<GameEvent> events)
        {
            foreach (var team in new[] { Team.Red, Team.Blue })
            {
                var layout = Formation.For(team, Rules, Pitch, State.PlayersPerTeam);
                foreach (var p in State.TeamPlayers(team).ToList())
                    Relocate(p, layout[p.Number - 1], events);
            }
            PlaceKicker(kicking, events);
        }

        private void PlaceKicker(Team team, List<GameEvent> events)
        {
            var spot = Pitch.KickoffSpot(team);
            var kicker = State.TeamPlayers(team)
                .Where(p => p.Active && !p.IsGoalkeeper)
                .OrderBy(p => p.Position.DistanceTo(spot))
                .ThenBy(p => p.Id)
                .First();
            Relocate(kicker, spot, events);
            GiveBall(kicker);
            State.LastTouch = null;
            events.Add(new RestartAwarded(RestartKind.Kickoff, team, kicker.Id, spot, false));
        }

        // ------------------------------------------------------------------ match flow

        private void BeginTurn(Team team, List<GameEvent> events)
        {
            ClearPending();
            State.Phase = MatchPhase.Planning;
            State.TurnTeam = team;
            State.PlanningTeam = team;
            State.TurnNumber++;
            events.Add(new TurnStarted(team, State.TurnNumber));
        }

        private void EndOfRegulation(List<GameEvent> events)
        {
            if (State.Score(Team.Red) == State.Score(Team.Blue) && Rules.Ties.OfferGoldenGoal)
            {
                State.Phase = MatchPhase.AwaitingTieDecision;
                State.TieChoices.Clear();
                events.Add(new TieDecisionRequired());
                return;
            }
            EndMatch(events);
        }

        private void EndMatch(List<GameEvent> events)
        {
            State.Phase = MatchPhase.MatchOver;
            State.Result = new MatchResult(State.Score(Team.Red), State.Score(Team.Blue), State.IsGoldenGoal);
            events.Add(new MatchEnded(State.Result));
        }

        private void AdvanceClock(List<GameEvent> events)
        {
            State.ElapsedSeconds += Rules.Clock.SecondsPerStep;
            events.Add(new ClockAdvanced(State.ElapsedSeconds, State.RemainingSeconds));
        }

        private void ClearPending()
        {
            State.PendingTurnPlan = null;
            State.PendingCarrierId = null;
            State.PendingShotBlockerId = null;
        }

        // ------------------------------------------------------------------ primitives

        private void CreatePlayers()
        {
            foreach (var team in new[] { Team.Red, Team.Blue })
            {
                var layout = Formation.For(team, Rules, Pitch, State.PlayersPerTeam);
                for (var i = 0; i < layout.Count; i++)
                    State.PlayerList.Add(new Player(team, i + 1, i == 0 ? PlayerRole.Goalkeeper : PlayerRole.Outfield, layout[i]));
            }
            State.Ball.OwnerId = null;
            State.Ball.LoosePosition = Pitch.KickoffSpot(Team.Red);
        }

        private void GiveBall(Player player)
        {
            State.Ball.OwnerId = player.Id;
            State.LastTouch = player.Team;
        }

        private static void Relocate(Player player, Cell cell, List<GameEvent> events)
        {
            if (player.Position == cell) return;
            events.Add(new PlayerMoved(player.Id, player.Position, cell));
            player.Position = cell;
        }

        private Player Pick(IReadOnlyList<Player> players) =>
            players.Count == 1 ? players[0] : players[_random.NextInt(players.Count)];

        // ------------------------------------------------------------------ scenario hooks (tests, saved games)

        internal void ForcePosition(int playerId, Cell cell) => State.GetPlayer(playerId).Position = cell;

        internal void ForceBallOwner(int playerId) => GiveBall(State.GetPlayer(playerId));

        internal void ForceLooseBall(Cell cell)
        {
            State.Ball.OwnerId = null;
            State.Ball.LoosePosition = cell;
        }

        internal void ForceTurn(Team team)
        {
            ClearPending();
            State.Phase = MatchPhase.Planning;
            State.TurnTeam = team;
            State.PlanningTeam = team;
        }

        internal void ForceElapsed(int seconds) => State.ElapsedSeconds = seconds;
    }
}
