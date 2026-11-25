using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI
{
    /// <summary>One batched, clipped particle layer for both care tools.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CareBubbleView : MaskableGraphic
    {
        [SerializeField] private Texture2D _atlas;
        private const int Capacity = 18;
        private const float Lifetime = 0.68f;
        private readonly Vector2[] _points = new Vector2[Capacity];
        private readonly float[] _life = new float[Capacity];
        private readonly byte[] _kind = new byte[Capacity];
        private readonly byte[] _variant = new byte[Capacity];
        private int _next;
        private float _lastContact = -1;

        public int ActiveCount { get; private set; }
        public override Texture mainTexture => _atlas != null ? _atlas : base.mainTexture;

        public void Contact(Vector2 point, bool grooming = false)
        {
            if (Time.unscaledTime - _lastContact < 0.09f) return;
            _lastContact = Time.unscaledTime;
            Emit(point, grooming ? (byte)2 : (byte)0, 0);
            if (_atlas != null)
            {
                Emit(point, grooming ? (byte)2 : (byte)0, 1);
                Emit(point, grooming ? (byte)1 : (byte)0, 2);
            }
            SetVerticesDirty();
        }

        private void Emit(Vector2 point, byte kind, byte variant)
        {
            if (_life[_next] <= 0) ActiveCount++;
            _points[_next] = point;
            _life[_next] = Lifetime;
            _kind[_next] = kind;
            _variant[_next] = variant;
            _next = (_next + 1) % Capacity;
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
                float progress = 1 - _life[index] / Lifetime;
                Vector2 center = rect.min + Vector2.Scale(rect.size, _points[index]);
                if (_atlas == null)
                {
                    center.y += progress * 27;
                    AddRing(mesh, center, 18 + progress * 7,
                        new Color32(72, 173, 197, (byte)(220 * (1 - progress))));
                }
                else AddParticle(mesh, rect, center, progress, index);
            }
        }

        private void AddParticle(VertexHelper mesh, Rect rect, Vector2 center, float progress, int index)
        {
            int variant = _variant[index];
            int sign = variant == 1 ? -1 : 1;
            bool tuft = _kind[index] == 2, sparkle = _kind[index] == 1;
            float radius = (variant == 0 ? 48 : variant == 1 ? 30 : 21) *
                (.72f + Mathf.Min(1, progress * 6) * .28f);
            float offset = variant == 1 ? -41 : variant == 2 ? 44 : 2;
            float drift = (tuft ? 110 : 44) * progress * sign;
            center += new Vector2(drift + offset + Mathf.Sin(progress * 6) * 5,
                (variant == 1 ? 34 : variant == 2 ? 21 : 1) +
                (tuft ? 183 : 140) * progress - (tuft ? 161 * progress * progress : 0));
            float angle = tuft ? (0.25f - progress * 1.25f) * sign : sparkle ? progress / 2 : 0;
            float margin = radius * (tuft ? 1.42f : 1.08f);
            if (rect.width <= margin * 2 || rect.height <= margin * 2) return;
            center.x = Mathf.Clamp(center.x, rect.xMin + margin, rect.xMax - margin);
            center.y = Mathf.Clamp(center.y, rect.yMin + margin, rect.yMax - margin);
            var tint = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01((1 - progress) * 2.7f)));
            Vector2 corner = new Vector2(sparkle ? .5f : 0, tuft ? 0 : .5f);
            AddQuad(mesh, center, radius, angle, corner, tint);
        }

        private static void AddQuad(VertexHelper mesh, Vector2 center, float radius, float angle,
            Vector2 uv, Color32 tint)
        {
            int start = mesh.currentVertCount;
            var right = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            var up = new Vector2(-right.y, right.x);
            const float pad = 1f / 256f;
            const float edge = .5f - pad;
            mesh.AddVert(center - right - up, tint, uv + new Vector2(pad, pad));
            mesh.AddVert(center - right + up, tint, uv + new Vector2(pad, edge));
            mesh.AddVert(center + right + up, tint, uv + new Vector2(edge, edge));
            mesh.AddVert(center + right - up, tint, uv + new Vector2(edge, pad));
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
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
