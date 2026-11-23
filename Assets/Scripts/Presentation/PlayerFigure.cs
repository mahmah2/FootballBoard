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
        private GameObject _selectionRing;
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

            // Base disc in the team colour so pieces read from above.
            kit.Primitive(PrimitiveType.Cylinder, "Base", transform, Palette.TeamPrimary(player.Team) * 0.7f + Palette.Shadow * 0.3f,
                new Vector3(0, 0.025f, 0), new Vector3(0.36f, 0.025f, 0.36f));
            _selectionRing = kit.Primitive(PrimitiveType.Cylinder, "Selection", transform, Palette.Accent, new Vector3(0, 0.012f, 0), new Vector3(0.46f, 0.012f, 0.46f));
            _selectionRing.SetActive(false);
            _lockedRing = kit.Primitive(PrimitiveType.Cylinder, "Locked", transform, Palette.Shadow, new Vector3(0, 0.011f, 0), new Vector3(0.43f, 0.011f, 0.43f));
            _lockedRing.SetActive(false);

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
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

            // Shirt number floating above the head, facing the camera.
            var label = new GameObject("Number");
            label.transform.SetParent(_body, false);
            label.transform.localPosition = new Vector3(0, 0.88f, 0);
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

        public void SetSelected(bool selected) => _selectionRing.SetActive(selected);

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
