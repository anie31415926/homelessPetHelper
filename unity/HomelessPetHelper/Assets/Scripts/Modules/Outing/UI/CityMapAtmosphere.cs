using System;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Outing.UI
{
    /// <summary>Decorative map-space art. The presenter remains the authority for point availability.</summary>
    public sealed class CityMapAtmosphere : MaskableGraphic
    {
        private const float SampleInterval = 1f / 24f;
        private const float LoopSeconds = 120;
        private static readonly Rect CloudUv = new Rect(0, 0, .75f, 1);
        private static readonly Rect BeeUv = new Rect(.75f, .5f, .25f, .5f);

        [SerializeField] private Texture2D _page;
        [SerializeField] private CityMapFlowerSway _flower;

        private float _time;
        private float _sampleAge;
        private bool _flowerLocked;

        public override Texture mainTexture => _page != null ? _page : base.mainTexture;

        protected override void Awake()
        {
            base.Awake();
            if (_page == null || _flower == null || raycastTarget)
                throw new InvalidOperationException("City map atmosphere has incomplete visual bindings.");
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _time = 0;
            _sampleAge = 0;
            if (_flower != null) _flower.Sample(0);
        }

        internal void SetFlowerLocked(bool locked)
        {
            if (_flowerLocked == locked) return;
            _flowerLocked = locked;
            SetVerticesDirty();
        }

        internal void Advance(float elapsed)
        {
            _sampleAge += elapsed;
            if (_sampleAge < SampleInterval) return;
            _time = (_time + _sampleAge) % LoopSeconds;
            _sampleAge = 0;
            if (!_flowerLocked && !_flower.IsCulled) _flower.Sample(_time);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_page == null || _flower == null) return;
            Rect map = rectTransform.rect;
            float width = map.width;
            float drift = Phase(30);
            Cloud(mesh, MapPosition(map, -.025f + .027f * Mathf.Sin(drift), .73f), width * .29f, .72f);
            Cloud(mesh, MapPosition(map, .98f + .045f * Mathf.Sin(drift + 2), .98f), width * .26f, .70f);
            Cloud(mesh, MapPosition(map, -.018f - .035f * Mathf.Sin(drift + 1), .23f), width * .25f, .62f);

            if (!_flowerLocked) DrawBee(mesh);
        }

        private void DrawBee(VertexHelper mesh)
        {
            RectTransform bed = _flower.Bed;
            float angle = Phase(8);
            Vector2 center = bed.rect.center + new Vector2(
                Mathf.Sin(angle) * bed.rect.width * .30f,
                (.21f + .22f * Mathf.Sin(2 * angle + .8f)) * bed.rect.height);
            float size = bed.rect.width * .153f;
            float bob = 1 + .06f * Mathf.Sin(Phase(.25f));
            Quad(mesh, ToLocal(bed, center), new Vector2(size, size * bob), BeeUv, Color.white,
                Mathf.Cos(angle) < 0, Mathf.Sin(angle) * .23f);
        }

        private void Cloud(VertexHelper mesh, Vector2 center, float width, float opacity) =>
            Quad(mesh, center, new Vector2(width, width * (2f / 3f)), CloudUv,
                new Color(1, 1, 1, opacity), false, 0);

        private Vector2 ToLocal(RectTransform source, Vector2 position) =>
            rectTransform.InverseTransformPoint(source.TransformPoint(position));

        private static Vector2 MapPosition(Rect rect, float x, float y) =>
            rect.min + Vector2.Scale(rect.size, new Vector2(x, y));

        private float Phase(float period) => _time * (2 * Mathf.PI / period);

        private static void Quad(VertexHelper mesh, Vector2 center, Vector2 size, Rect uv, Color color,
            bool flip, float angle)
        {
            int first = mesh.currentVertCount;
            Vector2 right = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (size.x * .5f);
            Vector2 up = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * (size.y * .5f);
            float leftU = flip ? uv.xMax : uv.xMin;
            float rightU = flip ? uv.xMin : uv.xMax;
            mesh.AddVert(center - right - up, color, new Vector2(leftU, uv.yMin));
            mesh.AddVert(center - right + up, color, new Vector2(leftU, uv.yMax));
            mesh.AddVert(center + right + up, color, new Vector2(rightU, uv.yMax));
            mesh.AddVert(center + right - up, color, new Vector2(rightU, uv.yMin));
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
