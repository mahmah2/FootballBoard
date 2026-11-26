using System.Collections.Generic;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>
    /// A planned move: a row of small glowing dots from the player to the destination, with a soft
    /// wave of light flowing along them toward the destination (dots swell and brighten as it passes).
    /// </summary>
    public sealed class MoveTrail : MonoBehaviour
    {
        private const float Spacing = 0.2f;      // world units between dots
        private const float DotRadius = 0.06f;
        private const float StartGap = 0.32f;    // clear of the figure's feet
        private const float EndGap = 0.36f;      // stop at the destination ring
        private const float Height = 0.035f;
        private const float WaveSpeed = 1.6f;    // waves per second
        private const float WaveLength = 5f;     // dots per wave

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly List<Renderer> _dots = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private MaterialKit _kit;
        private Color _color;
        private int _count;

        public static MoveTrail Create(Transform parent, MaterialKit kit)
        {
            var go = new GameObject("MoveTrail");
            go.transform.SetParent(parent, false);
            var trail = go.AddComponent<MoveTrail>();
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
            var length = Mathf.Max(0f, delta.magnitude - StartGap - EndGap);
            var dir = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector3.forward;
            _count = Mathf.Max(1, Mathf.FloorToInt(length / Spacing) + 1);
            var step = _count > 1 ? length / (_count - 1) : 0f;
            var material = _kit.Glass(color);
            for (var i = 0; i < _dots.Count; i++) _dots[i].gameObject.SetActive(i < _count);
            for (var i = 0; i < _count; i++)
            {
                if (i == _dots.Count)
                    _dots.Add(RingMeshes.Add(transform, "Dot", _kit, color, RingMeshes.Get(0f, DotRadius, 0, 360), 0f).GetComponent<Renderer>());
                var dot = _dots[i];
                dot.gameObject.SetActive(true);
                dot.sharedMaterial = material;
                dot.transform.position = from + dir * (StartGap + step * i) + Vector3.up * Height;
            }
            Animate();
        }

        public void Hide() => gameObject.SetActive(false);

        private void Update() => Animate();

        private void Animate()
        {
            var t = Time.time * WaveSpeed;
            for (var i = 0; i < _count; i++)
            {
                // A cosine wave travelling from the player (i = 0) toward the destination.
                var k = 0.5f + 0.5f * Mathf.Cos(2 * Mathf.PI * (t - i / WaveLength));
                k = k * k;
                var c = Color.Lerp(_color * 0.8f, Color.Lerp(_color, Color.white, 0.35f), k);
                c.a = Mathf.Lerp(0.35f, 1f, k);
                _block.SetColor(BaseColor, c);
                _dots[i].SetPropertyBlock(_block);
                _dots[i].transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.25f, k);
            }
        }
    }
}
