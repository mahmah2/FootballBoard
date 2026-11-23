using System.Collections.Generic;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>Palette-tinted copies of the two template materials, cached per colour.</summary>
    public sealed class MaterialKit
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly Material _opaque;
        private readonly Material _transparent;
        private readonly Dictionary<(Color, bool), Material> _cache = new Dictionary<(Color, bool), Material>();

        public MaterialKit(Material opaque, Material transparent)
        {
            _opaque = opaque;
            _transparent = transparent;
        }

        public Material Solid(Color color) => Get(color, false);

        public Material Glass(Color color) => Get(color, true);

        private Material Get(Color color, bool transparent)
        {
            if (_cache.TryGetValue((color, transparent), out var m)) return m;
            m = new Material(transparent ? _transparent : _opaque) { name = (transparent ? "Glass " : "Solid ") + ColorUtility.ToHtmlStringRGBA(color) };
            m.SetColor(BaseColor, color);
            m.color = color;
            _cache[(color, transparent)] = m;
            return m;
        }

        /// <summary>A primitive with a palette material and no collider.</summary>
        public GameObject Primitive(PrimitiveType type, string name, Transform parent, Color color, Vector3 localPos, Vector3 localScale, bool transparent = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = transparent ? Glass(color) : Solid(color);
            return go;
        }
    }
}
