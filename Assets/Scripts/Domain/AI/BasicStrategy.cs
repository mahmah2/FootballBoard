using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballBoard.Domain.AI
{
    /// <summary>A computer opponent. It only reads the public match state, so it never sees a hidden plan.</summary>
    public interface IMatchStrategy
    {
        TeamPlan PlanTurn(MatchEngine engine, Team team);
        TieChoice ChooseTie(MatchEngine engine, Team team);
    }

    /// <summary>
    /// Rule-based heuristic (spec §12): generate legal candidates, score them, pick the best with a small
    /// random factor. Scores follow the spec's guide: scoring chances, forward progression, attacking
    /// positions, cover of shooting lanes, contested destinations and dangerous losses of possession.
    /// </summary>
    public sealed class BasicStrategy : IMatchStrategy
    {
        private const double GoalValue = 100;
        private const double LossPenalty = 30;
        private const double ContestPenalty = 20;
        private const double AttackingPositionBonus = 30;
        private const double ProgressPerCell = 3;
        private const double Noise = 3;

        private readonly IRandomSource _random;

        public BasicStrategy(IRandomSource random = null)
        {
            _random = random ?? new SeededRandom((ulong)DateTime.UtcNow.Ticks);
        }

        public TieChoice ChooseTie(MatchEngine engine, Team team) => TieChoice.GoldenGoal;

        public TeamPlan PlanTurn(MatchEngine engine, Team team)
        {
            var plan = new Planner(engine, team, _random).Build();
            if (engine.ValidatePlan(team, plan) == null) return plan;

            // Should not happen, but never submit an illegal plan: drop moves until it is legal.
            var action = plan.Action;
            foreach (var move in plan.Moves.ToList())
            {
                plan.Moves.Remove(move);
                if (engine.ValidatePlan(team, plan) == null) return plan;
            }
            plan = new TeamPlan { Action = action };
            return engine.ValidatePlan(team, plan) == null ? plan : TeamPlan.Empty();
        }

        /// <summary>One planning pass for one team.</summary>
        private sealed class Planner
        {
            private readonly MatchEngine _engine;
            private readonly MatchState _s;
            private readonly Team _team;
            private readonly IRandomSource _random;
            private readonly TeamPlan _plan = new TeamPlan();
            private readonly HashSet<int> _locked = new HashSet<int>();
            private readonly Dictionary<Cell, int> _occupancy = new Dictionary<Cell, int>();
            private Cell _ballAfterAction;

            public Planner(MatchEngine engine, Team team, IRandomSource random)
            {
                _engine = engine;
                _s = engine.State;
                _team = team;
                _random = random;
            }

            private Pitch Pitch => _s.Pitch;
            private Team Opponent => _team.Opponent();
            private IEnumerable<Player> Mine => _s.TeamPlayers(_team).Where(p => p.Active);
            private IEnumerable<Player> Theirs => _s.TeamPlayers(Opponent).Where(p => p.Active);

            public TeamPlan Build()
            {
                foreach (var p in _s.Players.Where(p => p.Active)) Add(p.Position, 1);
                var carrier = _s.BallCarrier;
                _ballAfterAction = _s.BallPosition;

                if (carrier != null && carrier.Team == _team)
                {
                    if (_s.TurnTeam == _team) PlanAttackAction(carrier);
                    else PlanCarrierOffTurn(carrier);
                }

                PlanKeeper();
                if (_s.Ball.IsLoose) PlanChase();
                if (carrier != null && carrier.Team != _team) PlanPress(carrier);

                foreach (var p in Mine.Where(p => !p.IsGoalkeeper && !_locked.Contains(p.Id)).OrderBy(p => p.Id))
                {
                    if (carrier != null && carrier.Team == _team) PlanSupport(p);
                    else PlanMark(p);
                }
                return _plan;
            }

            // ---------------------------------------------------------- attacking choice

            private void PlanAttackAction(Player carrier)
            {
                _locked.Add(carrier.Id);
                var options = new List<(double score, Action apply)>();

                // Shots at every goal square.
                for (var y = Pitch.GoalTop; y <= Pitch.GoalBottom; y++)
                {
                    var target = new Cell(Pitch.GoalLineX(Opponent), y);
                    var value = ShotValue(carrier.Position, target);
                    var t = target;
                    options.Add((value, () => SetAction(CarrierAction.Shot(t), null)));
                }

                var here = PositionValue(carrier.Position);
                foreach (var mate in Mine.Where(p => p.Id != carrier.Id))
                {
                    var gain = PositionValue(mate.Position) - here + (mate.IsGoalkeeper ? -15 : 0);
                    var risk = OpponentsWithin(mate.Position, 1) > 0 ? ContestPenalty : 0;
                    var m = mate;

                    var straight = CarrierAction.StraightPass(mate.Id);
                    if (Legal(straight))
                        options.Add((gain - risk + 5, () => SetAction(CarrierAction.StraightPass(m.Id), m)));

                    var longPass = CarrierAction.LongPass(mate.Id);
                    var p = _s.Rules.Passing.LongPassSuccessChance;
                    if (Legal(longPass))
                        options.Add((p * gain - (1 - p) * LossPenalty, () => SetAction(CarrierAction.LongPass(m.Id), m)));
                }

                // Run with the ball (or keep it).
                options.Add((-Danger(carrier.Position), () => { }));
                foreach (var cell in _engine.GetMoveTargets(carrier.Id))
                {
                    if (Count(cell) >= Pitch.MaxPlayersPerSquare || OpponentsAt(cell) > 0) continue;
                    var value = PositionValue(cell) - here - Danger(cell);
                    var c = cell;
                    options.Add((value, () => Move(carrier, c)));
                }

                var best = options.OrderByDescending(o => o.score + Jitter()).First();
                best.apply();
            }

            private void SetAction(CarrierAction action, Player receiver)
            {
                _plan.Action = action;
                if (receiver != null)
                {
                    _locked.Add(receiver.Id);
                    _ballAfterAction = receiver.Position;
                }
            }

            private bool Legal(CarrierAction action) =>
                _engine.ValidatePlan(_team, new TeamPlan { Action = action }) == null;

            private void PlanCarrierOffTurn(Player carrier)
            {
                // We keep the ball on the other team's turn: step away from pressure if that is safer.
                _locked.Add(carrier.Id);
                var best = carrier.Position;
                var bestValue = PositionValue(carrier.Position) - Danger(carrier.Position);
                foreach (var cell in _engine.GetMoveTargets(carrier.Id))
                {
                    if (Count(cell) >= Pitch.MaxPlayersPerSquare || OpponentsAt(cell) > 0) continue;
                    var value = PositionValue(cell) - Danger(cell) + Jitter();
                    if (value > bestValue) { best = cell; bestValue = value; }
                }
                if (best != carrier.Position) Move(carrier, best);
            }

            // ---------------------------------------------------------- off-ball movement

            private void PlanSupport(Player p)
            {
                // Push into open space toward goal, keep a passing lane from where the ball will be.
                Choose(p, cell =>
                    PositionValue(cell) * 0.5
                    + 5 * Math.Min(NearestOpponent(cell), 3)
                    + (_s.IsLaneClear(_ballAfterAction, cell) ? 10 : 0)
                    - (TeammatesNear(cell, p.Id) > 0 ? 8 : 0));
            }

            private void PlanMark(Player p)
            {
                // Goal-side of the most dangerous attacker this player can reach; cover the shooting line.
                var ownGoal = Pitch.GoalCentre(_team);
                var threat = Theirs.Where(o => !o.IsGoalkeeper)
                    .OrderBy(o => o.Position.DistanceTo(ownGoal) + o.Position.DistanceTo(p.Position) * 0.5)
                    .FirstOrDefault();
                var markCell = threat == null ? ownGoal : StepToward(threat.Position, ownGoal);
                var ball = _s.BallPosition;
                Choose(p, cell =>
                    -2.0 * cell.DistanceTo(markCell)
                    + (OnLine(ball, ownGoal, cell) ? 40 : 0) * 0.25
                    - (TeammatesNear(cell, p.Id) > 0 ? 6 : 0));
            }

            private void PlanPress(Player carrier)
            {
                // Nearest outfield player closes down the carrier; tackle if it can reach the square.
                var presser = Mine.Where(p => !p.IsGoalkeeper && !_locked.Contains(p.Id))
                    .OrderBy(p => p.Position.DistanceTo(carrier.Position)).ThenBy(p => p.Id).FirstOrDefault();
                if (presser == null) return;
                _locked.Add(presser.Id);
                var targets = _engine.GetMoveTargets(presser.Id);
                if (targets.Contains(carrier.Position) && Count(carrier.Position) < Pitch.MaxPlayersPerSquare)
                {
                    Move(presser, carrier.Position, tackle: true);
                    return;
                }
                var step = targets.Where(c => Count(c) < Pitch.MaxPlayersPerSquare)
                    .OrderBy(c => c.DistanceTo(carrier.Position)).ThenBy(c => c.DistanceTo(Pitch.GoalCentre(_team)))
                    .Cast<Cell?>().FirstOrDefault();
                if (step.HasValue && step.Value.DistanceTo(carrier.Position) < presser.Position.DistanceTo(carrier.Position))
                    Move(presser, step.Value);
            }

            private void PlanChase()
            {
                var ball = _s.Ball.LoosePosition;
                var chaser = Mine.Where(p => !p.IsGoalkeeper && !_locked.Contains(p.Id))
                    .OrderBy(p => p.Position.DistanceTo(ball)).ThenBy(p => p.Id).FirstOrDefault();
                if (chaser == null) return;
                _locked.Add(chaser.Id);
                var step = _engine.GetMoveTargets(chaser.Id).Where(c => Count(c) < Pitch.MaxPlayersPerSquare)
                    .OrderBy(c => c.DistanceTo(ball)).ThenBy(c => Euclid(c, ball)).Cast<Cell?>().FirstOrDefault();
                if (step.HasValue && step.Value.DistanceTo(ball) < chaser.Position.DistanceTo(ball)) Move(chaser, step.Value);
            }

            private void PlanKeeper()
            {
                var keeper = Mine.FirstOrDefault(p => p.IsGoalkeeper);
                if (keeper == null || _locked.Contains(keeper.Id)) return;
                _locked.Add(keeper.Id);
                if (_s.BallCarrier != null && _s.BallCarrier.Id == keeper.Id) return;
                var ball = _s.BallPosition;
                var target = new Cell(Pitch.GoalLineX(_team), Math.Min(Math.Max(ball.Y, Pitch.GoalTop), Pitch.GoalBottom));
                if (keeper.Position == target) return;
                var best = _engine.GetMoveTargets(keeper.Id).Where(c => Count(c) < Pitch.MaxPlayersPerSquare)
                    .OrderBy(c => c.DistanceTo(target)).ThenBy(c => Math.Abs(c.X - target.X)).Cast<Cell?>().FirstOrDefault();
                if (best.HasValue && best.Value.DistanceTo(target) < keeper.Position.DistanceTo(target)) Move(keeper, best.Value);
            }

            private void Choose(Player p, Func<Cell, double> score)
            {
                var best = p.Position;
                var bestScore = score(p.Position) + Jitter();
                foreach (var cell in _engine.GetMoveTargets(p.Id))
                {
                    if (Count(cell) >= Pitch.MaxPlayersPerSquare) continue;
                    var s = score(cell) + Jitter();
                    if (s > bestScore) { best = cell; bestScore = s; }
                }
                if (best != p.Position) Move(p, best);
            }

            private void Move(Player p, Cell target, bool tackle = false)
            {
                _plan.Moves.Add(new MoveOrder(p.Id, target, tackle));
                Add(p.Position, -1);
                Add(target, 1);
                if (_s.BallCarrier != null && _s.BallCarrier.Id == p.Id) _ballAfterAction = target;
            }

            // ---------------------------------------------------------- evaluation

            /// <summary>How good it is for this team to have the ball on <paramref name="cell"/>.</summary>
            private double PositionValue(Cell cell)
            {
                var fromOwnGoal = Pitch.DistanceFromGoalLine(_team, cell);
                var toGoal = Pitch.DistanceFromGoalLine(Opponent, cell);
                var value = ProgressPerCell * fromOwnGoal;
                if (toGoal <= Pitch.Columns / 3) value += AttackingPositionBonus;
                value += 0.5 * Math.Max(0, BestShot(cell));
                return value;
            }

            private double BestShot(Cell from)
            {
                var best = double.MinValue;
                for (var y = Pitch.GoalTop; y <= Pitch.GoalBottom; y++)
                    best = Math.Max(best, ShotValue(from, new Cell(Pitch.GoalLineX(Opponent), y)));
                return best;
            }

            private double ShotValue(Cell from, Cell target)
            {
                var accuracy = _engine.ShotAccuracy(_team, from);
                var keeper = _s.Goalkeeper(Opponent);
                var goal = accuracy * _engine.GoalChance(target.DistanceTo(keeper.Position));
                // An adjacent defender on the line blocks the shot outright.
                int sx = Math.Sign(target.X - from.X), sy = Math.Sign(target.Y - from.Y);
                if ((sx != 0 || sy != 0) && Theirs.Any(o => !o.IsGoalkeeper && o.Position == from.Offset(sx, sy))) goal = 0;
                return goal * GoalValue - (1 - goal) * LossPenalty;
            }

            /// <summary>Risk of being tackled on a square: opponents that can step onto it.</summary>
            private double Danger(Cell cell) => OpponentsWithin(cell, 1) > 0 ? ContestPenalty : 0;

            private int OpponentsWithin(Cell cell, int range) => Theirs.Count(o => !o.IsGoalkeeper && o.Position.DistanceTo(cell) <= range);
            private int OpponentsAt(Cell cell) => Theirs.Count(o => o.Position == cell);

            private int NearestOpponent(Cell cell)
            {
                var others = Theirs.Where(o => !o.IsGoalkeeper).ToList();
                return others.Count == 0 ? 99 : others.Min(o => o.Position.DistanceTo(cell));
            }

            private int TeammatesNear(Cell cell, int self) => _plan.Moves.Count(m => m.PlayerId != self && m.Target.DistanceTo(cell) <= 1)
                + Mine.Count(p => p.Id != self && _plan.Moves.All(m => m.PlayerId != p.Id) && p.Position.DistanceTo(cell) <= 1);

            private bool OnLine(Cell from, Cell to, Cell cell) =>
                cell != from && cell != to && Pitch.LaneCells(from, to, 0.5).Contains(cell);

            private static int Euclid(Cell a, Cell b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

            private static Cell StepToward(Cell from, Cell to) => from.Offset(Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));

            private double Jitter() => (_random.NextDouble() * 2 - 1) * Noise;

            private int Count(Cell cell) => _occupancy.TryGetValue(cell, out var n) ? n : 0;

            private void Add(Cell cell, int delta) => _occupancy[cell] = Count(cell) + delta;
        }
    }
}
