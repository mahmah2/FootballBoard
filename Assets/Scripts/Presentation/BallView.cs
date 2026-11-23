using System.Collections;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>The ball. Rides at its carrier's feet, or sits on a cell when loose, or flies between.</summary>
    public sealed class BallView : MonoBehaviour
    {
        public const float Radius = 0.15f;
        private Transform _follow;
        private Vector3 _offset;

        public bool IsFlying { get; private set; }

        public static BallView Create(MaterialKit kit, Transform parent)
        {
            var go = new GameObject("Ball");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BallView>();
            kit.Primitive(PrimitiveType.Sphere, "Leather", go.transform, Palette.Chalk, Vector3.zero, Vector3.one * Radius * 2f);
            kit.Primitive(PrimitiveType.Sphere, "Patch", go.transform, Palette.Shadow, new Vector3(0, Radius * 0.55f, -Radius * 0.55f), Vector3.one * Radius * 0.9f);
            kit.Primitive(PrimitiveType.Cylinder, "Glow", go.transform, Palette.WithAlpha(Palette.Accent, 0.45f), new Vector3(0, -Radius + 0.006f, 0), new Vector3(0.34f, 0.004f, 0.34f), true);
            return view;
        }

        public void Follow(Transform carrier, Vector3 offset)
        {
            _follow = carrier;
            _offset = offset;
        }

        public void PlaceLoose(Vector3 world)
        {
            _follow = null;
            transform.position = world + Vector3.up * Radius;
        }

        public IEnumerator FlyTo(Vector3 world, float duration, float arc)
        {
            _follow = null;
            IsFlying = true;
            var start = transform.position;
            var end = world + Vector3.up * Radius;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = t / duration;
                transform.position = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
                transform.Rotate(Vector3.right, 720f * Time.deltaTime, Space.World);
                yield return null;
            }
            transform.position = end;
            IsFlying = false;
        }

        private void LateUpdate()
        {
            if (!IsFlying && _follow != null)
                transform.position = _follow.position + _offset + Vector3.up * Radius;
        }
    }
}
