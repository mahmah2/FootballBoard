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

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Stretch("Label", parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 3f;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.outlineWidth = 0.18f;
            tmp.outlineColor = Palette.Shadow;
            return tmp;
        }

        public static UnityEngine.UI.Button Button(Transform parent, string name, string text, Color background, Color textColor, UnityAction onClick, float fontSize = 30f)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var image = Panel(rt, background);
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            var label = Label(rt, text, fontSize, textColor);
            label.margin = new Vector4(10, 4, 10, 4);
            return button;
        }

        /// <summary>A palette-styled TMP dropdown built from TMP's default control hierarchy.</summary>
        public static TMP_Dropdown Dropdown(Transform parent, string name, System.Collections.Generic.List<string> options, int selected, Action<int> onChanged)
        {
            const float rowHeight = 76f;
            var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            var dropdown = go.GetComponent<TMP_Dropdown>();

            ((UnityEngine.UI.Image)dropdown.targetGraphic).color = Palette.Shadow;
            StyleText(dropdown.captionText, 34f, TextAlignmentOptions.Center);
            dropdown.captionText.rectTransform.offsetMin = new Vector2(20, 0);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-60, 0);
            StyleText(dropdown.itemText, 30f, TextAlignmentOptions.Center);

            var arrow = go.transform.Find("Arrow");
            if (arrow != null)
            {
                var arrowRt = (RectTransform)arrow;
                // A rotated square reads as a chevron/diamond marker without needing a sprite.
                arrowRt.sizeDelta = new Vector2(18, 18);
                arrowRt.localRotation = Quaternion.Euler(0, 0, 45f);
                arrow.GetComponent<UnityEngine.UI.Image>().color = Palette.Accent;
            }

            // Template: taller rows, palette colours.
            var template = dropdown.template;
            template.sizeDelta = new Vector2(0, rowHeight * Mathf.Min(options.Count, 6));
            template.GetComponent<UnityEngine.UI.Image>().color = Palette.PanelOpaque;
            var content = (RectTransform)template.Find("Viewport/Content");
            content.sizeDelta = new Vector2(0, rowHeight);
            var item = (RectTransform)content.Find("Item");
            item.sizeDelta = new Vector2(0, rowHeight);
            var itemBackground = item.Find("Item Background").GetComponent<UnityEngine.UI.Image>();
            itemBackground.color = Palette.Shadow;
            // No tint transition: it multiplies the row colour and turns it near-black.
            item.GetComponent<UnityEngine.UI.Toggle>().transition = UnityEngine.UI.Selectable.Transition.None;
            var checkmark = (RectTransform)item.Find("Item Checkmark");
            checkmark.GetComponent<UnityEngine.UI.Image>().color = Palette.Accent;
            checkmark.sizeDelta = new Vector2(14, 14);
            checkmark.anchoredPosition = new Vector2(28, 0);
            checkmark.localRotation = Quaternion.Euler(0, 0, 45f);
            var scrollbar = template.Find("Scrollbar");
            if (scrollbar != null)
            {
                scrollbar.GetComponent<UnityEngine.UI.Image>().color = Palette.PanelDark;
                var handle = scrollbar.Find("Sliding Area/Handle");
                if (handle != null) handle.GetComponent<UnityEngine.UI.Image>().color = Palette.Accent;
            }

            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(Mathf.Clamp(selected, 0, options.Count - 1));
            dropdown.RefreshShownValue();
            if (onChanged != null) dropdown.onValueChanged.AddListener(i => onChanged(i));
            return dropdown;
        }

        private static void StyleText(TMP_Text text, float size, TextAlignmentOptions align)
        {
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 3f;
            text.color = Palette.Chalk;
            text.alignment = align;
        }

        public static void SetButtonText(UnityEngine.UI.Button button, string text) =>
            button.GetComponentInChildren<TextMeshProUGUI>().text = text;

        public static void SetButtonColor(UnityEngine.UI.Button button, Color color) =>
            ((UnityEngine.UI.Image)button.targetGraphic).color = color;

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
