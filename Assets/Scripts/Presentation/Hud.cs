using System;
using System.Collections;
using System.Collections.Generic;
using FootballBoard.Domain;
using TMPro;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>Match HUD and full-screen panels (uGUI, built in code). No game logic lives here.</summary>
    public sealed class Hud : MonoBehaviour
    {
        public event Action<CarrierOption> OptionChosen;
        public event Action ConfirmClicked;
        public event Action ClearClicked;
        public event Action FitClicked;

        private TextMeshProUGUI _redScore, _blueScore, _clock, _status, _message, _bannerTitle, _bannerSub;
        private TextMeshProUGUI _overlayTitle, _overlaySub;
        private RectTransform _optionsRow, _overlay, _overlayButtons, _banner;
        private UnityEngine.UI.Image _overlayImage;
        private UnityEngine.UI.Button _confirm;
        private readonly Dictionary<CarrierOption, UnityEngine.UI.Button> _options = new Dictionary<CarrierOption, UnityEngine.UI.Button>();
        private Coroutine _messageFade;

        public bool OverlayVisible => _overlay.gameObject.activeSelf;

        public void Build()
        {
            var canvasGo = new GameObject("HudCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var root = canvasGo.transform;

            BuildTopBar(root);
            BuildBottomBar(root);
            BuildBanner(root);
            BuildOverlay(root);
        }

        private void BuildTopBar(Transform root)
        {
            var bar = UiFactory.Rect("TopBar", root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 96));
            UiFactory.Panel(bar, Palette.PanelDark);

            var red = UiFactory.Rect("RedScore", bar, new Vector2(0, 0), new Vector2(0.3f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            _redScore = UiFactory.Label(red, "RED 0", 46, Palette.RedPrimary, TextAlignmentOptions.MidlineLeft);
            _redScore.margin = new Vector4(36, 0, 0, 0);
            var blue = UiFactory.Rect("BlueScore", bar, new Vector2(0.7f, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, Vector2.zero);
            _blueScore = UiFactory.Label(blue, "0 BLUE", 46, Palette.BluePrimary, TextAlignmentOptions.MidlineRight);
            _blueScore.margin = new Vector4(0, 0, 36, 0);

            var centre = UiFactory.Rect("Clock", bar, new Vector2(0.3f, 0.42f), new Vector2(0.7f, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            _clock = UiFactory.Label(centre, "00:00", 40, Palette.Chalk);
            var status = UiFactory.Rect("Status", bar, new Vector2(0.3f, 0), new Vector2(0.7f, 0.46f), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);
            _status = UiFactory.Label(status, "", 24, Palette.Accent);

            var fit = UiFactory.Button(root, "FitButton", "FIT", Palette.PanelDark, Palette.Chalk, () => FitClicked?.Invoke(), 24);
            var fitRt = (RectTransform)fit.transform;
            fitRt.anchorMin = fitRt.anchorMax = fitRt.pivot = new Vector2(1, 1);
            fitRt.anchoredPosition = new Vector2(-24, -116);
            fitRt.sizeDelta = new Vector2(110, 60);
        }

        private void BuildBottomBar(Transform root)
        {
            var bar = UiFactory.Rect("BottomBar", root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 124));
            UiFactory.Panel(bar, Palette.PanelDark);

            _optionsRow = UiFactory.Rect("CarrierOptions", bar, new Vector2(0, 0), new Vector2(0.62f, 1), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(-24, -28));
            UiFactory.Row(_optionsRow, 14);
            AddOption(CarrierOption.Move, "MOVE");
            AddOption(CarrierOption.StraightPass, "PASS");
            AddOption(CarrierOption.LongPass, "LONG PASS");
            AddOption(CarrierOption.Shot, "SHOOT");

            var actions = UiFactory.Rect("PlanActions", bar, new Vector2(0.64f, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(-24, -28));
            UiFactory.Row(actions, 14);
            UiFactory.Button(actions, "ClearButton", "CLEAR", Palette.Shadow, Palette.Chalk, () => ClearClicked?.Invoke());
            _confirm = UiFactory.Button(actions, "ConfirmButton", "CONFIRM", Palette.Accent, Palette.Shadow, () => ConfirmClicked?.Invoke(), 34);

            var message = UiFactory.Rect("Message", root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 132), new Vector2(-200, 60));
            _message = UiFactory.Label(message, "", 28, Palette.Chalk);
        }

        private void AddOption(CarrierOption option, string text)
        {
            var button = UiFactory.Button(_optionsRow, option + "Button", text, Palette.Shadow, Palette.Chalk, () => OptionChosen?.Invoke(option), 28);
            _options[option] = button;
        }

        private void BuildBanner(Transform root)
        {
            _banner = UiFactory.Rect("Banner", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1100, 220));
            UiFactory.Panel(_banner, Palette.PanelDark, false);
            var title = UiFactory.Rect("Title", _banner, new Vector2(0, 0.42f), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            _bannerTitle = UiFactory.Label(title, "", 72, Palette.Accent);
            var sub = UiFactory.Rect("Subtitle", _banner, new Vector2(0, 0), new Vector2(1, 0.45f), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);
            _bannerSub = UiFactory.Label(sub, "", 30, Palette.Chalk);
            _banner.gameObject.SetActive(false);
        }

        private void BuildOverlay(Transform root)
        {
            _overlay = UiFactory.Stretch("Overlay", root);
            _overlayImage = UiFactory.Panel(_overlay, Palette.PanelOpaque);
            var title = UiFactory.Rect("Title", _overlay, new Vector2(0, 0.58f), new Vector2(1, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _overlayTitle = UiFactory.Label(title, "", 84, Palette.Accent);
            var sub = UiFactory.Rect("Subtitle", _overlay, new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.58f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _overlaySub = UiFactory.Label(sub, "", 34, Palette.Chalk);
            _overlayButtons = UiFactory.Rect("Buttons", _overlay, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.4f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 0));
            var column = _overlayButtons.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            column.spacing = 18;
            column.childAlignment = TextAnchor.MiddleCenter;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            _overlay.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ HUD state

        public void SetScore(int red, int blue)
        {
            _redScore.text = "RED " + red;
            _blueScore.text = blue + " BLUE";
        }

        public void SetClock(int elapsed, int duration, bool goldenGoal) =>
            _clock.text = goldenGoal ? "GOLDEN GOAL" : $"{UiFactory.Clock(elapsed)} / {UiFactory.Clock(duration)}";

        public void SetStatus(string text, Color color)
        {
            _status.text = text;
            _status.color = color;
        }

        public void ShowMessage(string text, bool isError = false)
        {
            if (_messageFade != null) StopCoroutine(_messageFade);
            _message.text = text;
            _message.color = isError ? Palette.Warning : Palette.Chalk;
            _messageFade = StartCoroutine(ClearMessageLater(isError ? 4f : 3f));
        }

        private IEnumerator ClearMessageLater(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _message.text = "";
        }

        /// <summary>Shows the carrier options (only when the planning team holds the ball on its own turn).</summary>
        public void SetPlanControls(bool visible, bool showOptions, CarrierOption selected)
        {
            _optionsRow.parent.gameObject.SetActive(visible);
            _optionsRow.gameObject.SetActive(showOptions);
            foreach (var pair in _options)
            {
                var active = pair.Key == selected;
                UiFactory.SetButtonColor(pair.Value, active ? Palette.Accent : Palette.Shadow);
                pair.Value.GetComponentInChildren<TextMeshProUGUI>().color = active ? Palette.Shadow : Palette.Chalk;
            }
        }

        // ------------------------------------------------------------------ banner (randomized outcomes)

        public IEnumerator Banner(string title, string subtitle, Color color, float seconds, bool suspense)
        {
            _banner.gameObject.SetActive(true);
            _bannerTitle.color = color;
            _bannerSub.text = subtitle;
            if (suspense)
            {
                // Slow reveal: the result is already decided; this is presentation only.
                for (var i = 0; i < 3; i++)
                {
                    _bannerTitle.text = new string('.', i + 1);
                    yield return new WaitForSeconds(0.25f);
                }
            }
            _bannerTitle.text = title;
            var rt = _bannerTitle.rectTransform;
            for (var t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                rt.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, t / 0.2f);
                yield return null;
            }
            rt.localScale = Vector3.one;
            yield return new WaitForSeconds(seconds);
            _banner.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ full-screen panels

        public void ShowOverlay(string title, string subtitle, Color titleColor, bool opaque, params (string label, Action onClick, bool primary)[] buttons)
        {
            _overlay.gameObject.SetActive(true);
            _overlayImage.color = opaque ? Palette.PanelOpaque : Palette.PanelDark;
            _overlayTitle.text = title;
            _overlayTitle.color = titleColor;
            _overlaySub.text = subtitle;
            UiFactory.ClearChildren(_overlayButtons);
            foreach (var (label, onClick, primary) in buttons)
            {
                var button = UiFactory.Button(_overlayButtons, label.Replace(" ", "") + "Button", label,
                    primary ? Palette.Accent : Palette.Shadow, primary ? Palette.Shadow : Palette.Chalk,
                    () => onClick(), 34);
                UiFactory.Preferred(button, 700, 88);
            }
        }

        /// <summary>Adds a dropdown above the overlay's buttons (call after <see cref="ShowOverlay"/>).</summary>
        public void AddOverlayDropdown(string name, int position, List<string> options, int selected, Action<int> onChanged)
        {
            var dropdown = UiFactory.Dropdown(_overlayButtons, name, options, selected, onChanged);
            UiFactory.Preferred(dropdown, 700, 88);
            dropdown.transform.SetSiblingIndex(position);
        }

        public void HideOverlay() => _overlay.gameObject.SetActive(false);
    }
}
