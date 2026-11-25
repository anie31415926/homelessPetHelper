using System;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Animals.UI
{
    /// <summary>Read-only coat presentation on an authored Image or RawImage; owns all derived GPU resources.</summary>
    public sealed class AnimalCoatView : MonoBehaviour
    {
        [SerializeField] private Graphic _target;
        [SerializeField] private Shader _shader;
        private AnimalSnapshot _snapshot;
        private Material _material, _previous;
        private Texture2D _texture;
        private Sprite _sprite;
        private Rect _rawUv;
        private Guid _animalId;
        private int _dirt = -1, _fur = -1;
        public AnimalSnapshot Snapshot => _snapshot;
        public bool HasResources => _material != null || _texture != null;

        internal void Bind(Graphic target, Shader shader)
        {
            _target = target;
            _shader = shader;
        }

        public void Show(AnimalSnapshot snapshot)
        {
            if (snapshot != null && snapshot.Coat == null) throw new ArgumentException("Explicit coat required.");
            _snapshot = snapshot;
            if (isActiveAndEnabled) Refresh();
        }

        public void SetFrameUv(RawImage image, Rect uv)
        {
            if (image == null || image != _target) throw new ArgumentException("Unexpected coat target.");
            image.uvRect = uv;
            if (_material != null) RefreshUv();
        }

        private void OnEnable() { if (_snapshot != null) Refresh(); }
        private void OnDisable() => Release();
        private void OnDestroy() => Release();
        private void LateUpdate() { if (_material != null) RefreshUv(); }

        private void Refresh()
        {
            if (_snapshot == null) { Release(); return; }
            if (_target == null || _shader == null) throw new InvalidOperationException("Missing coat view binding.");
            if (_material == null)
            {
                _previous = _target.material;
                _material = new Material(_shader) { name = "Individual coat presentation" };
                _target.material = _material;
                _texture = new Texture2D(AnimalCoatPattern.Resolution, AnimalCoatPattern.Resolution,
                    TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp, name = "Individual coat marks" };
                _material.SetTexture("_CoatTex", _texture);
                _dirt = _fur = -1;
                _sprite = null;
            }
            var coat = _snapshot.Coat;
            if (_animalId != _snapshot.AnimalId || _dirt != coat.Dirtiness || _fur != coat.LooseFur)
            {
                _animalId = _snapshot.AnimalId; _dirt = coat.Dirtiness; _fur = coat.LooseFur;
                int seed = AnimalCoatPattern.Seed(_animalId), size = AnimalCoatPattern.Resolution;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        pixels[y * size + x] = new Color32(AnimalCoatPattern.Sample(x, y, seed, false, _dirt),
                            AnimalCoatPattern.Sample(x, y, seed, true, _fur), 0, 255);
                _texture.SetPixels32(pixels); _texture.Apply(false, false);
            }
            RefreshUv();
        }

        private void RefreshUv()
        {
            Vector4 uv;
            if (_target is Image image)
            {
                var sprite = image.overrideSprite;
                if (sprite == _sprite && _sprite != null) return;
                _sprite = sprite;
                uv = sprite == null ? new Vector4(0, 0, 1, 1) : DataUtility.GetOuterUV(sprite);
            }
            else if (_target is RawImage raw)
            {
                if (_rawUv == raw.uvRect && _rawUv.width != 0) return;
                _rawUv = raw.uvRect;
                // Normalize within the sampled cell, preserving the texture's mirror direction.
                uv = new Vector4(Mathf.Min(_rawUv.xMin, _rawUv.xMax), Mathf.Min(_rawUv.yMin, _rawUv.yMax),
                    Mathf.Max(_rawUv.xMin, _rawUv.xMax), Mathf.Max(_rawUv.yMin, _rawUv.yMax));
            }
            else throw new InvalidOperationException("Unsupported coat target.");
            _material.SetVector("_UvRect", uv);
        }

        private void Release()
        {
            if (_material != null)
            {
                if (_target != null && _target.material == _material) _target.material = _previous;
                Destroy(_material); _material = null;
            }
            if (_texture != null) { Destroy(_texture); _texture = null; }
            _sprite = null; _rawUv = default;
        }
    }
}
