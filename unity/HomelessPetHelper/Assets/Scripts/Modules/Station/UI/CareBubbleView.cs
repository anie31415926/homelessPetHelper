using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI
{
    /// <summary>Bounded visual-only feedback; never owns care progress or game time.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CareBubbleView : MaskableGraphic
    {
        private const int Capacity = 16;
        private readonly Vector2[] _points = new Vector2[Capacity];
        private readonly float[] _life = new float[Capacity];
        private int _next;
        private float _lastContact = -1;

        public int ActiveCount { get; private set; }

        public void Contact(Vector2 point)
        {
            if (Time.unscaledTime - _lastContact < 0.035f) return;
            _lastContact = Time.unscaledTime;
            _points[_next] = point;
            _life[_next] = 0.6f;
            _next = (_next + 1) % Capacity;
            SetVerticesDirty();
        }

        public void ClearFeedback()
        {
            System.Array.Clear(_life, 0, Capacity);
            ActiveCount = 0;
            _lastContact = -1;
            SetVerticesDirty();
        }

        private void Update()
        {
            int active = 0;
            for (int index = 0; index < Capacity; index++)
            {
                if (_life[index] <= 0) continue;
                _life[index] = Mathf.Max(0, _life[index] - Time.unscaledDeltaTime);
                if (_life[index] > 0) active++;
            }

            if (active > 0 || ActiveCount > 0) SetVerticesDirty();
            ActiveCount = active;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            for (int index = 0; index < Capacity; index++)
            {
                if (_life[index] <= 0) continue;
                float age = 0.6f - _life[index];
                Vector2 center = rect.min + Vector2.Scale(rect.size, _points[index]);
                center += new Vector2((index % 3 - 1) * 10, age * 45);
                float radius = 18 + index % 4 * 4 + age * 8;
                var tint = new Color32(72, 173, 197, (byte)(220 * _life[index] / 0.6f));
                AddRing(mesh, center, radius, tint);
            }
        }

        private static void AddRing(VertexHelper mesh, Vector2 center, float radius, Color32 tint)
        {
            int start = mesh.currentVertCount;
            for (int segment = 0; segment < 16; segment++)
            {
                float angle = segment * Mathf.PI / 8;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + direction * radius, tint, Vector2.zero);
                mesh.AddVert(center + direction * (radius - 3), tint, Vector2.zero);
            }

            for (int segment = 0; segment < 16; segment++)
            {
                int current = start + segment * 2;
                int next = start + (segment + 1) % 16 * 2;
                mesh.AddTriangle(current, next, current + 1);
                mesh.AddTriangle(next, next + 1, current + 1);
            }
        }

        protected override void OnDisable()
        {
            ClearFeedback();
            base.OnDisable();
        }
    }
}
