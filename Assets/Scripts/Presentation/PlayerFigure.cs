using System.Collections;
using FootballBoard.Domain;
using TMPro;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>Tabletop miniature for one player. Purely visual; the engine owns its logical cell.</summary>
    public sealed class PlayerFigure : MonoBehaviour
    {
        public const float FigureScale = 1.5f;
        private Transform _body;
        private SelectionHalo _selectionHalo;
        private RadarRing _readyRing;
        private GameObject _lockedRing;
        private float _phase;

        public int PlayerId { get; private set; }
        public Team Team { get; private set; }
        public bool IsMoving { get; private set; }

        public static PlayerFigure Create(Player player, MaterialKit kit, Transform parent, Quaternion cameraRotation)
        {
            var go = new GameObject($"{player.Team} #{player.Number}");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * FigureScale;
            var figure = go.AddComponent<PlayerFigure>();
            figure.Build(player, kit, cameraRotation);
            return figure;
        }

        private void Build(Player player, MaterialKit kit, Quaternion cameraRotation)
        {
            PlayerId = player.Id;
            Team = player.Team;
            _phase = player.Id * 0.73f;

            var shirt = player.IsGoalkeeper ? Palette.Keeper(player.Team) : Palette.TeamPrimary(player.Team);
            var shorts = Palette.TeamSecondary(player.Team);

            // Selected: spinning arcs in the team colour. Ready (can be picked this turn): a radar-swept donut.
            _selectionHalo = SelectionHalo.Create(transform, kit, Palette.TeamPrimary(player.Team), 0.014f);
            _readyRing = RadarRing.Create(transform, 0.009f);
            _lockedRing = kit.Primitive(PrimitiveType.Cylinder, "Locked", transform, Palette.WithAlpha(Palette.Shadow, 0.4f), new Vector3(0, 0.011f, 0), new Vector3(0.43f, 0.011f, 0.43f), true);
            _lockedRing.SetActive(false);

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            var hasModel = BuildFromModel(player, kit, shirt, shorts);
            if (!hasModel) BuildPrimitives(player, kit, shirt, shorts);

            // Shirt number floating above the head, facing the camera.
            var label = new GameObject("Number");
            label.transform.SetParent(_body, false);
            label.transform.localPosition = new Vector3(0, hasModel ? FigureHeight + 0.3f : 0.88f, 0);
            label.transform.rotation = cameraRotation;
            var tmp = label.AddComponent<TextMeshPro>();
            tmp.text = player.Number.ToString();
            tmp.fontSize = 3.2f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = shorts;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Palette.Shadow;
            tmp.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
        }

        // ------------------------------------------------------------------ 3D model

        /// <summary>The low-poly player (Resources/Models/player.glb, imported by glTFast); null if absent.</summary>
        private static GameObject _model;
        private static bool _modelLoaded;
        private const float ModelHeight = 1.85f;   // model units, feet to hair
        private const float FigureHeight = 1.0f;   // body height of the piece, in figure units
        private const float FigureBreadth = 1.35f; // widen the slim model so it reads from the top-down camera

        private static readonly Color Hair = new Color32(0x3B, 0x2A, 0x1E, 0xFF);
        private static readonly Color Glove = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        private static readonly Color Boot = new Color32(0x26, 0x26, 0x2A, 0xFF);

        private bool BuildFromModel(Player player, MaterialKit kit, Color shirt, Color shorts)
        {
            if (!_modelLoaded)
            {
                _modelLoaded = true;
                _model = Resources.Load<GameObject>("Models/player");
            }
            if (_model == null) return false;

            var model = Instantiate(_model, _body, false);
            model.name = "Model";
            var scale = FigureHeight / ModelHeight;
            model.transform.localScale = new Vector3(scale * FigureBreadth, scale, scale * FigureBreadth);
            // The model faces +Z; turn it toward the goal this team attacks (Red attacks +X).
            model.transform.localRotation = Quaternion.Euler(0, player.Team == Team.Red ? 90f : -90f, 0);

            var keeperShorts = Palette.Shadow;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                // Meshes are named "<Joint>_<material>", e.g. "Torso_shirt" or "HandL_skin".
                var name = renderer.gameObject.name;
                var part = name.Substring(name.LastIndexOf('_') + 1);
                var isHand = name.StartsWith("Hand");
                var color = part switch
                {
                    "shirt" => shirt,
                    "sock" => shirt,
                    "bootTrim" => shirt,
                    "shorts" => player.IsGoalkeeper ? keeperShorts : shorts,
                    "band" => player.IsGoalkeeper ? keeperShorts : shorts,
                    "skin" => player.IsGoalkeeper && isHand ? Glove : Palette.Skin,
                    "hair" => Hair,
                    "boot" => Boot,
                    "white" => Palette.Chalk,
                    "lip" => Palette.Skin * 0.85f,
                    _ => Palette.Shadow,
                };
                color.a = 1f;
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++) materials[i] = kit.Solid(color);
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return true;
        }

        private void BuildPrimitives(Player player, MaterialKit kit, Color shirt, Color shorts)
        {
            // The simple fallback figure stands on a team-coloured disc so it reads from above
            // (the 3D model needs none: its kit shows the team).
            kit.Primitive(PrimitiveType.Cylinder, "Base", transform, Palette.TeamPrimary(player.Team) * 0.7f + Palette.Shadow * 0.3f,
                new Vector3(0, 0.025f, 0), new Vector3(0.36f, 0.025f, 0.36f));
            // Legs (slight taper and splay), shorts, torso, head, hair.
            var legL = kit.Primitive(PrimitiveType.Capsule, "LegL", _body, Palette.Shadow, new Vector3(-0.055f, 0.13f, 0), new Vector3(0.075f, 0.09f, 0.075f));
            legL.transform.localRotation = Quaternion.Euler(0, 0, -4f);
            var legR = kit.Primitive(PrimitiveType.Capsule, "LegR", _body, Palette.Shadow, new Vector3(0.055f, 0.13f, 0), new Vector3(0.075f, 0.09f, 0.075f));
            legR.transform.localRotation = Quaternion.Euler(0, 0, 4f);
            kit.Primitive(PrimitiveType.Sphere, "Shorts", _body, shorts, new Vector3(0, 0.25f, 0), new Vector3(0.24f, 0.13f, 0.18f));
            kit.Primitive(PrimitiveType.Capsule, "Torso", _body, shirt, new Vector3(0, 0.39f, 0), new Vector3(0.25f, 0.15f, 0.19f));
            kit.Primitive(PrimitiveType.Sphere, "Head", _body, Palette.Skin, new Vector3(0, 0.6f, 0), new Vector3(0.2f, 0.2f, 0.2f));
            kit.Primitive(PrimitiveType.Sphere, "Hair", _body, Palette.Shadow, new Vector3(0, 0.655f, -0.015f), new Vector3(0.21f, 0.12f, 0.2f));
            if (player.IsGoalkeeper)
            {
                kit.Primitive(PrimitiveType.Sphere, "GloveL", _body, Palette.Chalk, new Vector3(-0.17f, 0.4f, 0), Vector3.one * 0.09f);
                kit.Primitive(PrimitiveType.Sphere, "GloveR", _body, Palette.Chalk, new Vector3(0.17f, 0.4f, 0), Vector3.one * 0.09f);
            }
        }

        public void SetSelected(bool selected) => _selectionHalo.gameObject.SetActive(selected);

        /// <summary>Grey donut: this player belongs to the planning team and can be selected.</summary>
        public void SetReady(bool ready) => _readyRing.gameObject.SetActive(ready);

        public void SetLocked(bool locked) => _lockedRing.SetActive(locked);

        public void Place(Vector3 world)
        {
            StopAllCoroutines();
            IsMoving = false;
            transform.position = world;
        }

        public IEnumerator MoveTo(Vector3 world, float duration)
        {
            IsMoving = true;
            var start = transform.position;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / duration);
                transform.position = Vector3.Lerp(start, world, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.15f;
                yield return null;
            }
            transform.position = world;
            IsMoving = false;
        }

        public IEnumerator Shake(float duration)
        {
            var start = transform.position;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                transform.position = start + new Vector3(Mathf.Sin(t * 60f) * 0.05f, 0, 0);
                yield return null;
            }
            transform.position = start;
        }

        private void Update()
        {
            // Idle breathing: ±2% height bob at ~0.6 Hz.
            if (_body != null)
                _body.localScale = new Vector3(1f, 1f + Mathf.Sin((Time.time + _phase) * Mathf.PI * 1.2f) * 0.02f, 1f);
        }
    }
}
