using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FootballBoard.Domain;
using FootballBoard.Domain.AI;
using FootballBoard.Domain.Config;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>
    /// Application layer for a local hot-seat match: builds the world, turns taps into a TeamPlan,
    /// submits it through the engine (the only route to change the game), then plays the engine's
    /// events back as animation. Outcomes are already decided before any animation starts.
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        [SerializeField] private TextAsset rulesJson;
        [SerializeField] private Material opaqueTemplate;
        [SerializeField] private Material transparentTemplate;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private PointerInput pointer;
        [SerializeField] private Hud hud;
        [SerializeField] private bool hideBoardDuringHandover = true;

        private enum Mode { Menu, Handover, Planning, PickReceiver, ComputerPlanning, Resolving, Decision, Over }

        private RulesConfig _rules;
        private MatchEngine _engine;
        private MatchState S => _engine.State;
        private BoardSpace _space;
        private MaterialKit _kit;
        private BoardView _board;
        private BallView _ball;
        private GameObject _world;
        private readonly Dictionary<int, PlayerFigure> _figures = new Dictionary<int, PlayerFigure>();

        // What the screen currently shows (lags the engine state during playback).
        private readonly Dictionary<int, Cell> _viewCells = new Dictionary<int, Cell>();
        private int? _viewOwner;
        private Cell _viewLoose;

        private Mode _mode;
        private Team _planner;
        private readonly Dictionary<int, MoveOrder> _moves = new Dictionary<int, MoveOrder>();
        private CarrierAction _action;
        private int? _selected;
        private int _minutesIndex;
        private int _opponentIndex;   // 0 = computer, 1 = hot-seat
        private int _humanTeamIndex;  // 0 = Red, 1 = Blue
        private IMatchStrategy _strategy;

        private bool IsComputer(Team team) => S.Mode == GameMode.HumanVsComputer && team != S.HumanTeam;

        // ------------------------------------------------------------------ lifecycle

        private void Start()
        {
            _rules = RulesConfigLoader.FromJsonOrDefault(rulesJson != null ? rulesJson.text : null, out var errors);
            foreach (var error in errors) Debug.LogWarning("[Rules] " + error + " (using defaults)");
            _minutesIndex = Mathf.Max(0, _rules.Clock.PresetMinutes.IndexOf(_rules.Clock.DefaultMinutes));
            _kit = new MaterialKit(opaqueTemplate, transparentTemplate);

            hud.Build();
            hud.OptionChosen += OnOptionChosen;
            hud.ConfirmClicked += Confirm;
            hud.ClearClicked += ClearPlan;
            hud.FitClicked += () => cameraRig.Fit();
            pointer.Tapped += OnTap;
            pointer.ConfirmPressed += () => { if (!hud.OverlayVisible) Confirm(); };
            pointer.CancelPressed += Cancel;
            pointer.FitPressed += () => cameraRig.Fit();

            hud.SetPlanControls(false, false, CarrierOption.Move);
            ShowMenu();
        }

        private void ShowMenu()
        {
            _mode = Mode.Menu;
            var vsComputer = _opponentIndex == 0;
            hud.ShowOverlay("FOOTBALL BOARD",
                vsComputer
                    ? "Play against the computer. Choose your team and the match length."
                    : "Two players on one device: pass it between turns.",
                Palette.Accent, true,
                ("KICK OFF", StartMatch, true));
            var row = 0;
            hud.AddOverlayDropdown("OpponentDropdown", row++,
                new List<string> { "OPPONENT: COMPUTER", "OPPONENT: 2 PLAYERS (HOT-SEAT)" },
                _opponentIndex,
                index => { _opponentIndex = index; ShowMenu(); });
            if (vsComputer)
                hud.AddOverlayDropdown("TeamDropdown", row++,
                    new List<string> { "YOU PLAY: RED", "YOU PLAY: BLUE" },
                    _humanTeamIndex,
                    index => _humanTeamIndex = index);
            hud.AddOverlayDropdown("MatchLengthDropdown", row,
                _rules.Clock.PresetMinutes.Select(m => $"MATCH LENGTH: {m} MIN").ToList(),
                _minutesIndex,
                index => _minutesIndex = index);
        }

        private void StartMatch()
        {
            var settings = new MatchSettings
            {
                Mode = _opponentIndex == 0 ? GameMode.HumanVsComputer : GameMode.HumanVsHuman,
                HumanTeam = _humanTeamIndex == 0 ? Team.Red : Team.Blue,
                DurationMinutes = _rules.Clock.PresetMinutes[_minutesIndex]
            };
            _strategy = new BasicStrategy();
            _engine = MatchEngine.Create(_rules, settings);
            BuildWorld();
            var result = _engine.StartMatch();
            hud.HideOverlay();
            StartCoroutine(Resolve(result.Events));
        }

        private void BuildWorld()
        {
            if (_world != null) Destroy(_world);
            _figures.Clear();
            _world = new GameObject("Match");
            _space = new BoardSpace(S.Pitch.Rows, S.Pitch.Columns);

            _board = new GameObject("Board").AddComponent<BoardView>();
            _board.transform.SetParent(_world.transform, false);
            _board.Build(S.Pitch, _space, _kit);

            var pieces = new GameObject("Pieces").transform;
            pieces.SetParent(_world.transform, false);
            foreach (var player in S.Players)
                _figures[player.Id] = PlayerFigure.Create(player, _kit, pieces, cameraRig.ViewRotation);
            _ball = BallView.Create(_kit, pieces);

            cameraRig.Initialise(_space);
            SyncFromState();
            UpdateHud();
        }

        // ------------------------------------------------------------------ flow between turns

        private void AfterResolution()
        {
            switch (S.Phase)
            {
                case MatchPhase.Planning:
                    BeginHandover(S.PlanningTeam);
                    break;
                case MatchPhase.AwaitingTieDecision:
                    AskTie(Team.Red);
                    break;
                case MatchPhase.MatchOver:
                    ShowFullTime();
                    break;
            }
        }

        private void BeginHandover(Team team)
        {
            if (IsComputer(team))
            {
                StartCoroutine(ComputerPlans(team));
                return;
            }
            if (S.Mode == GameMode.HumanVsComputer)
            {
                // No device to pass around: go straight to planning with a short briefing.
                BeginPlanning(team);
                var hasBall = S.PossessionTeam == team;
                hud.ShowMessage(S.Ball.IsLoose ? "The ball is loose: get a player onto it first (two arrivals = 50/50)."
                    : hasBall && team == S.TurnTeam ? "Your ball: move it, pass, play it long or shoot. Then press CONFIRM."
                    : hasBall ? "You keep the ball on the computer's turn: move with it or reposition."
                    : "Defend: you move after their pass or shot. Step onto the ball to tackle. Then press CONFIRM.");
                return;
            }

            _mode = Mode.Handover;
            hud.SetPlanControls(false, false, CarrierOption.Move);
            string situation, instruction;
            var other = Name(team.Opponent());
            if (team == S.TurnTeam)
            {
                var hasBall = S.PossessionTeam == team;
                situation = $"Turn {S.TurnNumber} — you plan first. " + (hasBall
                    ? "You have the ball: move it, pass, play it long or shoot."
                    : S.Ball.IsLoose ? "The ball is loose: get a player onto it." : "Reposition your players to win the ball.");
                instruction = $"Arrange your plan and press CONFIRM, then hand the device to the {other} player.";
            }
            else
            {
                situation = S.Ball.IsLoose
                    ? $"{other} has planned. The ball is loose: get a player onto it first (two arrivals = 50/50)."
                    : S.PossessionTeam == team
                    ? $"{other} has planned. You keep the ball: move with it or reposition (pass or shoot on your own turn)."
                    : $"{other} has planned. Reposition to defend: you move after their pass or shot.\nStep onto whoever ends up with the ball to request a tackle.";
                instruction = "Arrange your plan and press CONFIRM. The turn then plays out: the pass or shot first, then everyone moves.";
            }
            hud.ShowOverlay($"{Name(team)} TO PLAN", $"{situation}\n\n{instruction}\nPress READY when you are holding the device.",
                Palette.TeamPrimary(team), hideBoardDuringHandover, ("READY", () => BeginPlanning(team), true));
        }

        /// <summary>The computer plans from the public board only (it cannot see a hidden human plan).</summary>
        private IEnumerator ComputerPlans(Team team)
        {
            _mode = Mode.ComputerPlanning;
            hud.SetPlanControls(false, false, CarrierOption.Move);
            hud.SetStatus("COMPUTER IS PLANNING…", Palette.TeamPrimary(team));
            yield return new WaitForSeconds(0.6f);

            var result = _engine.SubmitPlan(team, _strategy.PlanTurn(_engine, team));
            if (!result.Accepted)
            {
                Debug.LogError("[Computer] Plan rejected: " + result.Error);
                result = _engine.SubmitPlan(team, TeamPlan.Empty());
            }
            if (result.Events.OfType<PlanLocked>().Any())
                BeginHandover(S.PlanningTeam);
            else
                StartCoroutine(Resolve(result.Events));
        }

        private void BeginPlanning(Team team)
        {
            hud.HideOverlay();
            _planner = team;
            _moves.Clear();
            _action = null;
            _selected = null;
            _mode = Mode.Planning;
            hud.ShowMessage("Tap a player, then a highlighted square. Drag to pan, pinch or scroll to zoom.");
            Refresh();
        }

        private void AskTie(Team team)
        {
            if (IsComputer(team))
            {
                ChooseTie(team, _strategy.ChooseTie(_engine, team));
                return;
            }
            _mode = Mode.Decision;
            hud.ShowOverlay($"FULL TIME {S.Score(Team.Red)} - {S.Score(Team.Blue)}",
                $"{Name(team)}: take the draw, or play golden goal (next goal wins)?\nYour choice stays hidden from the other player.",
                Palette.TeamPrimary(team), true,
                ("TAKE THE DRAW", () => ChooseTie(team, TieChoice.Draw), false),
                ("GOLDEN GOAL", () => ChooseTie(team, TieChoice.GoldenGoal), true));
        }

        private void ChooseTie(Team team, TieChoice choice)
        {
            var result = _engine.SubmitTieChoice(team, choice);
            if (team == Team.Red)
            {
                AskTie(Team.Blue);
                return;
            }
            hud.HideOverlay();
            if (result.Events.OfType<GoldenGoalStarted>().Any())
                StartCoroutine(GoldenGoalIntro());
            else
                ShowFullTime();
        }

        private IEnumerator GoldenGoalIntro()
        {
            UpdateHud();
            yield return hud.Banner("GOLDEN GOAL", "The next goal wins", Palette.Accent, 1.4f, false);
            AfterResolution();
        }

        private void ShowFullTime()
        {
            _mode = Mode.Over;
            hud.SetPlanControls(false, false, CarrierOption.Move);
            var result = S.Result;
            var title = result.IsDraw ? "DRAW"
                : S.Mode == GameMode.HumanVsComputer ? (result.Winner == S.HumanTeam ? "YOU WIN" : "COMPUTER WINS")
                : $"{Name(result.Winner.Value)} WINS";
            var color = result.IsDraw ? Palette.Chalk : Palette.TeamPrimary(result.Winner.Value);
            hud.ShowOverlay(title, $"Red {result.RedScore} - {result.BlueScore} Blue" + (result.DecidedByGoldenGoal ? "  (golden goal)" : ""),
                color, false, ("PLAY AGAIN", ShowMenu, true));
        }

        // ------------------------------------------------------------------ planning input

        private bool CanDeclare => _planner == S.TurnTeam && S.PossessionTeam == _planner;

        private CarrierOption CurrentOption => _action?.Option ?? CarrierOption.Move;

        private void OnOptionChosen(CarrierOption option)
        {
            if ((_mode != Mode.Planning && _mode != Mode.PickReceiver) || !CanDeclare) return;
            var carrier = S.BallCarrier;
            switch (option)
            {
                case CarrierOption.Move:
                    _action = null;
                    _mode = Mode.Planning;
                    break;
                case CarrierOption.StraightPass:
                case CarrierOption.LongPass:
                    _moves.Remove(carrier.Id);
                    _action = new CarrierAction { Option = option };
                    _mode = Mode.PickReceiver;
                    hud.ShowMessage(option == CarrierOption.StraightPass
                        ? "Tap a teammate with a clear lane (any player in between blocks it)."
                        : "Tap a teammate, or any square to play the ball into space. It lands on target 1 time in 9.");
                    break;
                case CarrierOption.Shot:
                    _moves.Remove(carrier.Id);
                    _action = CarrierAction.Shot(S.Pitch.ShotTarget(carrier.Team, carrier.Position));
                    _mode = Mode.Planning;
                    ShowShotOdds();
                    break;
            }
            _selected = null;
            Refresh();
        }

        private void OnTap(Vector2 screen)
        {
            if (_mode != Mode.Planning && _mode != Mode.PickReceiver) return;
            var hit = cameraRig.ScreenToBoard(screen);
            if (!hit.HasValue) return;
            var cell = _space.WorldToCell(hit.Value);
            var tapped = S.Pitch.InBounds(cell) ? PlayerNear(hit.Value, cell) : null;

            if (_mode == Mode.PickReceiver)
            {
                var carrier = S.BallCarrier;
                if (tapped != null && tapped.Team == _planner && tapped.Id != carrier.Id)
                {
                    var id = tapped.Id;
                    if (TryChange(() => { _action.ReceiverId = id; _action.TargetCell = null; _moves.Remove(id); }))
                        _mode = Mode.Planning;
                }
                else if (_action.Option == CarrierOption.LongPass && S.Pitch.InBounds(cell))
                {
                    if (TryChange(() => { _action.ReceiverId = null; _action.TargetCell = cell; }))
                    {
                        _mode = Mode.Planning;
                        hud.ShowMessage("Ball into space: move a player onto that square to collect it. Two arrivals = 50/50.");
                    }
                }
                else
                {
                    hud.ShowMessage("Tap one of your teammates to receive the pass.", true);
                }
                Refresh();
                return;
            }

            // With a shot declared, tapping a square inside the opponent's goal re-aims it.
            if (_action != null && _action.Option == CarrierOption.Shot && _engine.IsGoalCell(_planner.Opponent(), cell))
            {
                if (TryChange(() => _action.TargetCell = cell)) ShowShotOdds();
                Refresh();
                return;
            }

            if (_selected.HasValue && S.Pitch.InBounds(cell))
            {
                var selected = S.GetPlayer(_selected.Value);
                if (cell == selected.Position)
                {
                    if (_moves.Remove(selected.Id)) hud.ShowMessage($"{Name(selected)} will stay put.");
                    _selected = null;
                    Refresh();
                    return;
                }
                if (_engine.GetMoveTargets(selected.Id).Contains(cell))
                {
                    var carrier = S.BallCarrier;
                    var tackle = carrier != null && carrier.Team != _planner && carrier.Position == cell;
                    var id = selected.Id;
                    if (TryChange(() => _moves[id] = new MoveOrder(id, cell, tackle)))
                    {
                        _selected = null;
                        if (tackle) hud.ShowMessage("Tackle requested: 50/50 if you reach the carrier.");
                    }
                    Refresh();
                    return;
                }
            }

            if (tapped != null && tapped.Team == _planner)
            {
                var lockReason = LockReason(tapped);
                if (lockReason != null)
                {
                    hud.ShowMessage(lockReason, true);
                    _selected = null;
                }
                else
                {
                    _selected = tapped.Id;
                    if (tapped.IsGoalkeeper)
                        hud.ShowMessage($"Goalkeeper: tap a square up to {S.Rules.Goalkeeper.MaxMoveCells} cells away, inside the penalty box.");
                }
            }
            else
            {
                _selected = null;
            }
            Refresh();
        }

        private void ShowShotOdds()
        {
            var carrier = S.BallCarrier;
            var target = _engine.ShotTargetFor(carrier, _action);
            var accuracy = _engine.ShotAccuracy(carrier.Team, carrier.Position);
            var keeper = S.Goalkeeper(carrier.Team.Opponent());
            var goal = _engine.GoalChance(target.DistanceTo(keeper.Position));
            hud.ShowMessage($"Tap a goal square to aim. On target {accuracy:P0}, then beats the keeper {goal:P0} (if the keeper stays put).");
        }

        private string LockReason(Player player)
        {
            var carrier = S.BallCarrier;
            if (carrier != null && player.Id == carrier.Id)
            {
                if (_planner != S.TurnTeam)
                    return S.Rules.Carrier.CanMoveOnOpponentTurn ? null : "Your carrier cannot move on the other team's turn.";
                if (CurrentOption != CarrierOption.Move) return "The carrier is locked: it is passing or shooting this turn.";
            }
            if (_action != null && _action.ReceiverId == player.Id) return "The pass receiver is locked this turn.";
            return null;
        }

        /// <summary>Apply a plan edit, keep it only if the engine still accepts the plan.</summary>
        private bool TryChange(System.Action change)
        {
            var movesBackup = _moves.Values.Select(m => new MoveOrder(m.PlayerId, m.Target, m.Tackle)).ToList();
            var actionBackup = _action == null ? null : new CarrierAction { Option = _action.Option, ReceiverId = _action.ReceiverId, TargetCell = _action.TargetCell };
            change();
            var error = _engine.ValidatePlan(_planner, BuildPlan());
            if (error == null) return true;
            _moves.Clear();
            foreach (var m in movesBackup) _moves[m.PlayerId] = m;
            _action = actionBackup;
            hud.ShowMessage(error, true);
            return false;
        }

        private TeamPlan BuildPlan()
        {
            var plan = new TeamPlan { Moves = _moves.Values.ToList() };
            var pendingReceiver = _action != null && (_action.Option == CarrierOption.StraightPass || _action.Option == CarrierOption.LongPass)
                && _action.ReceiverId == null && _action.TargetCell == null;
            if (_action != null && !pendingReceiver) plan.Action = _action;
            return plan;
        }

        private void Confirm()
        {
            if (_mode == Mode.PickReceiver)
            {
                hud.ShowMessage("Pick the pass receiver first, or choose another option.", true);
                return;
            }
            if (_mode != Mode.Planning) return;

            var result = _engine.SubmitPlan(_planner, BuildPlan());
            if (!result.Accepted)
            {
                hud.ShowMessage(result.Error, true);
                return;
            }
            _selected = null;
            _board.ClearOverlay();
            foreach (var f in _figures.Values) { f.SetSelected(false); f.SetLocked(false); }

            if (result.Events.OfType<PlanLocked>().Any())
                BeginHandover(S.PlanningTeam);
            else
                StartCoroutine(Resolve(result.Events));
        }

        private void ClearPlan()
        {
            if (_mode != Mode.Planning && _mode != Mode.PickReceiver) return;
            _moves.Clear();
            _action = null;
            _selected = null;
            _mode = Mode.Planning;
            hud.ShowMessage("Plan cleared.");
            Refresh();
        }

        private void Cancel()
        {
            if (_mode == Mode.PickReceiver)
            {
                _action = null;
                _mode = Mode.Planning;
            }
            _selected = null;
            if (_mode == Mode.Planning) Refresh();
        }

        private Player PlayerNear(Vector3 hit, Cell cell) =>
            S.PlayersAt(cell)
                .OrderBy(p => (_figures[p.Id].transform.position - hit).sqrMagnitude)
                .FirstOrDefault();

        // ------------------------------------------------------------------ plan visuals

        private void Refresh()
        {
            _board.ClearOverlay();
            var carrier = S.BallCarrier;
            foreach (var f in _figures.Values)
            {
                var p = S.GetPlayer(f.PlayerId);
                f.SetSelected(_selected == p.Id);
                f.SetLocked(p.Team == _planner && LockReason(p) != null);
            }

            if (_selected.HasValue)
            {
                foreach (var cell in _engine.GetMoveTargets(_selected.Value))
                {
                    var isTackle = carrier != null && carrier.Team != _planner && carrier.Position == cell;
                    _board.HighlightCell(cell, Palette.WithAlpha(isTackle ? Palette.Warning : Palette.Chalk, 0.32f));
                }
            }

            foreach (var move in _moves.Values)
            {
                var from = _figures[move.PlayerId].transform.position;
                var to = _space.CellToWorld(move.Target);
                var color = move.Tackle ? Palette.Warning : Palette.Accent;
                _board.HighlightCell(move.Target, Palette.WithAlpha(color, 0.45f), 0.5f);
                _board.DrawArrow(from, to, Palette.WithAlpha(color, 0.9f));
            }

            if (_mode == Mode.PickReceiver)
            {
                foreach (var mate in S.TeamPlayers(_planner).Where(p => p.Id != carrier.Id))
                {
                    var ok = _action.Option == CarrierOption.LongPass || S.IsLaneClear(carrier.Position, mate.Position);
                    _board.HighlightCell(mate.Position, Palette.WithAlpha(ok ? Palette.Accent : Palette.Warning, 0.35f));
                }
            }
            else if (_action != null && carrier != null)
            {
                var from = _figures[carrier.Id].transform.position;
                if (_action.Option == CarrierOption.Shot)
                {
                    var aim = _engine.ShotTargetFor(carrier, _action);
                    for (var y = S.Pitch.GoalTop; y <= S.Pitch.GoalBottom; y++)
                    {
                        var goalCell = new Cell(S.Pitch.GoalLineX(_planner.Opponent()), y);
                        _board.HighlightCell(goalCell, Palette.WithAlpha(Palette.Accent, goalCell == aim ? 0.6f : 0.18f));
                    }
                    _board.DrawArrow(from, _space.CellToWorld(aim), Palette.WithAlpha(Palette.Accent, 0.9f), 0.12f);
                }
                else if (_action.Option == CarrierOption.LongPass && _action.TargetCell.HasValue)
                {
                    _board.HighlightCell(_action.TargetCell.Value, Palette.WithAlpha(Palette.Chalk, 0.5f), 0.6f);
                    _board.DrawArrow(from, _space.CellToWorld(_action.TargetCell.Value), Palette.WithAlpha(Palette.Chalk, 0.9f), 0.12f);
                }
                else if (_action.ReceiverId.HasValue)
                {
                    var receiver = S.GetPlayer(_action.ReceiverId.Value);
                    _board.DrawArrow(from, _figures[receiver.Id].transform.position,
                        Palette.WithAlpha(_action.Option == CarrierOption.LongPass ? Palette.Chalk : Palette.Accent, 0.9f), 0.12f);
                }
            }

            hud.SetPlanControls(true, CanDeclare, CurrentOption);
            UpdateHud();
        }

        private void UpdateHud()
        {
            hud.SetScore(S.Score(Team.Red), S.Score(Team.Blue));
            hud.SetClock(S.ElapsedSeconds, S.DurationSeconds, S.IsGoldenGoal);
            if (_mode == Mode.Planning || _mode == Mode.PickReceiver)
            {
                var role = _planner == S.TurnTeam ? "YOUR TURN" : $"DEFENDING {Name(S.TurnTeam)}'S TURN";
                hud.SetStatus($"{Name(_planner)} PLANNING — {role}", Palette.TeamPrimary(_planner));
            }
            else
            {
                hud.SetStatus($"TURN {S.TurnNumber}", Palette.Chalk);
            }
        }

        // ------------------------------------------------------------------ event playback

        private IEnumerator Resolve(IReadOnlyList<GameEvent> events)
        {
            _mode = Mode.Resolving;
            hud.SetPlanControls(false, false, CarrierOption.Move);
            UpdateHud();
            yield return PlayEvents(events);
            SyncFromState();
            UpdateHud();
            AfterResolution();
        }

        private IEnumerator PlayEvents(IReadOnlyList<GameEvent> events)
        {
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i] is PlayerMoved || events[i] is MoveCancelled)
                {
                    var run = new List<GameEvent>();
                    while (i < events.Count && (events[i] is PlayerMoved || events[i] is MoveCancelled)) run.Add(events[i++]);
                    i--;
                    yield return AnimateMoves(run);
                    continue;
                }

                switch (events[i])
                {
                    case ClockAdvanced clock:
                        hud.SetClock(clock.ElapsedSeconds, S.DurationSeconds, S.IsGoldenGoal);
                        break;
                    case CoinTossed toss:
                        yield return hud.Banner($"{Name(toss.KickoffTeam)} KICKS OFF", toss.WasRandom ? "Coin toss" : "", Palette.TeamPrimary(toss.KickoffTeam), 1.0f, toss.WasRandom);
                        break;
                    case LooseBallClaimed claim:
                        SetOwner(claim.PlayerId);
                        if (claim.IsRandomized)
                            yield return hud.Banner("CLASH!", $"{Name(S.GetPlayer(claim.PlayerId))} wins the loose ball", Palette.Accent, 1.0f, true);
                        break;
                    case TackleResolved tackle:
                        yield return hud.Banner(tackle.Success ? "TACKLE WON" : "TACKLE FAILED",
                            $"{Name(S.GetPlayer(tackle.TacklerId))} on {Name(S.GetPlayer(tackle.CarrierId))}", Palette.Warning, 1.0f, true);
                        if (tackle.Success) SetOwner(tackle.TacklerId);
                        break;
                    case ActionCancelled cancelled:
                        hud.ShowMessage(cancelled.Reason, true);
                        break;
                    case PassResolved pass:
                        yield return AnimatePass(pass);
                        break;
                    case ShotResolved shot:
                        yield return AnimateShot(shot);
                        break;
                    case GoalScored goal:
                        hud.SetScore(goal.RedScore, goal.BlueScore);
                        yield return hud.Banner("GOAL!", $"{Name(goal.ScoringTeam)} scores — {goal.RedScore} : {goal.BlueScore}", Palette.TeamPrimary(goal.ScoringTeam), 1.8f, false);
                        break;
                    case RestartAwarded restart:
                        SetOwner(restart.PlayerId);
                        var title = restart.Kind == RestartKind.Kickoff ? "KICK-OFF" : restart.Kind == RestartKind.GoalKick ? "GOAL KICK" : "THROW-IN";
                        yield return hud.Banner(title, Name(restart.Team), Palette.TeamPrimary(restart.Team), 0.7f, restart.TieBrokenByCoin);
                        break;
                }
            }
        }

        private IEnumerator AnimateMoves(List<GameEvent> run)
        {
            foreach (var e in run.OfType<PlayerMoved>()) _viewCells[e.PlayerId] = e.To;
            var slots = SlotPositions();
            var routines = new List<Coroutine>();
            foreach (var pair in slots)
            {
                var figure = _figures[pair.Key];
                if ((figure.transform.position - pair.Value).sqrMagnitude > 0.0001f)
                    routines.Add(StartCoroutine(figure.MoveTo(pair.Value, 0.32f)));
            }
            foreach (var e in run.OfType<MoveCancelled>())
                routines.Add(StartCoroutine(_figures[e.PlayerId].Shake(0.3f)));
            foreach (var r in routines) yield return r;
            if (_viewOwner.HasValue) PlaceBall(); // squares may now be shared: keep the ball on the owner's side
            if (run.OfType<MoveCancelled>().Any())
                hud.ShowMessage("Some moves were cancelled: the square was full, or the player had just been moved by a restart.");
        }

        private IEnumerator AnimatePass(PassResolved pass)
        {
            var target = pass.NewOwnerId.HasValue
                ? _figures[pass.NewOwnerId.Value].transform.position
                : _space.CellToWorld(pass.BallCell);
            var isLong = pass.Kind == PassKind.Long;
            yield return _ball.FlyTo(target, isLong ? 1.1f : 0.6f, isLong ? 2.2f : 0.25f);

            if (pass.NewOwnerId.HasValue) SetOwner(pass.NewOwnerId.Value);
            else if (pass.Outcome == PassOutcome.LooseBall) SetLoose(pass.BallCell);

            if (!pass.IsRandomized) yield break;
            string title;
            switch (pass.Outcome)
            {
                case PassOutcome.Completed: title = isLong && !pass.Contested ? "CAUGHT IT! (1 IN 9)" : "WON THE 50/50"; break;
                case PassOutcome.Intercepted: title = "INTERCEPTED"; break;
                case PassOutcome.LandedOnOpponent: title = "OVERHIT — OPPONENT'S BALL"; break;
                case PassOutcome.LandedOnTeammate: title = "OVERHIT — STILL OURS"; break;
                case PassOutcome.LooseBall: title = "OFF TARGET — LOOSE BALL"; break;
                case PassOutcome.ReachedSquare: title = "ON TARGET (1 IN 9)"; break;
                default: title = "OUT OF PLAY"; break;
            }
            yield return hud.Banner(title, isLong ? "Long pass" : "Contested pass", Palette.Accent, 1.0f, true);
        }

        private IEnumerator AnimateShot(ShotResolved shot)
        {
            var targetWorld = _space.CellToWorld(shot.Target);
            var into = shot.Target.X == 0 ? Vector3.left : Vector3.right;
            switch (shot.Outcome)
            {
                case ShotOutcome.Blocked:
                case ShotOutcome.LostContest:
                    yield return _ball.FlyTo(_figures[shot.DefenderId.Value].transform.position, 0.35f, 0.1f);
                    SetOwner(shot.DefenderId.Value);
                    yield return hud.Banner(shot.Outcome == ShotOutcome.Blocked ? "BLOCKED" : "DISPOSSESSED",
                        Name(S.GetPlayer(shot.DefenderId.Value)) + " wins the ball", Palette.Warning, 0.9f, shot.IsRandomized);
                    break;
                case ShotOutcome.Missed:
                    var wide = targetWorld + into * 1.2f + Vector3.forward * (shot.Target.Y < S.Pitch.CentreRow ? 2.5f : -2.5f);
                    yield return _ball.FlyTo(wide, 0.9f, 0.9f);
                    yield return hud.Banner("WIDE!", $"On-target chance was {shot.Accuracy:P0}", Palette.Chalk, 1.0f, true);
                    break;
                case ShotOutcome.Saved:
                    yield return _ball.FlyTo(_figures[shot.DefenderId.Value].transform.position, 0.8f, 0.6f);
                    SetOwner(shot.DefenderId.Value);
                    yield return hud.Banner("SAVED!", $"Goal chance was {shot.GoalChance:P0}", Palette.Chalk, 1.2f, true);
                    break;
                case ShotOutcome.Goal:
                    yield return _ball.FlyTo(targetWorld + into * 0.6f, 0.8f, 0.6f);
                    _viewOwner = null;
                    break;
            }
        }

        // ------------------------------------------------------------------ view sync

        private void SyncFromState()
        {
            _viewCells.Clear();
            foreach (var p in S.Players) _viewCells[p.Id] = p.Position;
            _viewOwner = S.Ball.OwnerId;
            _viewLoose = S.Ball.LoosePosition;
            foreach (var pair in SlotPositions()) _figures[pair.Key].Place(pair.Value);
            PlaceBall();
        }

        private Dictionary<int, Vector3> SlotPositions()
        {
            var result = new Dictionary<int, Vector3>();
            foreach (var group in _viewCells.GroupBy(kv => kv.Value))
            {
                var ids = group.Select(kv => kv.Key).OrderBy(id => id).ToList();
                for (var i = 0; i < ids.Count; i++)
                    result[ids[i]] = _space.SlotPosition(group.Key, i, ids.Count);
            }
            return result;
        }

        private void SetOwner(int playerId)
        {
            _viewOwner = playerId;
            PlaceBall();
        }

        private void SetLoose(Cell cell)
        {
            _viewOwner = null;
            _viewLoose = cell;
            PlaceBall();
        }

        private void PlaceBall()
        {
            if (_viewOwner.HasValue)
            {
                var figure = _figures[_viewOwner.Value];
                // Alone: ball at the feet, toward the attack. Sharing a square: on the owner's outer side,
                // away from the other player, so it is clear who has it.
                var cell = _viewCells[_viewOwner.Value];
                var mates = _viewCells.Where(kv => kv.Value == cell).Select(kv => kv.Key).OrderBy(id => id).ToList();
                var side = mates.Count > 1
                    ? (mates.IndexOf(_viewOwner.Value) == 0 ? -1f : 1f)
                    : figure.Team.AttackDirection();
                _ball.Follow(figure.transform, new Vector3(side * (mates.Count > 1 ? 0.34f : 0.28f), 0f, -0.2f));
            }
            else
            {
                _ball.PlaceLoose(_space.CellToWorld(_viewLoose));
            }
        }

        private static string Name(Team team) => team == Team.Red ? "RED" : "BLUE";

        private static string Name(Player p) => $"{(p.Team == Team.Red ? "Red" : "Blue")} #{p.Number}";
    }
}
