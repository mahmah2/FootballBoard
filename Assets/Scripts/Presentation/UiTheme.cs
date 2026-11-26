using System;
using TMPro;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>
    /// The game's look in one place: fonts, button colours, title colour and the main-menu
    /// background. Edit Assets/Resources/UI/UiTheme in the Inspector; every screen reads it at start-up.
    /// Missing values fall back to the built-in defaults below.
    /// </summary>
    [CreateAssetMenu(menuName = "Football Board/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Serializable]
        public struct ButtonColors
        {
            public Color fill;
            public Color outline;
            [Tooltip("Text gradient, top")] public Color textTop;
            [Tooltip("Text gradient, bottom")] public Color textBottom;
            [Tooltip("The light band across the upper half (use low alpha)")] public Color shine;

            public ButtonColors(int fill, int outline, int textTop, int textBottom, float shineAlpha)
            {
                this.fill = Hex(fill); this.outline = Hex(outline);
                this.textTop = Hex(textTop); this.textBottom = Hex(textBottom);
                shine = new Color(1, 1, 1, shineAlpha);
            }
        }

        [Header("Fonts (TextMesh Pro font assets)")]
        [Tooltip("Buttons, dropdowns and big titles")] public TMP_FontAsset displayFont;
        [Tooltip("Everything else: scores, clock, messages, subtitles, shirt numbers")] public TMP_FontAsset bodyFont;
        [Tooltip("Letter spacing for body text")] public float bodyCharacterSpacing = 2f;
        [Tooltip("Multiplies every display-font size (some fonts run small)")] public float displaySizeScale = 1.15f;

        [Header("Button styles")]
        public ButtonColors blue = new ButtonColors(0x2E6FE0, 0x0B2A6B, 0xFFE58A, 0xF2A322, 0.26f);
        public ButtonColors green = new ButtonColors(0x2FB45A, 0x0D4A23, 0xFFFFFF, 0xDDF7C8, 0.28f);
        public ButtonColors red = new ButtonColors(0xE0493B, 0x5C130C, 0xFFFFFF, 0xFFE0C8, 0.25f);
        public ButtonColors gold = new ButtonColors(0xFFC93C, 0x7A4A00, 0x1C3E8C, 0x0B2A6B, 0.38f);
        public ButtonColors slate = new ButtonColors(0x3A4A5C, 0x121A24, 0xF6E3D3, 0xC9B8A8, 0.14f);

        [Header("Titles")]
        public Color titleColor = Hex(0xFFD23F);
        public Color titleOutline = Hex(0x0B2A6B);

        [Header("Main menu")]
        [Tooltip("Shown behind the main menu and full-time screen. Leave empty to use Resources/UI/bg_menu, or a plain colour.")]
        public Sprite menuBackground;
        [Tooltip("Multiplies the background so the buttons and title stand out")]
        public Color menuBackgroundTint = new Color(0.8f, 0.8f, 0.8f, 1f);

        [Tooltip("The band behind the main menu's title and subtitle")]
        public Color menuBandColor = new Color(0.02f, 0.05f, 0.12f, 0.6f);

        [Header("Pause")]
        [Tooltip("Laid over the frozen match while paused (alpha = how dark)")]
        public Color pauseTint = new Color(0f, 0f, 0f, 0.5f);

        private static UiTheme _current;

        /// <summary>The theme asset at Resources/UI/UiTheme, or built-in defaults.</summary>
        public static UiTheme Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Resources.Load<UiTheme>("UI/UiTheme");
                if (_current == null) _current = CreateInstance<UiTheme>();
                var fallback = Resources.Load<TMP_FontAsset>("Fonts/Bangers SDF");
                if (_current.displayFont == null) _current.displayFont = fallback;
                if (_current.bodyFont == null) _current.bodyFont = _current.displayFont;
                return _current;
            }
        }

        public ButtonColors Colors(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Green: return green;
                case ButtonStyle.Red: return red;
                case ButtonStyle.Gold: return gold;
                case ButtonStyle.Slate: return slate;
                default: return blue;
            }
        }

        /// <summary>The menu background: the assigned sprite, else Resources/UI/bg_menu, else null.</summary>
        public Sprite MenuBackground()
        {
            if (menuBackground != null) return menuBackground;
            var sprite = Resources.Load<Sprite>("UI/bg_menu");
            if (sprite != null) return menuBackground = sprite;
            var tex = Resources.Load<Texture2D>("UI/bg_menu");
            if (tex != null) menuBackground = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return menuBackground;
        }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
