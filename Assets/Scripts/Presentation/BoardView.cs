using System.Collections.Generic;
using FootballBoard.Domain;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>Builds the tabletop pitch from primitives and draws cell highlights and plan lines.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float LineWidth = 0.07f;
        private const float LineY = 0.003f;

        private BoardSpace _space;
        private MaterialKit _kit;
        private Transform _overlay;
        private readonly List<GameObject> _cellPool = new List<GameObject>();
        private readonly List<LineRenderer> _linePool = new List<LineRenderer>();
        private int _cellsUsed, _linesUsed;

        public void Build(Pitch pitch, BoardSpace space, MaterialKit kit)
        {
            _space = space;
            _kit = kit;
            float w = space.Width, d = space.Depth;

            // Table and rim.
            kit.Primitive(PrimitiveType.Cube, "TableBase", transform, Palette.TableEdge, new Vector3(0, -0.75f, 0), new Vector3(w + 4.8f, 0.5f, d + 3.8f));
            kit.Primitive(PrimitiveType.Cube, "Table", transform, Palette.Table, new Vector3(0, -0.3f, 0), new Vector3(w + 4f, 0.5f, d + 3f));

            // Turf stripes, one per column.
            var turf = new GameObject("Turf").transform;
            turf.SetParent(transform, false);
            for (var x = 0; x < space.Columns; x++)
            {
                var pos = space.CellToWorld(x, (space.Rows - 1) * 0.5f) + Vector3.down * 0.05f;
                kit.Primitive(PrimitiveType.Cube, "Stripe" + x, turf, x % 2 == 0 ? Palette.Turf : Palette.TurfStripe, pos, new Vector3(1f, 0.1f, d));
            }

            // Subtle grid.
            var grid = new GameObject("Grid").transform;
            grid.SetParent(transform, false);
            for (var x = 1; x < space.Columns; x++)
                Line(grid, Palette.GridLine, space.CellToWorld(x - 0.5f, -0.5f), space.CellToWorld(x - 0.5f, space.Rows - 0.5f), 0.03f, 0.001f);
            for (var y = 1; y < space.Rows; y++)
                Line(grid, Palette.GridLine, space.CellToWorld(-0.5f, y - 0.5f), space.CellToWorld(space.Columns - 0.5f, y - 0.5f), 0.03f, 0.001f);

            // Chalk markings.
            var chalk = new GameObject("Markings").transform;
            chalk.SetParent(transform, false);
            float left = -0.5f, right = space.Columns - 0.5f, top = -0.5f, bottom = space.Rows - 0.5f;
            Line(chalk, Palette.Chalk, space.CellToWorld(left, top), space.CellToWorld(right, top));
            Line(chalk, Palette.Chalk, space.CellToWorld(left, bottom), space.CellToWorld(right, bottom));
            Line(chalk, Palette.Chalk, space.CellToWorld(left, top), space.CellToWorld(left, bottom));
            Line(chalk, Palette.Chalk, space.CellToWorld(right, top), space.CellToWorld(right, bottom));
            var mid = space.Columns * 0.5f - 0.5f;
            Line(chalk, Palette.Chalk, space.CellToWorld(mid, top), space.CellToWorld(mid, bottom));
            Circle(chalk, space.CellToWorld(mid, (space.Rows - 1) * 0.5f), 2f);

            foreach (var team in new[] { Team.Red, Team.Blue })
            {
                BuildPenaltyBox(chalk, pitch, team);
                BuildGoal(pitch, team);
            }

            _overlay = new GameObject("Overlay").transform;
            _overlay.SetParent(transform, false);
        }

        private void BuildPenaltyBox(Transform parent, Pitch pitch, Team team)
        {
            float lineX = team == Team.Red ? -0.5f : _space.Columns - 0.5f;
            float frontX = team == Team.Red ? pitch.BoxDepth - 0.5f : _space.Columns - pitch.BoxDepth - 0.5f;
            float top = pitch.BoxTop - 0.5f, bottom = pitch.BoxBottom + 0.5f;
            Line(parent, Palette.Chalk, _space.CellToWorld(lineX, top), _space.CellToWorld(frontX, top));
            Line(parent, Palette.Chalk, _space.CellToWorld(lineX, bottom), _space.CellToWorld(frontX, bottom));
            Line(parent, Palette.Chalk, _space.CellToWorld(frontX, top), _space.CellToWorld(frontX, bottom));
        }

        private void BuildGoal(Pitch pitch, Team team)
        {
            var root = new GameObject(team + "Goal").transform;
            root.SetParent(transform, false);
            float sign = team == Team.Red ? -1f : 1f;
            var lineX = team == Team.Red ? -0.5f : _space.Columns - 0.5f;
            var top = _space.CellToWorld(lineX, pitch.GoalTop - 0.5f);
            var bottom = _space.CellToWorld(lineX, pitch.GoalBottom + 0.5f);
            const float height = 0.9f, depth = 0.8f, post = 0.09f;
            var width = Mathf.Abs(top.z - bottom.z);
            var centre = (top + bottom) * 0.5f;

            _kit.Primitive(PrimitiveType.Cylinder, "PostA", root, Palette.Chalk, top + Vector3.up * height * 0.5f, new Vector3(post, height * 0.5f, post));
            _kit.Primitive(PrimitiveType.Cylinder, "PostB", root, Palette.Chalk, bottom + Vector3.up * height * 0.5f, new Vector3(post, height * 0.5f, post));
            _kit.Primitive(PrimitiveType.Cube, "Crossbar", root, Palette.Chalk, centre + Vector3.up * height, new Vector3(post, post, width + post));
            var net = Palette.WithAlpha(Palette.Chalk, 0.28f);
            _kit.Primitive(PrimitiveType.Cube, "NetBack", root, net, centre + new Vector3(sign * depth, height * 0.5f, 0), new Vector3(0.02f, height, width), true);
            _kit.Primitive(PrimitiveType.Cube, "NetTop", root, net, centre + new Vector3(sign * depth * 0.5f, height, 0), new Vector3(depth, 0.02f, width), true);
            _kit.Primitive(PrimitiveType.Cube, "NetSideA", root, net, top + new Vector3(sign * depth * 0.5f, height * 0.5f, 0), new Vector3(depth, height, 0.02f), true);
            _kit.Primitive(PrimitiveType.Cube, "NetSideB", root, net, bottom + new Vector3(sign * depth * 0.5f, height * 0.5f, 0), new Vector3(depth, height, 0.02f), true);
        }

        // ------------------------------------------------------------------ overlay API

        public void ClearOverlay()
        {
            foreach (var go in _cellPool) go.SetActive(false);
            foreach (var lr in _linePool) lr.gameObject.SetActive(false);
            _cellsUsed = _linesUsed = 0;
        }

        public void HighlightCell(Cell cell, Color color, float size = 0.86f)
        {
            if (_cellsUsed == _cellPool.Count)
                _cellPool.Add(_kit.Primitive(PrimitiveType.Cube, "CellHighlight", _overlay, color, Vector3.zero, Vector3.one, true));
            var go = _cellPool[_cellsUsed++];
            go.SetActive(true);
            go.transform.position = _space.CellToWorld(cell) + Vector3.up * 0.012f;
            go.transform.localScale = new Vector3(size, 0.01f, size);
            go.GetComponent<Renderer>().sharedMaterial = _kit.Glass(color);
        }

        public void DrawArrow(Vector3 from, Vector3 to, Color color, float width = 0.08f)
        {
            if (_linesUsed == _linePool.Count) _linePool.Add(NewLine(_overlay, "PlanLine"));
            var lr = _linePool[_linesUsed++];
            lr.gameObject.SetActive(true);
            lr.sharedMaterial = _kit.Glass(color);
            lr.startWidth = width;
            lr.endWidth = width * 0.35f;
            lr.positionCount = 2;
            lr.SetPosition(0, from + Vector3.up * 0.05f);
            lr.SetPosition(1, to + Vector3.up * 0.05f);
        }

        // ------------------------------------------------------------------ helpers

        private void Line(Transform parent, Color color, Vector3 a, Vector3 b, float width = LineWidth, float y = LineY)
        {
            var mid = (a + b) * 0.5f + Vector3.up * y;
            var length = Vector3.Distance(a, b) + width;
            var alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            _kit.Primitive(PrimitiveType.Cube, "Line", parent, color, mid, alongX ? new Vector3(length, 0.004f, width) : new Vector3(width, 0.004f, length));
        }

        private void Circle(Transform parent, Vector3 centre, float radius)
        {
            var lr = NewLine(parent, "CentreCircle");
            lr.sharedMaterial = _kit.Glass(Palette.Chalk);
            lr.startWidth = lr.endWidth = LineWidth;
            lr.loop = true;
            const int segments = 64;
            lr.positionCount = segments;
            for (var i = 0; i < segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                lr.SetPosition(i, centre + new Vector3(Mathf.Cos(a) * radius, LineY, Mathf.Sin(a) * radius));
            }
        }

        private static LineRenderer NewLine(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.TransformZ;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            return lr;
        }
    }
}
