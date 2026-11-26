using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace FootballBoard.Presentation
{
    /// <summary>Small helpers for building styled uGUI elements in code.</summary>
    public static class UiFactory
    {
        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }

        public static UnityEngine.UI.Image Panel(RectTransform rt, Color color, bool raycast = true)
        {
            var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <param name="font">Set the font here rather than later: once an outline exists the label holds
        /// its own copy of the font material, and swapping fonts afterwards garbles the text.</param>
        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, TMP_FontAsset font = null)
        {
            var rt = Stretch("Label", parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var theme = UiTheme.Current;
            var face = font != null ? font : theme.bodyFont;
            if (face != null) tmp.font = face;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = face != null ? FontStyles.Normal : FontStyles.Bold;
            tmp.characterSpacing = font != null ? 3f : theme.bodyCharacterSpacing;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.outlineWidth = 0.18f;
            tmp.outlineColor = Palette.Shadow;
            return tmp;
        }

        /// <summary>An animated game button (see <see cref="AnimatedButton"/>) in one of the colour styles.</summary>
        public static UnityEngine.UI.Button Button(Transform parent, string name, string text, ButtonStyle style, UnityAction onClick, float fontSize = 30f)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            // An invisible hit area; the visible layers live in the animated child "Root".
            var hit = Panel(rt, new Color(0, 0, 0, 0));
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = hit;
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            if (onClick != null) button.onClick.AddListener(onClick);
            AnimatedButton.Build(button, text, fontSize, style);
            return button;
        }

        /// <summary>
        /// A dropdown in the game-button look: dark rounded outline with a drop shadow, blue fill with
        /// a shine band, gold display-font caption; the open list uses the same rounded style.
        /// </summary>
        public static TMP_Dropdown Dropdown(Transform parent, string name, System.Collections.Generic.List<string> options, int selected, Action<int> onChanged)
        {
            const float rowHeight = 76f;
            var navy = new Color(0.043f, 0.165f, 0.42f, 1f);
            var blue = new Color(0.18f, 0.435f, 0.878f, 1f);
            var gold = new Color(1f, 0.898f, 0.541f, 1f);
            var amber = new Color(0.949f, 0.639f, 0.133f, 1f);

            var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            var dropdown = go.GetComponent<TMP_Dropdown>();
            dropdown.transition = UnityEngine.UI.Selectable.Transition.None;
            go.AddComponent<UiPressScale>();

            // Face: outline (the root image), fill and shine, with a drop shadow.
            var rootImage = (UnityEngine.UI.Image)dropdown.targetGraphic;
            RoundedImage(rootImage, navy, 1.2f);
            var shadow = go.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.45f);
            shadow.effectDistance = new Vector2(5, -6);
            var fill = Stretch("Fill", go.transform);
            fill.offsetMin = new Vector2(5, 5);
            fill.offsetMax = new Vector2(-5, -5);
            RoundedImage(Panel(fill, blue, false), blue, 1.45f);
            fill.SetSiblingIndex(0);
            var shine = Rect("Shine", go.transform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            shine.offsetMin = new Vector2(11, 0);
            shine.offsetMax = new Vector2(-11, -9);
            RoundedImage(Panel(shine, Color.white, false), new Color(1, 1, 1, 0.24f), 2.2f);
            shine.SetSiblingIndex(1);

            StyleText(dropdown.captionText, 36f, TextAlignmentOptions.Center);
            Gradient(dropdown.captionText, gold, amber, navy);
            dropdown.captionText.rectTransform.offsetMin = new Vector2(24, 0);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-56, 0);
            StyleText(dropdown.itemText, 32f, TextAlignmentOptions.Center);
            Gradient(dropdown.itemText, Color.white, new Color(0.87f, 0.92f, 1f, 1f), navy);

            var arrow = go.transform.Find("Arrow");
            if (arrow != null)
            {
                var arrowRt = (RectTransform)arrow;
                // A triangle pointing down says "this opens a list".
                arrowRt.sizeDelta = new Vector2(30, 30);
                arrowRt.anchoredPosition = new Vector2(-32, 0);
                arrowRt.localRotation = Quaternion.identity;
                var arrowImage = arrow.GetComponent<UnityEngine.UI.Image>();
                arrowImage.sprite = SpeakerIcon.DownTriangle();
                arrowImage.type = UnityEngine.UI.Image.Type.Simple;
                arrowImage.color = gold;
                var arrowOutline = arrow.gameObject.AddComponent<UnityEngine.UI.Outline>();
                arrowOutline.effectColor = navy;
                arrowOutline.effectDistance = new Vector2(2, -2);
                arrow.SetAsLastSibling();
            }

            // The open list: a navy rounded panel with rounded blue rows that light up under the pointer.
            var template = dropdown.template;
            template.sizeDelta = new Vector2(0, rowHeight * Mathf.Min(options.Count, 6) + 12);
            RoundedImage(template.GetComponent<UnityEngine.UI.Image>(), navy, 1.2f);
            var listShadow = template.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            listShadow.effectColor = new Color(0, 0, 0, 0.45f);
            listShadow.effectDistance = new Vector2(5, -6);
            var viewport = (RectTransform)template.Find("Viewport");
            viewport.offsetMin = new Vector2(6, 6);
            viewport.offsetMax = new Vector2(-6, -6);
            var content = (RectTransform)template.Find("Viewport/Content");
            content.sizeDelta = new Vector2(0, rowHeight);
            var item = (RectTransform)content.Find("Item");
            item.sizeDelta = new Vector2(0, rowHeight);
            var itemBackground = item.Find("Item Background").GetComponent<UnityEngine.UI.Image>();
            RoundedImage(itemBackground, blue, 1.45f);
            var bgRt = itemBackground.rectTransform;
            bgRt.offsetMin = new Vector2(4, 3);
            bgRt.offsetMax = new Vector2(-4, -3);
            var toggle = item.GetComponent<UnityEngine.UI.Toggle>();
            toggle.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            toggle.targetGraphic = itemBackground;
            var tint = toggle.colors;
            tint.normalColor = new Color(0.62f, 0.62f, 0.7f, 1f);
            tint.highlightedColor = Color.white;
            tint.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            tint.selectedColor = Color.white;
            tint.colorMultiplier = 1f;
            tint.fadeDuration = 0.08f;
            toggle.colors = tint;
            var checkmark = (RectTransform)item.Find("Item Checkmark");
            checkmark.GetComponent<UnityEngine.UI.Image>().color = gold;
            checkmark.sizeDelta = new Vector2(14, 14);
            checkmark.anchoredPosition = new Vector2(30, 0);
            checkmark.localRotation = Quaternion.Euler(0, 0, 45f);
            var scrollbar = template.Find("Scrollbar");
            if (scrollbar != null)
            {
                scrollbar.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.25f);
                var handle = scrollbar.Find("Sliding Area/Handle");
                if (handle != null) handle.GetComponent<UnityEngine.UI.Image>().color = gold;
            }

            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(Mathf.Clamp(selected, 0, options.Count - 1));
            dropdown.RefreshShownValue();
            if (onChanged != null) dropdown.onValueChanged.AddListener(i => onChanged(i));
            return dropdown;
        }

        private static void RoundedImage(UnityEngine.UI.Image image, Color color, float cornerTightness)
        {
            image.sprite = AnimatedButton.RoundedSprite();
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = cornerTightness;
            image.color = color;
        }

        private static void Gradient(TMP_Text text, Color top, Color bottom, Color outline)
        {
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(top, top, bottom, bottom);
            text.color = Color.white;
            text.outlineWidth = 0.2f;
            text.outlineColor = outline;
        }

        // ------------------------------------------------------------------ display font

        /// <summary>The display font for buttons and titles (set in the UI theme asset).</summary>
        public static TMP_FontAsset DisplayFont => UiTheme.Current.displayFont;

        public static void UseDisplayFont(TMP_Text text)
        {
            if (DisplayFont == null || text.font == DisplayFont) return;
            // An outline gives the label its own copy of the old font's material; swapping only the
            // font would keep that copy and draw the new glyphs from the old atlas (garbled text).
            // Reset to the new font's material, then put the outline back.
            var width = text.outlineWidth;
            var color = text.outlineColor;
            text.font = DisplayFont;
            text.fontSharedMaterial = DisplayFont.material;
            text.outlineWidth = width;
            text.outlineColor = color;
        }

        private static void StyleText(TMP_Text text, float size, TextAlignmentOptions align)
        {
            UseDisplayFont(text);
            text.fontSize = size;
            text.fontStyle = DisplayFont != null ? FontStyles.Normal : FontStyles.Bold;
            text.characterSpacing = 3f;
            text.color = Palette.Chalk;
            text.alignment = align;
        }

        public static void SetButtonText(UnityEngine.UI.Button button, string text) =>
            button.GetComponentInChildren<TextMeshProUGUI>().text = text;

        public static void SetButtonStyle(UnityEngine.UI.Button button, ButtonStyle style) =>
            button.GetComponent<AnimatedButton>().SetStyle(style);

        public static UnityEngine.UI.HorizontalLayoutGroup Row(RectTransform rt, float spacing)
        {
            var row = rt.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            return row;
        }

        public static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }

        public static void Preferred(Component c, float width, float height)
        {
            var le = c.gameObject.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minHeight = height;
        }

        public static string Clock(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

        public static Action Noop => () => { };
    }
}
