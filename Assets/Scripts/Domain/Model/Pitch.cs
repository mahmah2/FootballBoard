using System;
using System.Collections.Generic;
using FootballBoard.Domain.Config;

namespace FootballBoard.Domain
{
    /// <summary>
    /// Board geometry. Column 0 is Red's goal line and column Columns-1 is Blue's.
    /// Red attacks toward increasing X. Rows are 0-based (row 6 is the centre of a 13-row board).
    /// </summary>
    public sealed class Pitch
    {
        public int Rows { get; }
        public int Columns { get; }
        public int CentreRow { get; }
        public int GoalTop { get; }
        public int GoalBottom { get; }
        public int BoxTop { get; }
        public int BoxBottom { get; }
        public int BoxDepth { get; }
        public int MaxPlayersPerSquare { get; }

        public Pitch(BoardRules board)
        {
            Rows = board.Rows;
            Columns = board.Columns;
            CentreRow = Rows / 2;
            GoalTop = CentreRow - board.GoalWidth / 2;
            GoalBottom = CentreRow + board.GoalWidth / 2;
            BoxTop = CentreRow - board.PenaltyBoxWidth / 2;
            BoxBottom = CentreRow + board.PenaltyBoxWidth / 2;
            BoxDepth = board.PenaltyBoxDepth;
            MaxPlayersPerSquare = board.MaxPlayersPerSquare;
        }

        public bool InBounds(Cell c) => c.X >= 0 && c.X < Columns && c.Y >= 0 && c.Y < Rows;

        /// <summary>Column of the goal line defended by <paramref name="defending"/>.</summary>
        public int GoalLineX(Team defending) => defending == Team.Red ? 0 : Columns - 1;

        public Cell GoalCentre(Team defending) => new Cell(GoalLineX(defending), CentreRow);

        public int DistanceFromGoalLine(Team defending, Cell c) => Math.Abs(c.X - GoalLineX(defending));

        public bool InPenaltyBox(Team owner, Cell c) =>
            InBounds(c) && DistanceFromGoalLine(owner, c) < BoxDepth && c.Y >= BoxTop && c.Y <= BoxBottom;

        /// <summary>
        /// 0 inside the penalty box of <paramref name="owner"/>; otherwise which rectangular ring around
        /// the box the cell is on (1 = the squares touching the box, 2 = the next ring, ...).
        /// </summary>
        public int RingsOutsidePenaltyBox(Team owner, Cell c)
        {
            var lineX = GoalLineX(owner);
            var nearX = lineX;
            var farX = owner == Team.Red ? lineX + BoxDepth - 1 : lineX - (BoxDepth - 1);
            int minX = Math.Min(nearX, farX), maxX = Math.Max(nearX, farX);
            var dx = c.X < minX ? minX - c.X : c.X > maxX ? c.X - maxX : 0;
            var dy = c.Y < BoxTop ? BoxTop - c.Y : c.Y > BoxBottom ? c.Y - BoxBottom : 0;
            return Math.Max(dx, dy);
        }

        public bool InOwnHalf(Team team, Cell c) =>
            InBounds(c) && (team == Team.Red ? c.X < Columns / 2 : c.X >= Columns / 2);

        /// <summary>The kick-off cell: the centre-row cell on the kicking team's side of the centre line.</summary>
        public Cell KickoffSpot(Team kicking) => new Cell(kicking == Team.Red ? Columns / 2 - 1 : Columns / 2, CentreRow);

        /// <summary>Goal-line cell in the shooter's row, or the nearest goal cell if that row is outside the goal.</summary>
        public Cell ShotTarget(Team shooter, Cell from) =>
            new Cell(GoalLineX(shooter.Opponent()), Math.Min(Math.Max(from.Y, GoalTop), GoalBottom));

        public Cell MirrorForBlue(Cell redCell) => new Cell(Columns - 1 - redCell.X, redCell.Y);

        /// <summary>
        /// Cells strictly between <paramref name="from"/> and <paramref name="to"/> whose centre lies within
        /// <paramref name="tolerance"/> cell widths of the straight line joining the two centres.
        /// </summary>
        public IReadOnlyList<Cell> LaneCells(Cell from, Cell to, double tolerance)
        {
            var result = new List<Cell>();
            double dx = to.X - from.X, dy = to.Y - from.Y;
            var lengthSquared = dx * dx + dy * dy;
            if (lengthSquared == 0) return result;
            var length = Math.Sqrt(lengthSquared);

            for (var x = Math.Min(from.X, to.X); x <= Math.Max(from.X, to.X); x++)
            for (var y = Math.Min(from.Y, to.Y); y <= Math.Max(from.Y, to.Y); y++)
            {
                double px = x - from.X, py = y - from.Y;
                var t = (px * dx + py * dy) / lengthSquared;
                if (t <= 0 || t >= 1) continue;
                var distance = Math.Abs(dy * px - dx * py) / length;
                if (distance <= tolerance + 1e-9) result.Add(new Cell(x, y));
            }
            return result;
        }

        /// <summary>The 8 neighbouring cells in a fixed order (may be off the pitch).</summary>
        public static IReadOnlyList<Cell> Neighbours(Cell c) => new[]
        {
            c.Offset(-1, -1), c.Offset(0, -1), c.Offset(1, -1),
            c.Offset(-1, 0), c.Offset(1, 0),
            c.Offset(-1, 1), c.Offset(0, 1), c.Offset(1, 1)
        };
    }
}
