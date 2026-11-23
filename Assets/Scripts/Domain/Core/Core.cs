using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FootballBoard.Domain.Tests")]

namespace FootballBoard.Domain
{
    public enum Team
    {
        Red = 0,
        Blue = 1
    }

    public static class TeamExtensions
    {
        public static Team Opponent(this Team team) => team == Team.Red ? Team.Blue : Team.Red;

        /// <summary>+1 for Red (attacks toward increasing X), -1 for Blue.</summary>
        public static int AttackDirection(this Team team) => team == Team.Red ? 1 : -1;
    }

    public enum PlayerRole
    {
        Goalkeeper,
        Outfield
    }

    public enum GameMode
    {
        HumanVsHuman,
        HumanVsComputer
    }

    /// <summary>
    /// Logical grid cell. X is the column (0 = Red's goal line), Y is the row (0 = top).
    /// </summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Chebyshev distance: the larger of the row and column differences.</summary>
        public static int Distance(Cell a, Cell b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        public int DistanceTo(Cell other) => Distance(this, other);

        public Cell Offset(int dx, int dy) => new Cell(X + dx, Y + dy);

        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
        public override string ToString() => "(" + X + "," + Y + ")";
    }
}
