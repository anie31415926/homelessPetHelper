using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Outing.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class CityMapFlowerSway : BaseMeshEffect
    {
        private const int Steps = 8;
        private float _time;

        internal RectTransform Bed => graphic.rectTransform;
        internal bool IsCulled => graphic.canvasRenderer.cull;

        internal void Sample(float time)
        {
            if (Mathf.Approximately(_time, time)) return;
            _time = time;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount != 4) return;

            var first = new UIVertex();
            var opposite = new UIVertex();
            mesh.PopulateUIVertex(ref first, 0);
            mesh.PopulateUIVertex(ref opposite, 2);
            float width = opposite.position.x - first.position.x;
            mesh.Clear();
            for (int row = 0; row <= Steps; row++)
            {
                float y = (float)row / Steps;
                float bend = Mathf.SmoothStep(0, 1, y) * .022f * width;
                for (int column = 0; column <= Steps; column++)
                {
                    float x = (float)column / Steps;
                    var vertex = first;
                    vertex.position = new Vector3(Mathf.Lerp(first.position.x, opposite.position.x, x) +
                        bend * Mathf.Sin(2 * Mathf.PI * _time / 4 + 3.4f * x),
                        Mathf.Lerp(first.position.y, opposite.position.y, y));
                    vertex.uv0 = new Vector2(Mathf.Lerp(first.uv0.x, opposite.uv0.x, x),
                        Mathf.Lerp(first.uv0.y, opposite.uv0.y, y));
                    mesh.AddVert(vertex);
                    if (row == 0 || column == 0) continue;
                    int i = row * (Steps + 1) + column;
                    mesh.AddTriangle(i - Steps - 2, i - 1, i);
                    mesh.AddTriangle(i - Steps - 2, i, i - Steps - 1);
                }
            }
        }
    }
}
