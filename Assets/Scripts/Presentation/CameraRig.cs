using Unity.Cinemachine;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>
    /// Elevated orthographic board camera. A Cinemachine camera follows a focus point with damping;
    /// this rig moves the focus (pan), changes the orthographic size (zoom) and frames the whole board (fit).
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private Transform focus;
        [SerializeField] private Camera outputCamera;
        [SerializeField] private float pitchDegrees = 58f;
        [SerializeField] private float zoomDamping = 10f;

        private BoardSpace _space;
        private float _targetSize, _fitSize, _lastAspect;

        public Camera OutputCamera => outputCamera;
        public Quaternion ViewRotation => Quaternion.Euler(pitchDegrees, 0f, 0f);

        public void Initialise(BoardSpace space)
        {
            _space = space;
            Fit(true);
            _lastAspect = outputCamera.aspect;
        }

        /// <summary>Frame the whole board, including goals and a little table.</summary>
        public void Fit(bool immediate = false)
        {
            var aspect = Mathf.Max(0.5f, outputCamera.aspect);
            var width = _space.Width + 3f;
            var depth = (_space.Depth + 1.5f) * Mathf.Sin(pitchDegrees * Mathf.Deg2Rad) + 1f;
            _fitSize = Mathf.Max(depth * 0.5f, width * 0.5f / aspect);
            _targetSize = _fitSize;
            focus.position = new Vector3(0f, 0f, -0.3f);
            if (immediate) SetLensSize(_fitSize);
        }

        public void Zoom(float factor, Vector2 screenAnchor)
        {
            var before = ScreenToBoard(screenAnchor);
            _targetSize = Mathf.Clamp(_targetSize * factor, _fitSize * 0.3f, _fitSize * 1.15f);
            // Keep the point under the cursor/fingers roughly in place.
            if (before.HasValue)
            {
                var pull = 1f - factor;
                focus.position += (before.Value - focus.position) * pull;
                ClampFocus();
            }
        }

        public void Pan(Vector2 fromScreen, Vector2 toScreen)
        {
            var a = ScreenToBoard(fromScreen);
            var b = ScreenToBoard(toScreen);
            if (!a.HasValue || !b.HasValue) return;
            focus.position += a.Value - b.Value;
            ClampFocus();
        }

        /// <summary>Intersects the screen ray with the pitch plane (y = 0).</summary>
        public Vector3? ScreenToBoard(Vector2 screen)
        {
            var ray = outputCamera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            return plane.Raycast(ray, out var distance) ? ray.GetPoint(distance) : (Vector3?)null;
        }

        private void ClampFocus()
        {
            if (_space == null) return; // no board yet (main menu): nothing to clamp to
            var p = focus.position;
            p.x = Mathf.Clamp(p.x, -_space.Width * 0.5f, _space.Width * 0.5f);
            p.z = Mathf.Clamp(p.z, -_space.Depth * 0.5f, _space.Depth * 0.5f);
            p.y = 0f;
            focus.position = p;
        }

        private void LateUpdate()
        {
            if (_space == null) return;
            if (!Mathf.Approximately(_lastAspect, outputCamera.aspect))
            {
                // Window resized or device rotated: keep the same relative zoom.
                var ratio = _fitSize > 0 ? _targetSize / _fitSize : 1f;
                var focusPos = focus.position;
                Fit();
                _targetSize = _fitSize * ratio;
                focus.position = focusPos;
                _lastAspect = outputCamera.aspect;
            }
            var size = virtualCamera.Lens.OrthographicSize;
            if (Mathf.Abs(size - _targetSize) > 0.001f)
                SetLensSize(Mathf.Lerp(size, _targetSize, 1f - Mathf.Exp(-zoomDamping * Time.deltaTime)));
        }

        private void SetLensSize(float size)
        {
            var lens = virtualCamera.Lens;
            lens.OrthographicSize = size;
            virtualCamera.Lens = lens;
        }
    }
}
