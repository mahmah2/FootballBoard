using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>
    /// The "ready to be selected" marker: a flat donut swept like a radar screen. The sweep rotates;
    /// at its leading edge the donut is fully opaque, then it fades evenly to fully transparent over
    /// one full turn. A very faint base ring keeps the donut's shape readable.
    /// </summary>
    public sealed class RadarRing : MonoBehaviour
    {
        private const float Inner = 0.2f, Outer = 0.265f;  // figure units, same donut as before
        private const float Speed = 170f;                  // degrees per second, clockwise from above
        private const int Segments = 72;

        private static readonly Color Tint = new Color(0.9f, 0.94f, 0.97f);
        private static Material _material;
        private static Mesh _sweepMesh, _baseMesh;

        private Transform _rotor;

        public static RadarRing Create(Transform parent, float y)
        {
            var go = new GameObject("Ready");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            var radar = go.AddComponent<RadarRing>();

            Part(go.transform, "Base", BaseMesh(), 0f);
            radar._rotor = new GameObject("Rotor").transform;
            radar._rotor.SetParent(go.transform, false);
            Part(radar._rotor, "Sweep", SweepMesh(), 0.001f);
            go.SetActive(false);
            return radar;
        }

        private void Update() => _rotor.localRotation = Quaternion.Euler(0, Time.time * Speed, 0);

        private static void Part(Transform parent, string name, Mesh mesh, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Material();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // Unlit, transparent and driven by vertex colour (always included in builds).
        private static Material Material() =>
            _material != null ? _material : _material = new Material(Shader.Find("Sprites/Default")) { name = "Radar" };

        /// <summary>
        /// The sweep. Angles grow anticlockwise (seen from above) while the rotor turns clockwise, so
        /// angle 0 is the leading edge (alpha 1) and 360 is a full turn behind it (alpha 0).
        /// </summary>
        private static Mesh SweepMesh() => _sweepMesh != null ? _sweepMesh
            : _sweepMesh = Donut(Inner, Outer, 0f, 360f, Segments, a => Mathf.Pow(1f - a / 360f, 1.6f) * 0.95f, Tint);

        private static Mesh BaseMesh() => _baseMesh != null ? _baseMesh
            : _baseMesh = Donut(Inner, Outer, 0f, 360f, Segments, a => 0.1f, Tint);

        private static Mesh Donut(float inner, float outer, float start, float sweep, int segments, System.Func<float, float> alphaAt, Color color)
        {
            var verts = new Vector3[(segments + 1) * 2];
            var colors = new Color[verts.Length];
            for (var i = 0; i <= segments; i++)
            {
                var offset = sweep * i / segments;
                var a = (start + offset) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                verts[i * 2] = dir * inner;
                verts[i * 2 + 1] = dir * outer;
                var c = color;
                c.a = alphaAt(offset);
                colors[i * 2] = colors[i * 2 + 1] = c;
            }
            var tris = new int[segments * 12];
            for (var i = 0; i < segments; i++)
            {
                int v = i * 2, t = i * 12;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 3;
                tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
                tris[t + 6] = v; tris[t + 7] = v + 3; tris[t + 8] = v + 1;   // back faces
                tris[t + 9] = v; tris[t + 10] = v + 2; tris[t + 11] = v + 3;
            }
            var mesh = new Mesh { name = "RadarDonut", vertices = verts, colors = colors, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
