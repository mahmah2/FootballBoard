using System.Collections.Generic;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>
    /// A row of flat chevrons on the pitch pointing from the ball carrier to a pass or shot target.
    /// A light sweeps along them toward the target (each chevron brightens, grows slightly and gets a
    /// soft glow), then they all fade and the sweep starts again.
    /// </summary>
    public sealed class ChevronTrail : MonoBehaviour
    {
        private const float Spacing = 0.55f;      // world units between chevrons
        private const float Size = 0.56f;         // chevron width
        private const float StartGap = 0.45f;     // keep clear of the carrier's figure
        private const float EndGap = 0.3f;        // stop just short of the target
        private const int MaxChevrons = 22;
        private const float Period = 1.25f;       // seconds per sweep
        private const float Height = 0.03f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static Mesh _mesh;

        private readonly List<(Transform chevron, Renderer body, Renderer glow)> _pool = new List<(Transform, Renderer, Renderer)>();
        private MaterialPropertyBlock _block;
        private MaterialKit _kit;
        private Color _color;
        private int _count;

        public static ChevronTrail Create(Transform parent, MaterialKit kit)
        {
            var go = new GameObject("ChevronTrail");
            go.transform.SetParent(parent, false);
            var trail = go.AddComponent<ChevronTrail>();
            trail._kit = kit;
            trail._block = new MaterialPropertyBlock();
            return trail;
        }

        public void Show(Vector3 from, Vector3 to, Color color)
        {
            gameObject.SetActive(true);
            _color = color;
            from.y = to.y = 0f;
            var delta = to - from;
            var length = delta.magnitude - StartGap - EndGap;
            var dir = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector3.forward;
            var rotation = Quaternion.LookRotation(dir, Vector3.up);

            _count = Mathf.Clamp(Mathf.FloorToInt(length / Spacing) + 1, 2, MaxChevrons);
            var step = Mathf.Max(length, 0.2f) / (_count - 1);
            for (var i = 0; i < _pool.Count; i++) _pool[i].chevron.gameObject.SetActive(i < _count);
            var material = _kit.Glass(color);
            for (var i = 0; i < _count; i++)
            {
                if (i == _pool.Count) _pool.Add(NewChevron());
                var (chevron, body, glow) = _pool[i];
                chevron.gameObject.SetActive(true);
                chevron.position = from + dir * (StartGap + step * i) + Vector3.up * Height;
                chevron.rotation = rotation;
                body.sharedMaterial = material;
                glow.sharedMaterial = material;
            }
            Animate();
        }

        public void Hide() => gameObject.SetActive(false);

        private void Update() => Animate();

        private void Animate()
        {
            // One highlight travels from the carrier to the target, then starts again.
            var p = Mathf.Repeat(Time.time / Period, 1f);
            var head = p * (_count + 1f) - 0.5f;                    // position of the lit chevron
            var dim = _color * 0.45f;
            var lit = Color.Lerp(_color, Color.white, 0.25f);
            for (var i = 0; i < _count; i++)
            {
                var (chevron, body, glow) = _pool[i];
                var k = Mathf.Clamp01(1f - Mathf.Abs(head - i));    // 0 = dim, 1 = fully lit
                var c = Color.Lerp(dim, lit, k);
                c.a = Mathf.Lerp(0.45f, 1f, k);
                _block.SetColor(BaseColor, c);
                body.SetPropertyBlock(_block);
                var g = _color;
                g.a = 0.4f * k;
                _block.SetColor(BaseColor, g);
                glow.SetPropertyBlock(_block);
                chevron.localScale = Vector3.one * Size;
            }
        }

        private (Transform, Renderer, Renderer) NewChevron()
        {
            var root = new GameObject("Chevron").transform;
            root.SetParent(transform, false);
            var body = Part(root, "Body", 1f, 0.004f);
            var glow = Part(root, "Glow", 1f, -0.002f);
            return (root, body, glow);
        }

        private static Renderer Part(Transform parent, string name, float scale, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, scale > 1f ? -0.06f : 0f);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = ChevronMesh();
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>A flat "›" shape in the XZ plane, 1 unit wide, pointing +Z, visible from both sides.</summary>
        private static Mesh ChevronMesh()
        {
            if (_mesh != null) return _mesh;
            var v = new[]
            {
                new Vector3(-0.5f, 0, -0.22f), new Vector3(0f, 0, 0.28f), new Vector3(0.5f, 0, -0.22f),
                new Vector3(0.5f, 0, -0.58f), new Vector3(0f, 0, -0.08f), new Vector3(-0.5f, 0, -0.58f),
            };
            var front = new[] { 0, 1, 4, 0, 4, 5, 1, 2, 3, 1, 3, 4 };
            var tris = new int[front.Length * 2];
            for (var i = 0; i < front.Length; i += 3)
            {
                tris[i] = front[i]; tris[i + 1] = front[i + 1]; tris[i + 2] = front[i + 2];
                tris[front.Length + i] = front[i]; tris[front.Length + i + 1] = front[i + 2]; tris[front.Length + i + 2] = front[i + 1];
            }
            _mesh = new Mesh { name = "Chevron", vertices = v, triangles = tris };
            _mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            _mesh.RecalculateBounds();
            return _mesh;
        }
    }
}
