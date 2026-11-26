using System.Collections.Generic;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>
    /// The selected player's marker: layers of flat arcs in shades of the team colour, each layer
    /// spinning at its own speed and direction around the figure's feet, over a faint glow ring.
    /// </summary>
    public sealed class SelectionHalo : MonoBehaviour
    {
        // An arc: radii in figure units, start/sweep in degrees, shade (0 dark .. 1 light), alpha.
        private struct Arc
        {
            public float Inner, Outer, Start, Sweep, Shade, Alpha;
            public Arc(float inner, float outer, float start, float sweep, float shade, float alpha)
            { Inner = inner; Outer = outer; Start = start; Sweep = sweep; Shade = shade; Alpha = alpha; }
        }

        // Each layer spins as one; speeds in degrees per second (negative = clockwise from above).
        private static readonly (float speed, Arc[] arcs)[] Layers =
        {
            (95f, new[] { new Arc(0.19f, 0.212f, 0, 250, 1f, 0.95f), new Arc(0.19f, 0.212f, 275, 45, 0.8f, 0.7f) }),
            (-55f, new[] { new Arc(0.222f, 0.268f, 20, 75, 0.55f, 0.75f), new Arc(0.222f, 0.268f, 200, 45, 0.3f, 0.6f),
                           new Arc(0.222f, 0.268f, 120, 22, 0.85f, 0.8f) }),
            (140f, new[] { new Arc(0.278f, 0.288f, 0, 110, 1f, 0.9f), new Arc(0.278f, 0.288f, 150, 35, 0.9f, 0.75f),
                           new Arc(0.278f, 0.288f, 230, 70, 0.7f, 0.6f) }),
            (-32f, new[] { new Arc(0.298f, 0.335f, 90, 55, 0.2f, 0.45f), new Arc(0.345f, 0.352f, 210, 130, 0.95f, 0.65f),
                           new Arc(0.345f, 0.352f, 20, 35, 0.75f, 0.5f) }),
        };

        private readonly List<(Transform layer, float speed)> _layers = new List<(Transform, float)>();

        public static SelectionHalo Create(Transform parent, MaterialKit kit, Color team, float y)
        {
            var go = new GameObject("SelectionHalo");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            var halo = go.AddComponent<SelectionHalo>();

            // Soft glow under everything.
            RingMeshes.Add(go.transform, "Glow", kit, WithAlpha(team, 0.14f), RingMeshes.Get(0.17f, 0.37f, 0, 360), -0.002f);

            foreach (var (speed, arcs) in Layers)
            {
                var layer = new GameObject("Layer").transform;
                layer.SetParent(go.transform, false);
                foreach (var a in arcs)
                {
                    var shade = a.Shade >= 0.5f
                        ? Color.Lerp(team, Color.white, (a.Shade - 0.5f) * 1.1f)
                        : Color.Lerp(team * 0.45f, team, a.Shade * 2f);
                    RingMeshes.Add(layer, "Arc", kit, WithAlpha(shade, a.Alpha), RingMeshes.Get(a.Inner, a.Outer, a.Start, a.Sweep), 0f);
                }
                halo._layers.Add((layer, speed));
            }
            go.SetActive(false);
            return halo;
        }

        private void Update()
        {
            foreach (var (layer, speed) in _layers)
                layer.localRotation = Quaternion.Euler(0, Time.time * speed, 0);
        }

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }

    /// <summary>Flat ring / arc meshes in the XZ plane (visible from both sides), cached by shape.</summary>
    internal static class RingMeshes
    {
        private static readonly Dictionary<(float, float, float, float), Mesh> Cache = new Dictionary<(float, float, float, float), Mesh>();

        public static Mesh Get(float inner, float outer, float startDeg, float sweepDeg)
        {
            var key = (inner, outer, startDeg, sweepDeg);
            if (Cache.TryGetValue(key, out var mesh)) return mesh;

            var segments = Mathf.Max(3, Mathf.CeilToInt(sweepDeg / 6f));
            var verts = new Vector3[(segments + 1) * 2];
            var normals = new Vector3[verts.Length];
            for (var i = 0; i <= segments; i++)
            {
                var a = (startDeg + sweepDeg * i / segments) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                verts[i * 2] = dir * inner;
                verts[i * 2 + 1] = dir * outer;
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
            }
            var tris = new List<int>(segments * 12);
            for (var i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.AddRange(new[] { a, b, d, a, d, c });   // one side
                tris.AddRange(new[] { a, d, b, a, c, d });   // and the other
            }
            mesh = new Mesh { name = $"Ring {inner:F2}-{outer:F2} {startDeg:F0}+{sweepDeg:F0}", vertices = verts, normals = normals };
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        public static GameObject Add(Transform parent, string name, MaterialKit kit, Color color, Mesh mesh, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = kit.Glass(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }
}
