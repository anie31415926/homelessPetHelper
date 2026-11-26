using HomelessPetHelper.Framework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Siege.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SiegePropIcon : MaskableGraphic
    {
        [SerializeField] private SceneSpritePalette _spritePalette;
        private SceneSpritePalette _defaultPalette;
        private bool _paletteCaptured;
        public void SetPalette(SceneSpritePalette palette)
        {
            if (!_paletteCaptured) { _defaultPalette = _spritePalette; _paletteCaptured = true; }
            _spritePalette = palette != null ? palette : _defaultPalette;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public override Texture mainTexture => _spritePalette == null ? base.mainTexture : _spritePalette.Texture;
        private string _visual;
        public void Show(string visual) { _visual = visual; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (string.IsNullOrEmpty(_visual)) return;
            var rect = rectTransform.rect;
            float size = Mathf.Min(rect.width, rect.height) * .43f;
            if (_visual == "paw") SiegeTokenArt.Paw(vh, rect.center, size, _spritePalette);
            else if (SiegeTokenArt.SupportsTower(_visual))
                SiegeTokenArt.Tower(vh, _visual, rect.center, size, false, _spritePalette);
            else SiegeTokenArt.Draw(vh, _visual, rect.center, size, 0, palette: _spritePalette);
        }
    }
}
