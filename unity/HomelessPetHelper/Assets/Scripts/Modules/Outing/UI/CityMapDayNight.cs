using System;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Outing.UI
{
    /// <summary>Applies the action clock to map art only. Never advances the clock.</summary>
    public sealed class CityMapDayNight : MaskableGraphic
    {
        private const float DayMinutes = 1440;
        private const float OpeningMinute = 480;
        private const float TransitionSeconds = .8f;
        private const int LampSides = 16;

        private static readonly Palette Day = new Palette(Color.white, Color.white, 0);
        private static readonly Palette Dawn = new Palette(new Color(1f, .87f, .72f),
            new Color(1f, .92f, .84f), .18f);
        private static readonly Palette Dusk = new Palette(new Color(1f, .77f, .56f),
            new Color(1f, .88f, .73f), .48f);
        private static readonly Palette Night = new Palette(new Color(.43f, .57f, .79f),
            new Color(.47f, .57f, .75f), 1f);

        [Serializable]
        private struct Lamp
        {
            public RectTransform Landmark;
            public Vector2 Position;
            public Vector2 Radius;
        }

        private readonly struct Palette
        {
            public readonly Color Art;
            public readonly Color Clouds;
            public readonly float Lamps;

            public Palette(Color art, Color clouds, float lamps)
            {
                Art = art;
                Clouds = clouds;
                Lamps = lamps;
            }

            public static Palette Blend(Palette from, Palette to, float t) =>
                new Palette(Color.Lerp(from.Art, to.Art, t), Color.Lerp(from.Clouds, to.Clouds, t),
                    Mathf.Lerp(from.Lamps, to.Lamps, t));
        }

        [SerializeField] private Image _terrain;
        [SerializeField] private Transform _landmarks;
        [SerializeField] private CityMapFog _fog;
        [SerializeField] private CityMapAtmosphere _atmosphere;
        [SerializeField] private Lamp[] _lamps;
#if UNITY_EDITOR
        private enum Preview { WorldClock, Morning, Dusk, Night }
        [SerializeField] private Preview _editorPreview;
#endif

        private CanvasRenderer[] _art;
        private float _worldMinute = OpeningMinute;
        private float _displayedTarget = -1;
        private float _transition = 1;
        private Palette _from = Day;
        private Palette _to = Day;
        private Palette _current = Day;

        protected override void Awake()
        {
            base.Awake();
            if (!Application.isPlaying) return;
            _art = new CanvasRenderer[_landmarks.childCount + 1];
            _art[0] = _terrain.canvasRenderer;
            for (int i = 0; i < _landmarks.childCount; i++)
                _art[i + 1] = _landmarks.GetChild(i).GetComponent<Image>().canvasRenderer;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _displayedTarget = -1;
        }

        internal void RenderTime(int minuteOfDay, int minutesPerDay)
        {
            _worldMinute = DisplayMinute(minuteOfDay, minutesPerDay);
            if (isActiveAndEnabled) Advance(0);
        }

        public static Color ArtworkTintAtTime(int minuteOfDay, int minutesPerDay) =>
            AtMinute(DisplayMinute(minuteOfDay, minutesPerDay)).Art;

        private static float DisplayMinute(int minuteOfDay, int minutesPerDay) =>
            Mathf.Repeat(OpeningMinute + minuteOfDay / (float)minutesPerDay * DayMinutes, DayMinutes);

        internal void Advance(float elapsed)
        {
            float target = _worldMinute;
#if UNITY_EDITOR
            switch (_editorPreview)
            {
                case Preview.Morning: target = 480; break;
                case Preview.Dusk: target = 1100; break;
                case Preview.Night: target = 1320; break;
            }
#endif
            if (_displayedTarget != target)
            {
                bool firstFrame = _displayedTarget < 0;
                _displayedTarget = target;
                _from = _current;
                _to = AtMinute(target);
                _transition = firstFrame ? 1 : 0;
                if (firstFrame) Apply(_to);
            }
            if (_transition >= 1 || elapsed <= 0) return;

            _transition = Mathf.Min(1, _transition + elapsed / TransitionSeconds);
            Apply(Palette.Blend(_from, _to, Mathf.SmoothStep(0, 1, _transition)));
        }

        private static Palette AtMinute(float minute)
        {
            if (minute < 300 || minute >= 1230) return Night;
            if (minute < 375) return Palette.Blend(Night, Dawn, Mathf.InverseLerp(300, 375, minute));
            if (minute < OpeningMinute)
                return Palette.Blend(Dawn, Day, Mathf.InverseLerp(375, OpeningMinute, minute));
            if (minute < 990) return Day;
            if (minute < 1100) return Palette.Blend(Day, Dusk, Mathf.InverseLerp(990, 1100, minute));
            return Palette.Blend(Dusk, Night, Mathf.InverseLerp(1100, 1230, minute));
        }

        private void Apply(Palette palette)
        {
            foreach (CanvasRenderer layer in _art) layer.SetColor(palette.Art);
            // Renderer tint preserves fog opacity and avoids rebuilding its cached region mesh.
            _fog.canvasRenderer.SetColor(palette.Clouds);
            _atmosphere.canvasRenderer.SetColor(palette.Clouds);
            if (!Mathf.Approximately(palette.Lamps, _current.Lamps)) SetVerticesDirty();
            _current = palette;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_current.Lamps <= .001f || _lamps == null) return;
            foreach (Lamp lamp in _lamps)
            {
                Rect rect = lamp.Landmark.rect;
                Vector2 position = rect.min + Vector2.Scale(rect.size, lamp.Position);
                Vector2 center = rectTransform.InverseTransformPoint(lamp.Landmark.TransformPoint(position));
                Vector2 radius = Vector2.Scale(rect.size, lamp.Radius);
                DrawGlow(mesh, center, radius * 3.4f, new Color(1f, .65f, .26f, .32f * _current.Lamps));
                DrawGlow(mesh, center, radius, new Color(1f, .92f, .62f, .96f * _current.Lamps));
            }
        }

        private static void DrawGlow(VertexHelper mesh, Vector2 center, Vector2 radius, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            tint.a = 0;
            for (int i = 0; i < LampSides; i++)
            {
                float angle = -i * (2 * Mathf.PI / LampSides);
                var offset = new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
                mesh.AddVert(center + offset, tint, Vector2.zero);
                mesh.AddTriangle(first, first + i + 1, first + (i + 1) % LampSides + 1);
            }
        }
    }
}
