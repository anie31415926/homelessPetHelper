using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Siege.UI
{
    /// <summary>A small outlined supply token. Its visible HUD label owns the resource meaning.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SiegeResourceGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            float size = Mathf.Min(rect.width, rect.height);
            Vector2 center = rect.center;
            Quad(mesh, center, size, new Vector2(.10f, .08f), new Vector2(.90f, .82f), color);
            Quad(mesh, center, size, new Vector2(.20f, .18f), new Vector2(.80f, .71f),
                new Color32(244, 215, 155, 255));
            Quad(mesh, center, size, new Vector2(.30f, .84f), new Vector2(.70f, .98f), color);
            Quad(mesh, center, size, new Vector2(.43f, .33f), new Vector2(.57f, .61f), color);
        }
        private static void Quad(VertexHelper mesh, Vector2 center, float size, Vector2 min, Vector2 max,
            Color tint)
        {
            int first = mesh.currentVertCount;
            Vector2 origin = center - Vector2.one * size / 2;
            mesh.AddVert(origin + min * size, tint, Vector2.zero);
            mesh.AddVert(origin + new Vector2(min.x, max.y) * size, tint, Vector2.zero);
            mesh.AddVert(origin + max * size, tint, Vector2.zero);
            mesh.AddVert(origin + new Vector2(max.x, min.y) * size, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
