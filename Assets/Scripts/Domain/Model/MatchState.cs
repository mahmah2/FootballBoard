using System;
using System.Collections.Generic;
using System.Linq;
using FootballBoard.Domain.Config;

namespace FootballBoard.Domain
{
    public enum MatchPhase
    {
        /// <summary>Teams may place outfield players in their own half before kick-off.</summary>
        Setup,

        /// <summary>Waiting for <see cref="MatchState.PlanningTeam"/> to submit its plan.</summary>
        Planning,

        /// <summary>Full time with a draw: both teams choose draw or golden goal.</summary>
        AwaitingTieDecision,

        MatchOver
    }

    public enum TieChoice
    {
        Draw,
        GoldenGoal
    }

    public sealed class MatchResult
    {
        public int RedScore { get; }
        public int BlueScore { get; }
        public bool DecidedByGoldenGoal { get; }
        public Team? Winner => RedScore == BlueScore ? (Team?)null : RedScore > BlueScore ? Team.Red : Team.Blue;
        public bool IsDraw => Winner == null;

        internal MatchResult(int red, int blue, bool goldenGoal)
        {
            RedScore = red;
            BlueScore = blue;
            DecidedByGoldenGoal = goldenGoal;
        }
    }

    /// <summary>
    /// The full logical state of a match. Read freely; it only changes through <see cref="MatchEngine"/> commands.
    /// </summary>
    public sealed class MatchState
    {
        internal readonly List<Player> PlayerList = new List<Player>();
        private readonly int[] _scores = new int[2];

        public RulesConfig Rules { get; }
        public Pitch Pitch { get; }
        public GameMode Mode { get; }
        public Team HumanTeam { get; }
        public int PlayersPerTeam { get; }
        public int GoalkeeperMoveCells { get; }
        public int DurationSeconds { get; }

        public IReadOnlyList<Player> Players => PlayerList;
        public Ball Ball { get; } = new Ball();

        /// <summary>Team that touched the ball last (decides throw-ins).</summary>
        public Team? LastTouch { get; internal set; }

        public int ElapsedSeconds { get; internal set; }
        public int RemainingSeconds => Math.Max(0, DurationSeconds - ElapsedSeconds);

        public MatchPhase Phase { get; internal set; } = MatchPhase.Setup;
        public bool IsPaused { get; internal set; }

        /// <summary>Team whose turn it is (it may use the ball this turn).</summary>
        public Team TurnTeam { get; internal set; }

        /// <summary>Team that must submit a plan now. The turn team plans first, then the other team.</summary>
        public Team PlanningTeam { get; internal set; }

        public int TurnNumber { get; internal set; }
        public bool IsGoldenGoal { get; internal set; }
        public MatchResult Result { get; internal set; }

        internal TeamPlan PendingTurnPlan;
        internal int? PendingCarrierId;
        internal int? PendingShotBlockerId;
        internal Cell PendingBlockCell;
        internal readonly Dictionary<Team, TieChoice> TieChoices = new Dictionary<Team, TieChoice>();

        internal MatchState(RulesConfig rules, GameMode mode, Team humanTeam, int playersPerTeam, int keeperMoveCells, int durationSeconds)
        {
            Rules = rules;
            Pitch = new Pitch(rules.Board);
            Mode = mode;
            HumanTeam = humanTeam;
            PlayersPerTeam = playersPerTeam;
            GoalkeeperMoveCells = keeperMoveCells;
            DurationSeconds = durationSeconds;
        }

        public int Score(Team team) => _scores[(int)team];
        internal void AddGoal(Team team) => _scores[(int)team]++;

        public Player GetPlayer(int id) => PlayerList.FirstOrDefault(p => p.Id == id);

        public IEnumerable<Player> TeamPlayers(Team team) => PlayerList.Where(p => p.Team == team);

        public Player Goalkeeper(Team team) => PlayerList.First(p => p.Team == team && p.IsGoalkeeper);

        public Player BallCarrier => Ball.OwnerId.HasValue ? GetPlayer(Ball.OwnerId.Value) : null;

        public Team? PossessionTeam => BallCarrier?.Team;

        public Cell BallPosition => BallCarrier?.Position ?? Ball.LoosePosition;

        public IEnumerable<Player> PlayersAt(Cell cell) => PlayerList.Where(p => p.Active && p.Position == cell);

        public int OccupancyAt(Cell cell) => PlayerList.Count(p => p.Active && p.Position == cell);

        /// <summary>True when a straight pass between the two cells is not blocked by any player.</summary>
        public bool IsLaneClear(Cell from, Cell to) => LaneBlockers(from, to).Count == 0;

        public IReadOnlyList<Player> LaneBlockers(Cell from, Cell to)
        {
            var cells = Pitch.LaneCells(from, to, Rules.Passing.LaneTolerance);
            return PlayerList.Where(p => p.Active && cells.Contains(p.Position)).ToList();
        }

        internal bool ClockExpired => !IsGoldenGoal && ElapsedSeconds >= DurationSeconds;
    }
}
