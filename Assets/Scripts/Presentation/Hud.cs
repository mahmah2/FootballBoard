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
        public event Action MenuClicked;
        public event Action SoundClicked;
        public event Action<float> VolumeChanged;

        private TextMeshProUGUI _redScore, _blueScore, _clock, _status, _message, _bannerTitle, _bannerSub;
        private TextMeshProUGUI _overlayTitle, _overlaySub;
        private RectTransform _optionsRow, _overlay, _overlayButtons, _banner, _pause, _pauseButtons, _menuButton;
        private RectTransform _overlayColumn, _mainMenu, _menuDropdowns, _menuKickOffSlot;
        private TextMeshProUGUI _menuTitle, _menuSub;
        private UnityEngine.UI.Image _overlayImage, _soundIcon, _overlayBackground;
        private RectTransform _soundButton, _volumeRoot;
        private UnityEngine.UI.Slider _volume;
        private Sprite _speakerOn, _speakerOff;
        private UnityEngine.UI.Button _confirm;
        private readonly Dictionary<CarrierOption, UnityEngine.UI.Button> _options = new Dictionary<CarrierOption, UnityEngine.UI.Button>();
        private Coroutine _messageFade;
        private Team? _pulseTeam;
        private float _pulseStart;

        /// <summary>Seconds for one full fade out and back in of the planning team's score.</summary>
        private const float PulsePeriod = 1.6f;
        private const float PulseMinAlpha = 0.25f;

        public CutscenePlayer Cutscenes { get; private set; }

        public bool OverlayVisible => _overlay.gameObject.activeSelf;
        public bool PauseVisible => _pause.gameObject.activeSelf;

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
            Cutscenes = gameObject.AddComponent<CutscenePlayer>();
            Cutscenes.Build(root);
            BuildOverlay(root);
            _menuButton.SetAsLastSibling(); // reachable from the hot-seat handover screens too
            _soundButton.SetAsLastSibling();
            _volumeRoot.SetAsLastSibling();
            BuildPause(root);
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

            var fit = UiFactory.Button(root, "FitButton", "FIT", ButtonStyle.Slate, () => FitClicked?.Invoke(), 24);
            var fitRt = (RectTransform)fit.transform;
            fitRt.anchorMin = fitRt.anchorMax = fitRt.pivot = new Vector2(1, 1);
            fitRt.anchoredPosition = new Vector2(-24, -116);
            fitRt.sizeDelta = new Vector2(110, 60);

            var menu = UiFactory.Button(root, "MenuButton", "MENU", ButtonStyle.Slate, () => MenuClicked?.Invoke(), 24);
            _menuButton = (RectTransform)menu.transform;
            _menuButton.anchorMin = _menuButton.anchorMax = _menuButton.pivot = new Vector2(0, 1);
            _menuButton.anchoredPosition = new Vector2(24, -116);
            _menuButton.sizeDelta = new Vector2(130, 60);
            _menuButton.gameObject.SetActive(false);

            // Speaker icon next to MENU: tap to turn all sound on or off.
            var sound = UiFactory.Button(root, "SoundButton", "", ButtonStyle.Slate, () => SoundClicked?.Invoke(), 24);
            _soundButton = (RectTransform)sound.transform;
            _soundButton.anchorMin = _soundButton.anchorMax = _soundButton.pivot = new Vector2(0, 1);
            _soundButton.anchoredPosition = new Vector2(24 + 130 + 12, -116);
            _soundButton.sizeDelta = new Vector2(72, 60);
            // The icon scales with the button (bigger on the menus), keeping its proportions.
            var icon = UiFactory.Rect("Icon", sound.GetComponent<AnimatedButton>().Root, new Vector2(0.15f, 0.12f), new Vector2(0.85f, 0.88f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _soundIcon = UiFactory.Panel(icon, Palette.Chalk, false);
            _soundIcon.preserveAspect = true;
            _speakerOn = SpeakerIcon.Create(true);
            _speakerOff = SpeakerIcon.Create(false);
            _soundIcon.sprite = _speakerOn;
            _soundButton.gameObject.SetActive(false);
            BuildVolumeSlider(root);
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
            UiFactory.Button(actions, "ClearButton", "CLEAR", ButtonStyle.Red, () => ClearClicked?.Invoke());
            _confirm = UiFactory.Button(actions, "ConfirmButton", "CONFIRM", ButtonStyle.Green, () => ConfirmClicked?.Invoke(), 34);

            var message = UiFactory.Rect("Message", root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 132), new Vector2(-200, 60));
            _message = UiFactory.Label(message, "", 28, Palette.Chalk);
        }

        private void AddOption(CarrierOption option, string text)
        {
            var button = UiFactory.Button(_optionsRow, option + "Button", text, ButtonStyle.Blue, () => OptionChosen?.Invoke(option), 28);
            _options[option] = button;
        }

        private void BuildBanner(Transform root)
        {
            _banner = UiFactory.Rect("Banner", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1100, 220));
            UiFactory.Panel(_banner, Palette.PanelDark, false);
            var title = UiFactory.Rect("Title", _banner, new Vector2(0, 0.42f), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            _bannerTitle = UiFactory.Label(title, "", 80, Palette.Accent, TextAlignmentOptions.Center, UiFactory.DisplayFont);
            var sub = UiFactory.Rect("Subtitle", _banner, new Vector2(0, 0), new Vector2(1, 0.45f), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);
            _bannerSub = UiFactory.Label(sub, "", 30, Palette.Chalk);
            _banner.gameObject.SetActive(false);
        }

        private void BuildOverlay(Transform root)
        {
            _overlay = UiFactory.Stretch("Overlay", root);
            _overlayImage = UiFactory.Panel(_overlay, Palette.PanelOpaque);
            // Optional artwork behind the main menu (UI theme), filling the screen without stretching.
            var bg = UiFactory.Stretch("Background", _overlay);
            _overlayBackground = UiFactory.Panel(bg, Color.white, false);
            _overlayBackground.preserveAspect = false;
            var fitter = bg.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            bg.gameObject.SetActive(false);
            var column = _overlayColumn = CentredColumn(_overlay, 1400);
            _overlayTitle = UiFactory.Label(column, "", 96, Palette.Accent, TextAlignmentOptions.Center, UiFactory.DisplayFont);
            _overlayTitle.outlineColor = UiTheme.Current.titleOutline;
            UiFactory.Preferred(_overlayTitle, 1400, 110);
            _overlaySub = UiFactory.Label(column, "", 34, Palette.Chalk);
            _overlayButtons = ButtonColumn(column);
            BuildMainMenu();
            _overlay.gameObject.SetActive(false);
        }

        // Main menu layout (sizes in canvas units, reference 1920x1080).
        private const float MenuRowWidth = 1480, MenuDropdownWidth = 760, MenuDropdownHeight = 88, MenuSpacing = 18;
        private const float KickOffWidth = 640, KickOffHeight = 124;

        /// <summary>
        /// The main menu's own layout: title and subtitle on a dark band across the top, the dropdowns
        /// stacked on the left and KICK OFF on the right, its bottom level with the last dropdown's.
        /// </summary>
        private void BuildMainMenu()
        {
            _mainMenu = UiFactory.Stretch("MainMenu", _overlay);

            var band = UiFactory.Rect("TitleBand", _mainMenu, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(0, 230));
            UiFactory.Panel(band, UiTheme.Current.menuBandColor, false);
            var title = UiFactory.Rect("Title", band, new Vector2(0, 0.38f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _menuTitle = UiFactory.Label(title, "", 104, UiTheme.Current.titleColor, TextAlignmentOptions.Center, UiFactory.DisplayFont);
            _menuTitle.outlineColor = UiTheme.Current.titleOutline;
            var sub = UiFactory.Rect("Subtitle", band, new Vector2(0, 0.04f), new Vector2(1, 0.4f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _menuSub = UiFactory.Label(sub, "", 36, Palette.Chalk);

            // The row of controls under the band, centred on the screen.
            var rowHeight = 3 * MenuDropdownHeight + 2 * MenuSpacing;
            var row = UiFactory.Rect("Controls", _mainMenu, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(MenuRowWidth, rowHeight));

            _menuDropdowns = UiFactory.Rect("Dropdowns", row, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), Vector2.zero, new Vector2(MenuDropdownWidth, 0));
            var stack = _menuDropdowns.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            stack.spacing = MenuSpacing;
            stack.childAlignment = TextAnchor.LowerLeft; // stack from the bottom: the last dropdown sits on the row's bottom edge
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            _menuKickOffSlot = UiFactory.Rect("KickOff", row, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(KickOffWidth, KickOffHeight));
            _mainMenu.gameObject.SetActive(false);
        }

        /// <summary>Shows the main menu (dropdowns are added with <see cref="AddMenuDropdown"/>).</summary>
        public void ShowMainMenu(string title, string subtitle, Action kickOff)
        {
            _overlay.gameObject.SetActive(true);
            _overlayImage.color = Palette.PanelOpaque;
            _overlayBackground.gameObject.SetActive(false);
            _overlayColumn.gameObject.SetActive(false);
            _mainMenu.gameObject.SetActive(true);
            _menuTitle.text = title;
            _menuSub.text = subtitle;
            UiFactory.ClearChildren(_menuDropdowns);
            UiFactory.ClearChildren(_menuKickOffSlot);
            var button = UiFactory.Button(_menuKickOffSlot, "KickOffButton", "KICK OFF", ButtonStyle.Green, () => kickOff(), 46);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public void AddMenuDropdown(string name, List<string> options, int selected, Action<int> onChanged)
        {
            var dropdown = UiFactory.Dropdown(_menuDropdowns, name, options, selected, onChanged);
            UiFactory.Preferred(dropdown, MenuDropdownWidth, MenuDropdownHeight);
        }

        private void BuildPause(Transform root)
        {
            _pause = UiFactory.Stretch("PauseMenu", root);
            UiFactory.Panel(_pause, UiTheme.Current.pauseTint); // the frozen match shows through
            var column = CentredColumn(_pause, 1000);
            var title = UiFactory.Label(column, "PAUSED", 96, UiTheme.Current.titleColor, TextAlignmentOptions.Center, UiFactory.DisplayFont);
            title.outlineColor = UiTheme.Current.titleOutline;
            UiFactory.Preferred(title, 1000, 110);
            _pauseButtons = ButtonColumn(column);
            _pause.gameObject.SetActive(false);
        }

        /// <summary>
        /// A column centred on the screen that grows to fit its children, so panels stay centred on
        /// any aspect ratio (a wide phone is much shorter than a 16:9 monitor in canvas units).
        /// </summary>
        private static RectTransform CentredColumn(RectTransform parent, float width)
        {
            var column = UiFactory.Rect("Column", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 0));
            var layout = column.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 28;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = column.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            return column;
        }

        private static RectTransform ButtonColumn(RectTransform parent)
        {
            var buttons = UiFactory.Rect("Buttons", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var layout = buttons.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 18;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return buttons;
        }

        // ------------------------------------------------------------------ HUD state

        /// <summary>Fades the planning team's name in and out; null stops it.</summary>
        public void SetPlanningTeam(Team? team)
        {
            if (team == _pulseTeam) return;
            _pulseTeam = team;
            _pulseStart = Time.unscaledTime;
            SetAlpha(_redScore, 1f);
            SetAlpha(_blueScore, 1f);
        }

        private void Update()
        {
            if (!_pulseTeam.HasValue || _redScore == null) return;
            // Cosine ease: starts fully visible, glides down to the minimum and back up.
            var phase = (Time.unscaledTime - _pulseStart) / PulsePeriod * Mathf.PI * 2f;
            var alpha = Mathf.Lerp(PulseMinAlpha, 1f, 0.5f + 0.5f * Mathf.Cos(phase));
            SetAlpha(_pulseTeam == Team.Red ? _redScore : _blueScore, alpha);
        }

        private static void SetAlpha(TextMeshProUGUI label, float alpha)
        {
            var c = label.color;
            c.a = alpha;
            label.color = c;
        }

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

        /// <summary>Shows the plan buttons; the carrier options only while the ball carrier is selected.</summary>
        public void SetPlanControls(bool visible, bool showOptions, CarrierOption selected)
        {
            _optionsRow.parent.gameObject.SetActive(visible);
            _optionsRow.gameObject.SetActive(showOptions);
            // Without the options the bar's backing panel goes too, leaving only CLEAR/CONFIRM over the pitch.
            _optionsRow.parent.GetComponent<UnityEngine.UI.Image>().enabled = showOptions;
            foreach (var pair in _options)
            {
                var active = pair.Key == selected;
                UiFactory.SetButtonStyle(pair.Value, active ? ButtonStyle.Gold : ButtonStyle.Blue);
            }
        }

        public void SetConfirmLabel(string text) => UiFactory.SetButtonText(_confirm, text);

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
            _overlayBackground.gameObject.SetActive(false);
            _mainMenu.gameObject.SetActive(false);
            _overlayColumn.gameObject.SetActive(true);
            _overlayImage.color = opaque ? Palette.PanelOpaque : Palette.PanelDark;
            _overlayTitle.text = title;
            _overlayTitle.color = titleColor;
            _overlaySub.text = subtitle;
            UiFactory.ClearChildren(_overlayButtons);
            foreach (var (label, onClick, primary) in buttons)
            {
                var button = UiFactory.Button(_overlayButtons, label.Replace(" ", "") + "Button", label,
                    primary ? ButtonStyle.Green : ButtonStyle.Blue, () => onClick(), 34);
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

        /// <summary>Shows the theme's menu background behind the overlay (call after <see cref="ShowOverlay"/>).</summary>
        public void ShowOverlayBackground()
        {
            var theme = UiTheme.Current;
            var sprite = theme.MenuBackground();
            if (sprite == null) return;
            _overlayBackground.sprite = sprite;
            _overlayBackground.color = theme.menuBackgroundTint;
            _overlayBackground.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectRatio = sprite.rect.width / sprite.rect.height;
            _overlayBackground.gameObject.SetActive(true);
        }

        /// <summary>The in-match MENU button (hidden on the main menu and at full time).</summary>
        /// <summary>
        /// MENU shows only during a match. The speaker button is always available: next to MENU in a
        /// match, in the top-left corner on the menus.
        /// </summary>
        public void SetMenuButtonVisible(bool visible)
        {
            _menuButton.gameObject.SetActive(visible);
            _soundButton.gameObject.SetActive(true);
            _soundButton.anchoredPosition = visible ? new Vector2(24 + 130 + 12, -116) : new Vector2(24, -24);
            _soundButton.sizeDelta = visible ? new Vector2(72, 60) : new Vector2(96, 80);
            // The volume slider sits just right of the speaker, vertically centred on it.
            _volumeRoot.gameObject.SetActive(true);
            _volumeRoot.anchoredPosition = _soundButton.anchoredPosition + new Vector2(_soundButton.sizeDelta.x + 24, 0);
            _volumeRoot.sizeDelta = new Vector2(visible ? 200 : 240, _soundButton.sizeDelta.y);
        }

        /// <summary>
        /// Master volume: a thin line (gold up to the handle) with a white circle to drag.
        /// All the way left mutes.
        /// </summary>
        private void BuildVolumeSlider(Transform root)
        {
            var gold = UiTheme.Current.gold.fill;
            var navy = UiTheme.Current.blue.outline;
            _volumeRoot = UiFactory.Rect("Volume", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(200, 60));
            // A transparent hit area so the whole strip can be grabbed, not just the thin line.
            UiFactory.Panel(_volumeRoot, new Color(0, 0, 0, 0));

            var track = UiFactory.Rect("Track", _volumeRoot, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24, 8));
            Line(track, new Color(0.04f, 0.08f, 0.16f, 0.75f));

            var fillArea = UiFactory.Rect("Fill Area", _volumeRoot, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24, 8));
            var fill = UiFactory.Rect("Fill", fillArea, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            Line(fill, gold);

            var handleArea = UiFactory.Rect("Handle Slide Area", _volumeRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24, 0));
            var handle = UiFactory.Rect("Handle", handleArea, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
            var handleImage = UiFactory.Panel(handle, Color.white);
            handleImage.sprite = SpeakerIcon.Circle();
            handleImage.preserveAspect = true; // the slider stretches the handle to its height: keep it round
            var ring = handle.gameObject.AddComponent<UnityEngine.UI.Outline>();
            ring.effectColor = navy;
            ring.effectDistance = new Vector2(2, -2);
            var shadow = handle.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.45f);
            shadow.effectDistance = new Vector2(2, -4);
            handle.gameObject.AddComponent<UiPressScale>();

            _volume = _volumeRoot.gameObject.AddComponent<UnityEngine.UI.Slider>();
            _volume.fillRect = fill;
            _volume.handleRect = handle;
            _volume.targetGraphic = handleImage;
            _volume.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            _volume.minValue = 0f;
            _volume.maxValue = 1f;
            _volume.transition = UnityEngine.UI.Selectable.Transition.None;
            _volume.onValueChanged.AddListener(v => VolumeChanged?.Invoke(v));
            _volumeRoot.gameObject.SetActive(false);
        }

        private static void Line(RectTransform rt, Color color)
        {
            var image = UiFactory.Panel(rt, color, false);
            image.sprite = AnimatedButton.RoundedSprite();
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 6f; // fully rounded ends on an 8-unit line
        }

        /// <summary>Moves the slider to <paramref name="volume"/> without raising <see cref="VolumeChanged"/>.</summary>
        public void SetVolume(float volume) => _volume.SetValueWithoutNotify(volume);

        /// <summary>Shows the speaker with sound waves (on) or crossed out (off).</summary>
        public void SetSoundIcon(bool on)
        {
            _soundIcon.sprite = on ? _speakerOn : _speakerOff;
            _soundIcon.color = on ? Palette.Chalk : Palette.WithAlpha(Palette.Chalk, 0.6f);
        }

        public void ShowPause(Action onResume, Action onEndMatch)
        {
            _pause.gameObject.SetActive(true);
            UiFactory.ClearChildren(_pauseButtons);
            foreach (var (label, onClick, primary) in new (string, Action, bool)[] { ("RESUME", onResume, true), ("END MATCH", onEndMatch, false) })
            {
                var button = UiFactory.Button(_pauseButtons, label.Replace(" ", "") + "Button", label,
                    primary ? ButtonStyle.Green : ButtonStyle.Red, () => onClick(), 34);
                UiFactory.Preferred(button, 700, 88);
            }
        }

        public void HidePause() => _pause.gameObject.SetActive(false);

        /// <summary>Clears transient match UI (banner, message, plan controls) when a match is abandoned.</summary>
        public void ResetMatchUi()
        {
            StopAllCoroutines();
            _banner.gameObject.SetActive(false);
            Cutscenes.Hide();
            _message.text = "";
            SetStatus("", Palette.Chalk);
            SetPlanningTeam(null);
            SetPlanControls(false, false, CarrierOption.Move);
        }
    }
}
