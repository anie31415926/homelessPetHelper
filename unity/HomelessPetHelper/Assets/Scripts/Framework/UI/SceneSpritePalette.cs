using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>A single static texture page for sprites and solid-color geometry in one UI mesh.</summary>
    public sealed class SceneSpritePalette : ScriptableObject
    {
        [SerializeField] private Texture2D _texture;
        [SerializeField] private Sprite[] _sprites = Array.Empty<Sprite>();
        private Dictionary<string, Sprite> _lookup;
        public Texture Texture => _texture;

        public bool TryGet(string id, out Sprite sprite)
        {
            if (_lookup == null) BuildLookup();
            sprite = null;
            return id != null && _lookup.TryGetValue(id, out sprite);
        }

        private void BuildLookup()
        {
            var lookup = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var sprite in _sprites)
            {
                if (sprite == null || sprite.texture != _texture || lookup.ContainsKey(sprite.name))
                    throw new InvalidOperationException("Missing, duplicate or foreign scene sprite.");
                lookup.Add(sprite.name, sprite);
            }
            _lookup = lookup;
        }

        private void OnEnable() => _lookup = null;
        private void OnValidate() => _lookup = null;

        public bool TryDraw(VertexHelper mesh, string id, Vector2 center, Vector2 bounds,
            float angle = 0, Color? tint = null)
        {
            if (!TryGet(id, out var sprite)) return false;
            var source = sprite.textureRect;
            float scale = Mathf.Min(bounds.x / source.width, bounds.y / source.height);
            Vector2 half = source.size * (scale / 2);
            var uv = new Rect(source.x / _texture.width, source.y / _texture.height,
                source.width / _texture.width, source.height / _texture.height);
            int start = mesh.currentVertCount;
            float sine = Mathf.Sin(angle), cosine = Mathf.Cos(angle);
            Color color = tint ?? Color.white;
            AddVertex(mesh, center, new Vector2(-half.x, -half.y), new Vector2(uv.xMin, uv.yMin), sine, cosine, color);
            AddVertex(mesh, center, new Vector2(-half.x, half.y), new Vector2(uv.xMin, uv.yMax), sine, cosine, color);
            AddVertex(mesh, center, new Vector2(half.x, half.y), new Vector2(uv.xMax, uv.yMax), sine, cosine, color);
            AddVertex(mesh, center, new Vector2(half.x, -half.y), new Vector2(uv.xMax, uv.yMin), sine, cosine, color);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
            return true;
        }

        private static void AddVertex(VertexHelper mesh, Vector2 center, Vector2 offset, Vector2 uv,
            float sine, float cosine, Color color)
        {
            var position = center + new Vector2(offset.x * cosine - offset.y * sine,
                offset.x * sine + offset.y * cosine);
            mesh.AddVert(position, color, uv);
        }
    }
}
