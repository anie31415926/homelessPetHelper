using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Siege.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SiegeResourceGraphic : MaskableGraphic
    {
        internal enum Symbol { Supply, Damage, Cadence, Range, Durability }

        [SerializeField] private Symbol _symbol;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            SiegeTokenArt.DrawSymbol(mesh, _symbol, rect.center, Mathf.Min(rect.width, rect.height) * .46f, color);
        }
    }
}
