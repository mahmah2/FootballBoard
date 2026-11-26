using System.Collections;
using System.Collections.Generic;
using FootballBoard.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FootballBoard.Presentation
{
    /// <summary>The short illustrated scenes played when a random roll decides something.</summary>
    public enum CutsceneKind
    {
        KeeperDive, // shots: the keeper flies across, the ball drifts the same way
        Duel,       // tackles, 50/50s, contested passes: two players close in, the ball squirts out
        LongBall    // long passes: the ball sails overhead, a runner chases it
    }

    /// <summary>
    /// Plays cutscenes as layered sprites over a background, on the HUD canvas.
    /// Art is loaded from <c>Resources/Cutscenes/&lt;name&gt;</c> (see docs/CUTSCENE_ART.md);
    /// anything missing is replaced by generated placeholder art, so the feature works before
    /// the real images exist. Tap to skip. Uses scaled time, so the pause menu freezes it.
    /// </summary>
    public sealed class CutscenePlayer : MonoBehaviour
    {
        private const float Duration = 1.9f;
        private const float FadeIn = 0.18f, FadeOut = 0.22f;

        private RectTransform _root, _stage;
        private CanvasGroup _group;
        private Image _background, _speedLines;
        private TextMeshProUGUI _title, _subtitle;
        private bool _skip;
        private readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        /// <summary>A sprite layer: positions are in screen heights from the centre (x right, y up).</summary>
        private struct Layer
        {
            public Sprite Sprite;
            public float Height;              // sprite height, in screen heights
            public Vector2 From, To;
            public float RotationFrom, RotationTo;
            public float ScaleFrom, ScaleTo;
            public bool FaceLeft;             // all art faces right; mirror it for the other side
            public float Delay;               // seconds before this layer starts moving
        }

        public void Build(Transform canvasRoot)
        {
            _root = UiFactory.Stretch("Cutscene", canvasRoot);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            var tint = UiFactory.Panel(_root, Color.black); // also catches the skip tap
            var skip = _root.gameObject.AddComponent<Button>();
            skip.targetGraphic = tint;
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(() => _skip = true);

            // Background fills the screen without stretching (crops the overflow).
            var bg = UiFactory.Stretch("Background", _root);
            _background = bg.gameObject.AddComponent<Image>();
            _background.raycastTarget = false;
            _background.color = new Color(0.78f, 0.8f, 0.86f); // dim the backdrop a little so the figures pop
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            var lines = UiFactory.Rect("SpeedLines", _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _speedLines = lines.gameObject.AddComponent<Image>();
            _speedLines.sprite = CutsceneArt.SpeedLines();
            _speedLines.raycastTarget = false;
            _speedLines.color = new Color(1, 1, 1, 0.35f);

            _stage = UiFactory.Stretch("Stage", _root);

            var caption = UiFactory.Rect("Caption", _root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(0, 170));
            UiFactory.Panel(caption, Palette.WithAlpha(Palette.Shadow, 0.75f), false);
            var title = UiFactory.Rect("Title", caption, new Vector2(0, 0.45f), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            _title = UiFactory.Label(title, "", 72, Palette.Accent, TextAlignmentOptions.Center, UiFactory.DisplayFont);
            var sub = UiFactory.Rect("Subtitle", caption, new Vector2(0, 0), new Vector2(1, 0.48f), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);
            _subtitle = UiFactory.Label(sub, "", 30, Palette.Chalk);

            _root.gameObject.SetActive(false);
        }

        /// <param name="showBall">KeeperDive only: true when the keeper catches it (the ball flies into the gloves).</param>
        public IEnumerator Play(CutsceneKind kind, Team attacking, Team defending, bool mirror, string title, string subtitle, Color titleColor, bool showBall = true)
        {
            _skip = false;
            _root.gameObject.SetActive(true);
            UiFactory.ClearChildren(_stage);
            _title.text = title;
            _title.color = titleColor;
            _subtitle.text = subtitle;

            var layers = Compose(kind, attacking, defending, showBall);
            _background.sprite = Load(kind == CutsceneKind.KeeperDive ? "bg_goal" : "bg_pitch",
                () => kind == CutsceneKind.KeeperDive ? CutsceneArt.GoalBackground() : CutsceneArt.PitchBackground());
            var bgFitter = _background.GetComponent<AspectRatioFitter>();
            bgFitter.aspectRatio = _background.sprite.rect.width / _background.sprite.rect.height;

            var screenHeight = ((RectTransform)_root).rect.height;
            var lineSize = Mathf.Max(((RectTransform)_root).rect.width, screenHeight) * 1.6f;
            _speedLines.rectTransform.sizeDelta = new Vector2(lineSize, lineSize);

            var images = new List<Image>();
            foreach (var layer in layers)
            {
                var rt = UiFactory.Rect("Layer", _stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var image = rt.gameObject.AddComponent<Image>();
                image.sprite = layer.Sprite;
                image.raycastTarget = false;
                var h = layer.Height * screenHeight;
                rt.sizeDelta = new Vector2(h * layer.Sprite.rect.width / layer.Sprite.rect.height, h);
                images.Add(image);
            }

            for (var t = 0f; t < Duration && !_skip; t += Time.deltaTime)
            {
                _group.alpha = Mathf.Min(1f, t / FadeIn, (Duration - t) / FadeOut);
                _speedLines.rectTransform.localRotation = Quaternion.Euler(0, 0, t * 6f);
                for (var i = 0; i < layers.Count; i++)
                {
                    var l = layers[i];
                    var p = EaseOut(Mathf.Clamp01((t - l.Delay) / (Duration - l.Delay)));
                    var pos = Vector2.LerpUnclamped(l.From, l.To, p) * screenHeight;
                    var rot = Mathf.LerpUnclamped(l.RotationFrom, l.RotationTo, p);
                    var flip = l.FaceLeft != mirror; // mirroring the scene flips every sprite once more
                    if (mirror) { pos.x = -pos.x; rot = -rot; }
                    var rt = images[i].rectTransform;
                    rt.anchoredPosition = pos;
                    rt.localRotation = Quaternion.Euler(0, 0, rot);
                    var s = Mathf.LerpUnclamped(l.ScaleFrom, l.ScaleTo, p);
                    rt.localScale = new Vector3(flip ? -s : s, s, 1);
                }
                yield return null;
            }
            _group.alpha = 0;
            _root.gameObject.SetActive(false);
        }

        /// <summary>Stops showing a scene that was interrupted (match ended mid-scene).</summary>
        public void Hide() => _root.gameObject.SetActive(false);

        /// <summary>Sine ease-out: quick start, long slow glide (the "slow-motion" feel).</summary>
        private static float EaseOut(float x) => Mathf.Sin(x * Mathf.PI * 0.5f);

        private List<Layer> Compose(CutsceneKind kind, Team attacking, Team defending, bool showBall)
        {
            var ball = Load("ball", CutsceneArt.Ball);
            switch (kind)
            {
                case CutsceneKind.KeeperDive:
                {
                    var dive = new List<Layer>
                    {
                        new Layer { Sprite = Player("keeper_dive", defending, true), Height = 0.62f,
                            From = new Vector2(-0.30f, -0.02f), To = new Vector2(-0.10f, 0.04f),
                            RotationFrom = 4, RotationTo = 10, ScaleFrom = 1f, ScaleTo = 1.06f }
                    };
                    // A save: the ball comes in from the right and ends in the keeper's gloves.
                    // Goals and wide shots show the keeper diving with nothing to catch.
                    if (showBall)
                        dive.Add(new Layer { Sprite = ball, Height = 0.17f,
                            From = new Vector2(0.75f, 0.10f), To = new Vector2(0.36f, 0.07f),
                            RotationFrom = 0, RotationTo = -300, ScaleFrom = 1.25f, ScaleTo = 1f });
                    return dive;
                }
                case CutsceneKind.Duel:
                    return new List<Layer>
                    {
                        new Layer { Sprite = Player("player_run", attacking, false), Height = 0.78f,
                            From = new Vector2(-0.75f, 0.0f), To = new Vector2(-0.16f, 0.02f),
                            RotationFrom = 0, RotationTo = 3, ScaleFrom = 1f, ScaleTo = 1.04f },
                        new Layer { Sprite = Player("player_tackle", defending, false), Height = 0.72f, FaceLeft = true,
                            From = new Vector2(0.75f, -0.06f), To = new Vector2(0.18f, -0.04f),
                            RotationFrom = 0, RotationTo = -4, ScaleFrom = 1f, ScaleTo = 1.04f },
                        new Layer { Sprite = ball, Height = 0.16f, Delay = 0.45f,
                            From = new Vector2(0.0f, -0.18f), To = new Vector2(-0.55f, -0.30f),
                            RotationFrom = 0, RotationTo = 540, ScaleFrom = 0.8f, ScaleTo = 1.6f }
                    };
                default: // LongBall
                    return new List<Layer>
                    {
                        new Layer { Sprite = ball, Height = 0.18f,
                            From = new Vector2(-0.70f, 0.05f), To = new Vector2(0.45f, 0.30f),
                            RotationFrom = 0, RotationTo = -600, ScaleFrom = 1.4f, ScaleTo = 0.7f },
                        new Layer { Sprite = Player("player_run", attacking, false), Height = 0.70f, Delay = 0.15f,
                            From = new Vector2(-0.60f, -0.22f), To = new Vector2(-0.05f, -0.16f),
                            RotationFrom = 0, RotationTo = 2, ScaleFrom = 1f, ScaleTo = 1.05f }
                    };
            }
        }

        private Sprite Player(string pose, Team team, bool keeper)
        {
            var name = $"{pose}_{(team == Team.Red ? "red" : "blue")}";
            return Load(name, () => CutsceneArt.PlayerPlaceholder(team, keeper, pose == "player_tackle"));
        }

        private Sprite Load(string name, System.Func<Sprite> placeholder)
        {
            if (_cache.TryGetValue(name, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>("Cutscenes/" + name);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>("Cutscenes/" + name);
                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            sprite = sprite != null ? sprite : placeholder();
            _cache[name] = sprite;
            return sprite;
        }
    }

    /// <summary>Generated stand-in art (and the speed-line overlay), built once at runtime.</summary>
    internal static class CutsceneArt
    {
        private static Sprite _lines;

        public static Sprite SpeedLines()
        {
            if (_lines != null) return _lines;
            const int size = 512;
            var pixels = new Color32[size * size];
            var random = new System.Random(7);
            var rays = new float[180];
            for (var i = 0; i < rays.Length; i++) rays[i] = random.NextDouble() < 0.35 ? (float)(0.25 + random.NextDouble() * 0.75) : 0f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float dx = x - size / 2f, dy = y - size / 2f;
                var r = Mathf.Sqrt(dx * dx + dy * dy) / (size / 2f);
                var angle = (Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 0.5f) * rays.Length;
                var ray = rays[Mathf.Clamp((int)angle, 0, rays.Length - 1)];
                var a = ray * Mathf.Clamp01((r - 0.3f) * 2.2f) * Mathf.Clamp01((1f - r) * 6f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            _lines = Make(pixels, size, size);
            return _lines;
        }

        public static Sprite GoalBackground()
        {
            const int w = 480, h = 270;
            var px = new Color32[w * h];
            Color top = new Color(0.08f, 0.10f, 0.18f), bottom = new Color(0.18f, 0.22f, 0.34f);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var c = Color.Lerp(bottom, top, y / (float)h);
                var net = (x + y / 3) % 22 < 2 || (y + x / 5) % 22 < 2; // skewed net grid
                if (net) c = Color.Lerp(c, Color.white, 0.55f);
                px[y * w + x] = c;
            }
            return Make(px, w, h);
        }

        public static Sprite PitchBackground()
        {
            const int w = 480, h = 270;
            var px = new Color32[w * h];
            Color sky = new Color(0.35f, 0.62f, 0.92f), skyLow = new Color(0.75f, 0.86f, 0.97f);
            Color crowd = new Color(0.22f, 0.24f, 0.32f), grass = Palette.Turf, grassFar = Palette.TurfStripe;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                Color c;
                if (y < h * 0.32f) c = Color.Lerp(grass, grassFar, y / (h * 0.32f));
                else if (y < h * 0.52f) c = Color.Lerp(crowd, crowd * 1.4f, ((x * 7 + y * 13) % 17) / 17f);
                else c = Color.Lerp(skyLow, sky, (y - h * 0.52f) / (h * 0.48f));
                px[y * w + x] = c;
            }
            return Make(px, w, h);
        }

        public static Sprite Ball()
        {
            const int s = 128;
            var px = new Color32[s * s];
            var spots = new[] { new Vector2(64, 64), new Vector2(30, 80), new Vector2(98, 82), new Vector2(42, 30), new Vector2(88, 30), new Vector2(64, 112) };
            for (var y = 0; y < s; y++)
            for (var x = 0; x < s; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(64, 64));
                if (d > 62) { px[y * s + x] = new Color32(0, 0, 0, 0); continue; }
                Color c = d > 58 ? Palette.Shadow : Color.white;
                foreach (var spot in spots)
                    if (Vector2.Distance(new Vector2(x, y), spot) < 13) c = Palette.Shadow;
                px[y * s + x] = c;
            }
            return Make(px, s, s);
        }

        /// <summary>A flat team-coloured figure: head, body and limbs as soft blobs.</summary>
        public static Sprite PlayerPlaceholder(Team team, bool keeper, bool lunging)
        {
            int w = keeper ? 320 : 220, h = keeper ? 200 : 300;
            var px = new Color32[w * h];
            var shirt = keeper ? Palette.Keeper(team) : Palette.TeamPrimary(team);
            var shorts = keeper ? Palette.Shadow : Palette.TeamSecondary(team);
            // Shapes as (centre, radius, colour) capsules sampled per pixel.
            var parts = new List<(Vector2 a, Vector2 b, float r, Color c)>();
            if (keeper)
            {
                // Horizontal dive toward +x.
                parts.Add((new Vector2(60, 95), new Vector2(170, 105), 34, shirt));            // torso
                parts.Add((new Vector2(170, 112), new Vector2(285, 140), 14, shirt));          // arms
                parts.Add((new Vector2(285, 140), new Vector2(300, 146), 16, Palette.Accent)); // gloves
                parts.Add((new Vector2(60, 90), new Vector2(20, 60), 16, shorts));             // legs
                parts.Add((new Vector2(195, 85), new Vector2(195, 85), 24, Palette.Skin));     // head
            }
            else
            {
                var lean = lunging ? 40f : 15f;
                parts.Add((new Vector2(100, 130), new Vector2(100 + lean, 210), 38, shirt));     // torso
                parts.Add((new Vector2(100, 120), new Vector2(lunging ? 190 : 70, lunging ? 40 : 30), 15, shorts)); // front leg
                parts.Add((new Vector2(100, 120), new Vector2(lunging ? 40 : 140, 40), 15, shorts)); // back leg
                parts.Add((new Vector2(100 + lean, 200), new Vector2(180, 230), 11, shirt));     // arm
                parts.Add((new Vector2(110 + lean, 255), new Vector2(110 + lean, 255), 26, Palette.Skin)); // head
            }
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var p = new Vector2(x, y);
                Color32 c = new Color32(0, 0, 0, 0);
                foreach (var (a, b, r, col) in parts)
                {
                    var d = DistanceToSegment(p, a, b);
                    if (d < r) c = d > r - 3 ? (Color32)Palette.Shadow : (Color32)col;
                }
                px[y * w + x] = c;
            }
            return Make(px, w, h);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static Sprite Make(Color32[] pixels, int w, int h)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }
    }
}
