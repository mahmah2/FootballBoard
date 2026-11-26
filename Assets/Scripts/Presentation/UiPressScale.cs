using UnityEngine;
using UnityEngine.EventSystems;

namespace FootballBoard.Presentation
{
    /// <summary>Swells slightly under the pointer and squashes when pressed (unscaled time).</summary>
    public sealed class UiPressScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private bool _hover, _pressed;
        private float _scale = 1f;

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) { _hover = false; _pressed = false; }
        public void OnPointerDown(PointerEventData e) => _pressed = true;
        public void OnPointerUp(PointerEventData e) => _pressed = false;

        private void OnDisable() { _hover = _pressed = false; _scale = 1f; transform.localScale = Vector3.one; }

        private void Update()
        {
            var target = _pressed ? 0.95f : _hover ? 1.03f : 1f;
            _scale = Mathf.Lerp(_scale, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            transform.localScale = Vector3.one * _scale;
        }
    }
}
