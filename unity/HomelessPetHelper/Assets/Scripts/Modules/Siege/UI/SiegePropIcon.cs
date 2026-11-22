using HomelessPetHelper.Framework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Siege.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SiegePropIcon : MaskableGraphic
    {
        [SerializeField] private SceneSpritePalette _spritePalette;
        public override Texture mainTexture => _spritePalette == null ? base.mainTexture : _spritePalette.Texture;
        private string _visual;
        public void Show(string visual) { _visual = visual; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (string.IsNullOrEmpty(_visual)) return;
            SiegeTokenArt.Draw(vh, _visual, rectTransform.rect.center,
                Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .43f,
                0, palette: _spritePalette);
        }
    }
}
