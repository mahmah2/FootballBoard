using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace FootballBoard.Presentation
{
    /// <summary>
    /// One input abstraction for touch, mouse and keyboard. Taps become board taps; drags beyond a
    /// small threshold pan the camera (so a pan never selects); pinch and wheel zoom.
    /// </summary>
    public sealed class PointerInput : MonoBehaviour
    {
        [SerializeField] private CameraRig rig;

        public event Action<Vector2> Tapped;
        public event Action ConfirmPressed;
        public event Action CancelPressed;
        public event Action FitPressed;

        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private bool _pressing, _dragging, _pressOverUi, _suppressUntilRelease;
        private Vector2 _pressStart, _last, _rightLast;
        private float _lastPinch;

        private float TapThreshold => Screen.dpi > 0 ? Screen.dpi * 0.12f : 12f;

        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void Update()
        {
            ReadKeyboard();

            var touches = Touch.activeTouches;
            if (touches.Count >= 2)
            {
                Pinch(touches[0].screenPosition, touches[1].screenPosition);
                _pressing = false;
                _suppressUntilRelease = true;
                return;
            }
            _lastPinch = 0f;

            var mouse = Mouse.current;
            bool pressed;
            Vector2 position;
            if (touches.Count == 1)
            {
                pressed = true;
                position = touches[0].screenPosition;
            }
            else if (mouse != null)
            {
                pressed = mouse.leftButton.isPressed;
                position = mouse.position.ReadValue();
            }
            else
            {
                pressed = false;
                position = _last;
            }

            if (!pressed) _suppressUntilRelease = false;
            if (!_suppressUntilRelease) Press(pressed, position);

            if (mouse != null && touches.Count == 0) ReadMouseExtras(mouse);
        }

        private void Press(bool pressed, Vector2 position)
        {
            if (pressed && !_pressing)
            {
                _pressing = true;
                _dragging = false;
                _pressStart = _last = position;
                _pressOverUi = IsOverUi(position);
            }
            else if (pressed)
            {
                if (!_pressOverUi)
                {
                    if (!_dragging && (position - _pressStart).magnitude > TapThreshold) _dragging = true;
                    if (_dragging) rig.Pan(_last, position);
                }
                _last = position;
            }
            else if (_pressing)
            {
                _pressing = false;
                if (!_dragging && !_pressOverUi) Tapped?.Invoke(_last);
            }
        }

        private void ReadMouseExtras(Mouse mouse)
        {
            var position = mouse.position.ReadValue();
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUi(position))
                rig.Zoom(scroll > 0 ? 0.88f : 1.12f, position);

            if (mouse.rightButton.wasPressedThisFrame) _rightLast = position;
            else if (mouse.rightButton.isPressed)
            {
                rig.Pan(_rightLast, position);
                _rightLast = position;
            }
        }

        private void Pinch(Vector2 a, Vector2 b)
        {
            var distance = Vector2.Distance(a, b);
            var centre = (a + b) * 0.5f;
            if (_lastPinch > 0f && distance > 1f)
            {
                rig.Zoom(_lastPinch / distance, centre);
                rig.Pan(_last, centre);
            }
            _lastPinch = distance;
            _last = centre;
        }

        private void ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) ConfirmPressed?.Invoke();
            if (keyboard.escapeKey.wasPressedThisFrame) CancelPressed?.Invoke();
            if (keyboard.fKey.wasPressedThisFrame) FitPressed?.Invoke();
        }

        private bool IsOverUi(Vector2 position)
        {
            var system = EventSystem.current;
            if (system == null) return false;
            _uiHits.Clear();
            system.RaycastAll(new PointerEventData(system) { position = position }, _uiHits);
            return _uiHits.Count > 0;
        }
    }
}
