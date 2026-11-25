using HomelessPetHelper.Framework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Siege.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SiegeCarryOverlay : MaskableGraphic
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
        private SiegeUnitDefinition _definition;
        private SiegeState.Unit _unit;
        private float _radius;
        public Vector2 MouthPoint => Vector2.up * rectTransform.rect.height * (6f / 78);

        internal void Show(SiegeCatalog catalog, SiegeState.Unit unit, float propScale)
        {
            _unit = unit;
            _definition = unit == null ? null : catalog.Unit(unit.DefinitionId);
            _radius = _definition == null ? 0
                : (string.IsNullOrEmpty(_definition.UpgradeId) ? 11 : 8) * propScale;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_definition == null) return;
            float pose = (float)(_unit.CarryFromDistance / 8 + _unit.Id * .7);
            SiegeTokenArt.Draw(vh, _definition.Icon, MouthPoint, _radius, pose, _unit.Brace > 0, _spritePalette);
        }
    }
}
