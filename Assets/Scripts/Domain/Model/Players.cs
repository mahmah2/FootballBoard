namespace FootballBoard.Domain
{
    public sealed class Player
    {
        /// <summary>Team offset + shirt number: Red 1..n, Blue 101..100+n. Shirt 1 is the goalkeeper.</summary>
        public int Id { get; }
        public Team Team { get; }
        public int Number { get; }
        public PlayerRole Role { get; }
        public Cell Position { get; internal set; }
        public bool Active { get; internal set; } = true;

        public bool IsGoalkeeper => Role == PlayerRole.Goalkeeper;

        internal Player(Team team, int number, PlayerRole role, Cell position)
        {
            Id = IdFor(team, number);
            Team = team;
            Number = number;
            Role = role;
            Position = position;
        }

        public static int IdFor(Team team, int number) => (team == Team.Red ? 0 : 100) + number;

        public override string ToString() => $"{Team} #{Number} {Position}";
    }

    public sealed class Ball
    {
        /// <summary>The player holding the ball, or null when the ball is loose on <see cref="LoosePosition"/>.</summary>
        public int? OwnerId { get; internal set; }
        public Cell LoosePosition { get; internal set; }
        public bool IsLoose => OwnerId == null;
    }
}
