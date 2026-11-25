using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Outing.UI
{
    /// <summary>One map-space fog sheet with connected, soft-edged reveal regions.</summary>
    public sealed class CityMapFog : MaskableGraphic
    {
        private const int Columns = 48;
        private const int Rows = 72;
        private const float TransitionSeconds = 1.15f;
        private const float SampleInterval = 1f / 24f;
        private static readonly Color LowCloud = new Color32(159, 194, 194, 255);
        private static readonly Color HighCloud = new Color32(252, 251, 234, 255);

        [SerializeField] private Vector2 _artSize = new Vector2(673, 1024);
        [SerializeField] private Vector2[] _startingRegion;
        [SerializeField] private Vector2[] _previewRegion;
        [SerializeField] private bool _previewRevealed;

        private struct Sample
        {
            public float Starter;
            public float Extended;
            public Color Tint;
        }

        private Sample[] _samples;
        private float _reveal;
        private float _sampleAge;

        // Presentation-only preview. The production unlock rules and save data remain unchanged.
        public bool PreviewRevealed
        {
            get => _previewRevealed;
            set => _previewRevealed = value;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _reveal = _previewRevealed ? 1 : 0;
            _sampleAge = 0;
        }

        internal void Advance(float elapsed)
        {
            float target = _previewRevealed ? 1 : 0;
            if (_reveal == target) return;
            _sampleAge += elapsed;
            if (_sampleAge < SampleInterval && Mathf.Abs(target - _reveal) > elapsed / TransitionSeconds) return;
            _reveal = Mathf.MoveTowards(_reveal, target, _sampleAge / TransitionSeconds);
            _sampleAge = 0;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_startingRegion == null || _startingRegion.Length < 3 ||
                _previewRegion == null || _previewRegion.Length < 3) return;
            if (_samples == null) CacheSamples();

            Rect bounds = rectTransform.rect;
            float reveal = Mathf.SmoothStep(0, 1, Application.isPlaying ? _reveal : (_previewRevealed ? 1 : 0));
            for (int y = 0; y <= Rows; y++)
            {
                for (int x = 0; x <= Columns; x++)
                {
                    int i = y * (Columns + 1) + x;
                    Sample sample = _samples[i];
                    Color tint = sample.Tint;
                    tint.a = Mathf.Lerp(sample.Starter, sample.Extended, reveal);
                    mesh.AddVert(new Vector3(bounds.xMin + bounds.width * x / Columns,
                        bounds.yMin + bounds.height * y / Rows), tint, Vector2.zero);
                    if (x == 0 || y == 0) continue;
                    int bottomLeft = i - Columns - 2;
                    int bottomRight = i - Columns - 1;
                    int topLeft = i - 1;
                    if (Visible(bottomLeft) || Visible(bottomRight) || Visible(topLeft) || Visible(i))
                    {
                        mesh.AddTriangle(bottomLeft, topLeft, i);
                        mesh.AddTriangle(bottomLeft, i, bottomRight);
                    }
                }
            }

            bool Visible(int i) => Mathf.Lerp(_samples[i].Starter, _samples[i].Extended, reveal) > .001f;
        }

        private void CacheSamples()
        {
            _samples = new Sample[(Columns + 1) * (Rows + 1)];
            for (int y = 0; y <= Rows; y++)
            {
                for (int x = 0; x <= Columns; x++)
                {
                    var p = new Vector2(_artSize.x * x / Columns, _artSize.y * (1 - (float)y / Rows));
                    float broad = Mathf.PerlinNoise(p.x / 147f + 17, p.y / 116f + 11);
                    float detail = Mathf.PerlinNoise(p.x / 55f + 37, p.y / 54f + 3);
                    float mottling = .72f * broad + .28f * detail;
                    float edge = (mottling - .5f) * 30;
                    float initial = SignedDistance(p, _startingRegion);
                    float extension = SignedDistance(p, _previewRegion);
                    _samples[y * (Columns + 1) + x] = new Sample
                    {
                        Starter = Opacity(initial, edge),
                        Extended = Opacity(Mathf.Max(initial, extension), edge),
                        Tint = Color.Lerp(LowCloud, HighCloud, Mathf.SmoothStep(0, 1, (mottling - .15f) / .73f))
                    };
                }
            }
        }

        private static float Opacity(float distance, float noise) =>
            Mathf.SmoothStep(0, 1, (-distance + noise) / 54f);

        private static float SignedDistance(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            float nearest = float.PositiveInfinity;
            Vector2 a = polygon[polygon.Length - 1];
            foreach (Vector2 b in polygon)
            {
                Vector2 edge = b - a;
                Vector2 fromA = point - a;
                if (edge.sqrMagnitude > .0001f)
                {
                    float t = Mathf.Clamp01(Vector2.Dot(fromA, edge) / edge.sqrMagnitude);
                    nearest = Mathf.Min(nearest, (fromA - t * edge).sqrMagnitude);
                }
                if ((a.y > point.y) != (b.y > point.y) && point.x < a.x + edge.x * fromA.y / edge.y)
                    inside = !inside;
                a = b;
            }
            return Mathf.Sqrt(nearest) * (inside ? 1 : -1);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            _samples = null;
            base.OnValidate();
        }
#endif
    }
}
