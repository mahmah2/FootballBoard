using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FootballBoard.Domain.Config
{
    /// <summary>
    /// Every tunable rule of the game. Loaded from rules.json; any value missing from the
    /// file keeps the default below (the defaults match spec v1.3).
    /// </summary>
    public sealed class RulesConfig
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public BoardRules Board { get; set; } = new BoardRules();
        public TeamRules Teams { get; set; } = new TeamRules();
        public GoalkeeperRules Goalkeeper { get; set; } = new GoalkeeperRules();
        public PassingRules Passing { get; set; } = new PassingRules();
        public ShootingRules Shooting { get; set; } = new ShootingRules();
        public CarrierRules Carrier { get; set; } = new CarrierRules();
        public TackleRules Tackle { get; set; } = new TackleRules();
        public ClockRules Clock { get; set; } = new ClockRules();
        public TieRules Ties { get; set; } = new TieRules();
        public TurnRules Turns { get; set; } = new TurnRules();

        public static RulesConfig CreateDefault() => new RulesConfig();

        /// <summary>Returns every problem found; an empty list means the config is usable.</summary>
        public IReadOnlyList<string> Validate()
        {
            var e = new List<string>();
            if (SchemaVersion != CurrentSchemaVersion)
                e.Add($"schemaVersion must be {CurrentSchemaVersion} (found {SchemaVersion}).");
            if (Board == null || Teams == null || Goalkeeper == null || Passing == null ||
                Shooting == null || Carrier == null || Tackle == null || Clock == null || Ties == null || Turns == null)
            {
                e.Add("All sections (board, teams, goalkeeper, passing, shooting, carrier, tackle, clock, ties, turns) must be objects, not null.");
                return e;
            }

            Board.Validate(e);
            Teams.Validate(e, Board);
            Goalkeeper.Validate(e, Board);
            Passing.Validate(e);
            Shooting.Validate(e);
            Tackle.Validate(e);
            Clock.Validate(e);
            return e;
        }

        internal static void CheckProbability(List<string> errors, string name, double value)
        {
            if (double.IsNaN(value) || value < 0.0 || value > 1.0)
                errors.Add($"{name} must be a probability between 0 and 1 (found {value.ToString(CultureInfo.InvariantCulture)}).");
        }

        internal static void CheckRange(List<string> errors, string name, int value, int min, int max)
        {
            if (value < min || value > max)
                errors.Add($"{name} must be between {min} and {max} (found {value}).");
        }
    }

    public sealed class BoardRules
    {
        public const int MinimumRows = 13;
        public const int MinimumColumns = 26;
        public const int MaximumRows = 41;
        public const int MaximumColumns = 80;

        public int Rows { get; set; } = 13;
        public int Columns { get; set; } = 26;
        public int GoalWidth { get; set; } = 5;
        public int PenaltyBoxWidth { get; set; } = 5;
        public int PenaltyBoxDepth { get; set; } = 4;
        public int MaxPlayersPerSquare { get; set; } = 2;

        internal void Validate(List<string> e)
        {
            RulesConfig.CheckRange(e, "board.rows", Rows, MinimumRows, MaximumRows);
            RulesConfig.CheckRange(e, "board.columns", Columns, MinimumColumns, MaximumColumns);
            if (Rows % 2 == 0) e.Add("board.rows must be odd so the goal can be centred.");
            if (Columns % 2 != 0) e.Add("board.columns must be even (fields grow by 2 from 26).");
            RulesConfig.CheckRange(e, "board.goalWidth", GoalWidth, 1, Rows);
            if (GoalWidth % 2 == 0) e.Add("board.goalWidth must be odd so the goal can be centred.");
            RulesConfig.CheckRange(e, "board.penaltyBoxWidth", PenaltyBoxWidth, GoalWidth, Rows);
            if (PenaltyBoxWidth % 2 == 0) e.Add("board.penaltyBoxWidth must be odd so the box can be centred.");
            RulesConfig.CheckRange(e, "board.penaltyBoxDepth", PenaltyBoxDepth, 1, Math.Max(1, Columns / 2 - 4));
            RulesConfig.CheckRange(e, "board.maxPlayersPerSquare", MaxPlayersPerSquare, 1, 4);
        }
    }

    public sealed class TeamRules
    {
        public const int MinimumPlayers = 2;
        public const int MaximumPlayers = 11;

        /// <summary>Players per team including the goalkeeper.</summary>
        public int PlayersPerTeam { get; set; } = 5;

        /// <summary>
        /// Optional kick-off formations keyed by player count ("5", "7", ...), written for Red
        /// (column = distance from own goal line, rowOffset = rows from the centre row).
        /// The first spot is the goalkeeper. Counts without an entry use an automatic layout.
        /// </summary>
        public Dictionary<string, List<FormationSpot>> Formations { get; set; } = new Dictionary<string, List<FormationSpot>>();

        internal void Validate(List<string> e, BoardRules board)
        {
            RulesConfig.CheckRange(e, "teams.playersPerTeam", PlayersPerTeam, MinimumPlayers, MaximumPlayers);
            if (Formations == null) return;

            foreach (var pair in Formations)
            {
                var name = $"teams.formations[\"{pair.Key}\"]";
                if (!int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ||
                    count < MinimumPlayers || count > MaximumPlayers)
                {
                    e.Add($"{name}: key must be a player count between {MinimumPlayers} and {MaximumPlayers}.");
                    continue;
                }
                var spots = pair.Value;
                if (spots == null || spots.Count != count || spots.Any(s => s == null))
                {
                    e.Add($"{name} must list exactly {count} spots.");
                    continue;
                }
                var half = board.Columns / 2;
                var maxOffset = board.Rows / 2;
                var keeper = spots[0];
                if (keeper.Column >= board.PenaltyBoxDepth || Math.Abs(keeper.RowOffset) > board.PenaltyBoxWidth / 2)
                    e.Add($"{name}: the first spot is the goalkeeper and must be inside the penalty box.");
                for (var i = 0; i < spots.Count; i++)
                {
                    var s = spots[i];
                    if (s.Column < 0 || s.Column >= half || Math.Abs(s.RowOffset) > maxOffset)
                        e.Add($"{name}[{i}] must be inside the team's own half.");
                }
                var crowded = spots.GroupBy(s => (s.Column, s.RowOffset)).Any(g => g.Count() > board.MaxPlayersPerSquare);
                if (crowded) e.Add($"{name} puts more than {board.MaxPlayersPerSquare} players on one square.");
            }
        }
    }

    public sealed class FormationSpot
    {
        public int Column { get; set; }
        public int RowOffset { get; set; }

        public FormationSpot() { }

        public FormationSpot(int column, int rowOffset)
        {
            Column = column;
            RowOffset = rowOffset;
        }
    }

    public sealed class GoalkeeperRules
    {
        /// <summary>
        /// Furthest a goalkeeper may move in one turn (inside its own penalty box). The player
        /// chooses each turn how far to go, from 1 up to this value.
        /// </summary>
        public int MaxMoveCells { get; set; } = 2;

        /// <summary>Goalkeeper range while the ball is near its box (see <see cref="NearBallRings"/>).</summary>
        public int MaxMoveCellsWhenBallNear { get; set; } = 1;

        /// <summary>
        /// The ball counts as near when it is inside the keeper's penalty box or within this many rings
        /// of squares around it (1 = the squares touching the box). -1 turns the limit off.
        /// </summary>
        public int NearBallRings { get; set; } = 1;

        /// <summary>
        /// Goal probability indexed by the distance from the shot target to the goalkeeper.
        /// The last entry is used for every larger distance.
        /// </summary>
        public List<double> GoalChanceByDistance { get; set; } = new List<double> { 0.05, 0.3, 0.6, 0.8 };

        /// <summary>
        /// Furthest a goalkeeper can dive to reach a shot: an on-target shot aimed further than this
        /// many squares from the keeper is always a goal. -1 = no limit (the table decides).
        /// </summary>
        public int DiveReachCells { get; set; } = 2;

        internal void Validate(List<string> e, BoardRules board)
        {
            RulesConfig.CheckRange(e, "goalkeeper.maxMoveCells", MaxMoveCells, 1, Math.Max(1, board.PenaltyBoxWidth));
            RulesConfig.CheckRange(e, "goalkeeper.maxMoveCellsWhenBallNear", MaxMoveCellsWhenBallNear, 1, Math.Max(1, board.PenaltyBoxWidth));
            RulesConfig.CheckRange(e, "goalkeeper.nearBallRings", NearBallRings, -1, Math.Max(board.Rows, board.Columns));
            RulesConfig.CheckRange(e, "goalkeeper.diveReachCells", DiveReachCells, -1, Math.Max(board.Rows, board.Columns));

            if (GoalChanceByDistance == null || GoalChanceByDistance.Count == 0)
            {
                e.Add("goalkeeper.goalChanceByDistance must contain at least one value.");
            }
            else
            {
                foreach (var p in GoalChanceByDistance)
                    RulesConfig.CheckProbability(e, "goalkeeper.goalChanceByDistance[]", p);
            }
        }
    }

    public sealed class PassingRules
    {
        /// <summary>A cell blocks a straight pass if the lane passes within this fraction of a cell width of its centre.</summary>
        public double LaneTolerance { get; set; } = 0.05;

        /// <summary>
        /// Spec v1.3 validates the lane only when the pass is declared. Set true to also
        /// re-check it after everyone has moved (a newly blocked pass is then cancelled).
        /// </summary>
        public bool RevalidateLaneAfterMovement { get; set; } = false;

        public double LongPassSuccessChance { get; set; } = 1.0 / 9.0;

        /// <summary>Chance the receiver keeps the ball when an opponent shares the destination square.</summary>
        public double ContestedDestinationSuccessChance { get; set; } = 0.5;

        /// <summary>When false, a carrier sharing a square may pass only sideways or backwards.</summary>
        public bool AllowForwardPassFromSharedSquare { get; set; } = false;

        /// <summary>
        /// Longest straight pass, as a fraction of the pitch length (0.5 = half the field, 13 squares on
        /// a 26-column board). Anything longer has to be played as a long pass.
        /// </summary>
        public double MaxStraightPassFieldFraction { get; set; } = 0.5;

        internal void Validate(List<string> e)
        {
            if (double.IsNaN(MaxStraightPassFieldFraction) || MaxStraightPassFieldFraction <= 0 || MaxStraightPassFieldFraction > 1)
                e.Add("passing.maxStraightPassFieldFraction must be greater than 0 and at most 1.");
            if (double.IsNaN(LaneTolerance) || LaneTolerance <= 0 || LaneTolerance > 0.5)
                e.Add("passing.laneTolerance must be greater than 0 and at most 0.5.");
            RulesConfig.CheckProbability(e, "passing.longPassSuccessChance", LongPassSuccessChance);
            RulesConfig.CheckProbability(e, "passing.contestedDestinationSuccessChance", ContestedDestinationSuccessChance);
        }
    }

    public sealed class ShootingRules
    {
        /// <summary>Chance the shot is on target from inside the opponent's penalty box.</summary>
        public double AccuracyInsideBox { get; set; } = 1.0;

        /// <summary>Accuracy lost on the first ring of squares around the penalty box.</summary>
        public double AccuracyDropPerRing { get; set; } = 0.05;

        /// <summary>
        /// Each further ring drops this much more than the previous one: with 0.05 and 0.01 the drops
        /// are 5%, 6%, 7%... so accuracy reads 95%, 89%, 82%, 74%...
        /// </summary>
        public double AccuracyDropIncreasePerRing { get; set; } = 0.01;

        /// <summary>Accuracy never falls below this, however far out the shot is.</summary>
        public double MinimumAccuracy { get; set; } = 0.2;

        /// <summary>Chance the shooter wins when an opponent shares the shooter's square.</summary>
        public double ContestedShooterWinChance { get; set; } = 0.5;

        internal void Validate(List<string> e)
        {
            RulesConfig.CheckProbability(e, "shooting.accuracyInsideBox", AccuracyInsideBox);
            RulesConfig.CheckProbability(e, "shooting.accuracyDropPerRing", AccuracyDropPerRing);
            RulesConfig.CheckProbability(e, "shooting.accuracyDropIncreasePerRing", AccuracyDropIncreasePerRing);
            RulesConfig.CheckProbability(e, "shooting.minimumAccuracy", MinimumAccuracy);
            RulesConfig.CheckProbability(e, "shooting.contestedShooterWinChance", ContestedShooterWinChance);
            if (MinimumAccuracy > AccuracyInsideBox)
                e.Add("shooting.minimumAccuracy must not be greater than shooting.accuracyInsideBox.");
        }
    }

    public sealed class TurnRules
    {
        /// <summary>
        /// True: the team holding the ball takes each turn (plans first and may pass or shoot), so
        /// the order is B R, B R... until the other team actually owns the ball. While the ball is loose
        /// the turn stays with the team that had it. False: teams simply alternate turns (spec v1.3).
        /// </summary>
        public bool PossessionTeamPlaysFirst { get; set; } = true;

        /// <summary>
        /// True: the declared pass or shot resolves first, on the board both teams planned against,
        /// and then every player moves (so defenders cannot step into this turn's pass). False: spec
        /// v1.3 order, movement first and the action against the final positions.
        /// </summary>
        public bool ActionBeforeMovement { get; set; } = true;
    }

    public sealed class CarrierRules
    {
        /// <summary>
        /// The ball carrier may move one cell with the ball during the other team's turn too
        /// (it still cannot pass or shoot then). On its own turn it moves via the Move option.
        /// </summary>
        public bool CanMoveOnOpponentTurn { get; set; } = true;
    }

    public sealed class TackleRules
    {
        public bool Enabled { get; set; } = true;
        public double SuccessChance { get; set; } = 0.5;

        internal void Validate(List<string> e)
        {
            RulesConfig.CheckProbability(e, "tackle.successChance", SuccessChance);
        }
    }

    public sealed class ClockRules
    {
        /// <summary>Match seconds consumed by each step. A team turn is two steps (arrangement + action).</summary>
        public int SecondsPerStep { get; set; } = 30;
        public int DefaultMinutes { get; set; } = 10;
        public List<int> PresetMinutes { get; set; } = new List<int> { 10, 20, 30, 40, 50, 60, 70, 90 };

        internal void Validate(List<string> e)
        {
            RulesConfig.CheckRange(e, "clock.secondsPerStep", SecondsPerStep, 1, 600);
            if (PresetMinutes == null || PresetMinutes.Count == 0)
            {
                e.Add("clock.presetMinutes must contain at least one value.");
                return;
            }
            foreach (var m in PresetMinutes) RulesConfig.CheckRange(e, "clock.presetMinutes[]", m, 1, 240);
            if (!PresetMinutes.Contains(DefaultMinutes)) e.Add("clock.defaultMinutes must be one of clock.presetMinutes.");
        }
    }

    public sealed class TieRules
    {
        /// <summary>When true, a draw at full time asks both teams: draw or golden goal.</summary>
        public bool OfferGoldenGoal { get; set; } = true;

        /// <summary>True: golden goal only if both teams choose it. False: either team can force it.</summary>
        public bool GoldenGoalRequiresBothTeams { get; set; } = true;
    }

    public sealed class RulesConfigException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public RulesConfigException(IReadOnlyList<string> errors)
            : base("Invalid rules configuration:\n - " + string.Join("\n - ", errors))
        {
            Errors = errors;
        }
    }
}
