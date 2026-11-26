using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FootballBoard.Presentation
{
    public enum ButtonStyle { Blue, Green, Red, Gold, Slate }

    /// <summary>
    /// A chunky game button: drop shadow, dark rounded outline, coloured fill, a light shine band on
    /// the upper half and bold italic text with a two-colour gradient. It swells and brightens under
    /// the pointer and squashes, darkens and sinks onto its shadow when pressed. Uses unscaled time,
    /// so it animates in the pause menu too.
    /// </summary>
    public sealed class AnimatedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private static readonly Vector2 ShadowOffset = new Vector2(5, -6);
        private static Sprite _rounded;

        private RectTransform _root, _shadow, _face;
        private Image _fill, _outline, _shine;
        private TextMeshProUGUI _label;
        private UiTheme.ButtonColors _colors;
        private bool _hover, _pressed;
        private float _scale = 1f, _press;

        /// <summary>The container that scales with the animation (put icons here).</summary>
        public RectTransform Root => _root;

        public static AnimatedButton Build(Button button, string text, float fontSize, ButtonStyle style)
        {
            var go = button.gameObject;
            var a = go.AddComponent<AnimatedButton>();
            a._root = UiFactory.Stretch("Root", go.transform);

            a._shadow = UiFactory.Stretch("Shadow", a._root);
            Rounded(a._shadow, new Color(0, 0, 0, 0.45f), 1.2f);
            a._face = UiFactory.Stretch("Face", a._root);
            var outline = UiFactory.Stretch("Outline", a._face);
            a._outline = Rounded(outline, Color.black, 1.2f);
            var fill = UiFactory.Stretch("BG", a._face);
            fill.offsetMin = new Vector2(5, 5);
            fill.offsetMax = new Vector2(-5, -5);
            a._fill = Rounded(fill, Color.white, 1.45f);
            var shine = UiFactory.Rect("Highlight", a._face, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            shine.offsetMin = new Vector2(11, 0);
            shine.offsetMax = new Vector2(-11, -9);
            a._shine = Rounded(shine, Color.white, 2.2f);

            var theme = UiTheme.Current;
            a._label = UiFactory.Label(a._face, text, fontSize * theme.displaySizeScale, Color.white, TextAlignmentOptions.Center, theme.displayFont);
            a._label.fontStyle = FontStyles.Normal;
            a._label.margin = new Vector4(14, 4, 14, 6);
            a._label.enableVertexGradient = true;
            a._label.outlineWidth = 0.22f;

            a.SetStyle(style);
            a.Apply();
            return a;
        }

        public void SetStyle(ButtonStyle style)
        {
            _colors = UiTheme.Current.Colors(style);
            _outline.color = _colors.outline;
            _shine.color = _colors.shine;
            _label.colorGradient = new VertexGradient(_colors.textTop, _colors.textTop, _colors.textBottom, _colors.textBottom);
            _label.outlineColor = _colors.outline;
            _label.color = Color.white;
            Apply();
        }

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) { _hover = false; _pressed = false; }
        public void OnPointerDown(PointerEventData e) => _pressed = true;
        public void OnPointerUp(PointerEventData e) => _pressed = false;

        private void OnDisable() { _hover = _pressed = false; _scale = 1f; _press = 0f; Apply(); }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var targetScale = _pressed ? 0.94f : _hover ? 1.05f : 1f;
            _scale = Mathf.Lerp(_scale, targetScale, 1f - Mathf.Exp(-dt * 18f));
            _press = Mathf.MoveTowards(_press, _pressed ? 1f : 0f, dt * 12f);
            Apply();
        }

        private void Apply()
        {
            if (_root == null) return;
            _root.localScale = Vector3.one * _scale;
            // Pressed: the face sinks toward its shadow. Hover: a touch brighter.
            var sink = ShadowOffset * 0.6f * _press;
            _shadow.offsetMin = ShadowOffset - sink;
            _shadow.offsetMax = ShadowOffset - sink;
            _face.anchoredPosition = sink;
            var fill = _colors.fill;
            if (_hover && !_pressed) fill = Color.Lerp(fill, Color.white, 0.12f);
            fill = Color.Lerp(fill, fill * 0.78f, _press);
            fill.a = 1f;
            _fill.color = fill;
        }

        private static Image Rounded(RectTransform rt, Color color, float cornerTightness)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = RoundedSprite();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = cornerTightness;   // larger = smaller corner radius
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A 64-pixel white rounded square, 9-sliced so it stretches to any button size.</summary>
        public static Sprite RoundedSprite()
        {
            if (_rounded != null) return _rounded;
            const int size = 64, radius = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "RoundedButton", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // Distance to the nearest corner circle's edge, anti-aliased over one pixel.
                var cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                var cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                var d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                var a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return _rounded;
        }

    }
}
