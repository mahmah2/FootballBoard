using FootballBoard.Domain;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>
    /// Maps logical cells to world positions. Cell (0,0) is Red's goal line, top row.
    /// World X grows toward Blue's goal, world Z grows toward row 0. One cell = 1 unit.
    /// Visual offsets never feed back into rules.
    /// </summary>
    public sealed class BoardSpace
    {
        public const float CellSize = 1f;
        public const float SlotOffset = 0.25f;

        public int Rows { get; }
        public int Columns { get; }
        public float Width => Columns * CellSize;
        public float Depth => Rows * CellSize;

        public BoardSpace(int rows, int columns)
        {
            Rows = rows;
            Columns = columns;
        }

        public Vector3 CellToWorld(Cell cell) => CellToWorld(cell.X, cell.Y);

        public Vector3 CellToWorld(float x, float y) =>
            new Vector3((x - (Columns - 1) * 0.5f) * CellSize, 0f, ((Rows - 1) * 0.5f - y) * CellSize);

        public Cell WorldToCell(Vector3 world) =>
            new Cell(Mathf.RoundToInt(world.x / CellSize + (Columns - 1) * 0.5f),
                     Mathf.RoundToInt((Rows - 1) * 0.5f - world.z / CellSize));

        /// <summary>Side-by-side placement when two players share a square (slot 0 left, slot 1 right).</summary>
        public Vector3 SlotPosition(Cell cell, int slot, int occupants)
        {
            var centre = CellToWorld(cell);
            if (occupants < 2) return centre;
            return centre + new Vector3(slot == 0 ? -SlotOffset : SlotOffset, 0f, 0f);
        }
    }
}
