using HomelessPetHelper.Framework.UI.Motion;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Game.Field
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OutingWeatherGraphic : MaskableGraphic
    {
        private const float FrameInterval = 1f / 30f;
        private static readonly Color RainInk = new Color32(87, 132, 160, 255);
        private static readonly Color RainLight = new Color32(216, 235, 237, 255);
        private static readonly Color SnowPaper = new Color32(253, 253, 245, 255);
        private static readonly Color SnowShade = new Color32(137, 167, 186, 255);
        private static readonly Vector2[] FluffOutline =
        {
            new Vector2(1, 0), new Vector2(.58f, .82f), new Vector2(-.50f, .92f),
            new Vector2(-1, 0), new Vector2(-.49f, -.79f), new Vector2(.52f, -.94f)
        };
        private static readonly Color FogInk = new Color32(133, 158, 170, 255);
        private static readonly Color FogLight = new Color32(224, 238, 234, 255);
        private static readonly ProfilerMarker PopulateMarker = new ProfilerMarker("Presentation.OutingWeather");
        private MotionScope _clock;
        private bool _useMapClock, _focused = true, _backgrounded;
        private OutingWeather _weather;
        private float _lastFrame, _mapSeconds;

        internal void Show(OutingWeather weather, MotionScope clock)
        {
            _weather = weather;
            _clock = clock;
            _useMapClock = false;
            _lastFrame = -FrameInterval;
            SetVerticesDirty();
        }

        internal void ShowOnMap(OutingWeather weather)
        {
            Show(weather, null);
            _useMapClock = true;
        }

        private float Elapsed => _useMapClock ? _mapSeconds : _clock == null ? 0 : _clock.SceneSeconds;

        private void LateUpdate()
        {
            if (_weather.Kind == OutingWeatherKind.Clear) return;
            if (_useMapClock)
            {
                if (!_focused || _backgrounded) return;
                _mapSeconds += Mathf.Min(Time.unscaledDeltaTime, .1f);
            }
            else if (_clock == null || _clock.ScenePaused) return;
            float now = Elapsed;
            if (now < _lastFrame + FrameInterval) return;
            _lastFrame = now;
            SetVerticesDirty();
        }

        private void OnApplicationFocus(bool focused) => _focused = focused;
        private void OnApplicationPause(bool paused) => _backgrounded = paused;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_weather.Kind == OutingWeatherKind.Clear) return;
            var bounds = rectTransform.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            using (PopulateMarker.Auto())
            {
                int tier = (int)_weather.Intensity;
                switch (_weather.Kind)
                {
                    case OutingWeatherKind.Rain:
                        DrawRain(mesh, bounds, Elapsed, tier);
                        if (!_useMapClock) DrawRainRipples(mesh, bounds, Elapsed, tier);
                        break;
                    case OutingWeatherKind.Snow: DrawSnow(mesh, bounds, Elapsed, tier); break;
                    case OutingWeatherKind.Fog: DrawFog(mesh, bounds, Elapsed, tier); break;
                }
            }
        }

        private static void DrawRain(VertexHelper mesh, Rect bounds, float time, int tier)
        {
            int count = 62 + tier * 46;
            float aspect = bounds.height / bounds.width;
            for (int i = 0; i < count; i++)
            {
                float depth = Sample(i, 1);
                float travel = time * (.83f + depth * .73f + tier * .10f);
                float slant = .19f + Sample(i, 11) * .13f;
                var uv = new Vector2(Mathf.Repeat(Sample(i, 2) - travel * slant * aspect, 1),
                    Mathf.Repeat(Sample(i, 3) - travel, 1));
                float alpha = EdgeFade(uv) * (.31f + depth * .51f + tier * .035f);
                if (alpha < .01f) continue;
                float length = bounds.width * (.016f + depth * depth * .027f + tier * .003f);
                float width = bounds.width * (.0021f + depth * .0041f);
                var core = Color.Lerp(RainInk, RainLight, i % 3 == 0 ? .76f : .10f + Sample(i, 12) * .30f);
                DrawDrop(mesh, bounds, Point(bounds, uv), new Vector2(slant, 1) * length, width, core, alpha);
            }
        }

        private static void DrawDrop(VertexHelper mesh, Rect bounds, Vector2 tip, Vector2 trail, float width,
            Color core, float alpha)
        {
            // One translucent head with feathered sides and a tapered tail.
            Vector2 side = new Vector2(-trail.y, trail.x).normalized * width;
            Vector2 head = tip + trail * .19f;
            Vector2 shoulder = tip + trail * .43f;
            int first = mesh.currentVertCount;
            mesh.AddVert(Clamp(bounds, head), Tint(core, alpha), Vector2.zero);
            mesh.AddVert(Clamp(bounds, tip), Tint(core, alpha * .1f), Vector2.zero);
            mesh.AddVert(Clamp(bounds, head + side), Tint(core, 0), Vector2.zero);
            mesh.AddVert(Clamp(bounds, shoulder + side * .54f), Tint(core, .04f * alpha), Vector2.zero);
            mesh.AddVert(Clamp(bounds, tip + trail), Tint(core, 0), Vector2.zero);
            mesh.AddVert(Clamp(bounds, shoulder - side * .54f), Tint(core, .04f * alpha), Vector2.zero);
            mesh.AddVert(Clamp(bounds, head - side), Tint(core, 0), Vector2.zero);
            for (int i = 0; i < 6; i++) mesh.AddTriangle(first, first + i + 1, first + (i + 1) % 6 + 1);
        }

        private static void DrawRainRipples(VertexHelper mesh, Rect bounds, float time, int tier)
        {
            for (int i = 0; i < 8 + tier * 5; i++)
            {
                float impact = time * (.90f + Sample(i, 4) * .50f) + Sample(i, 5);
                float age = Mathf.Repeat(impact, 1);
                if (age > .39f) continue;
                int splash = i + Mathf.FloorToInt(impact) * 37;
                float spread = age / .39f;
                var center = Point(bounds, new Vector2(.06f + Sample(splash, 6) * .88f,
                    .08f + Sample(splash, 7) * .47f));
                float radius = bounds.width * (.002f + spread * .012f);
                var shape = new Vector2(radius, radius * .28f);
                var prior = center + Vector2.right * radius;
                float alpha = Mathf.Sin(spread * Mathf.PI) * (.13f + tier * .034f);
                for (int side = 1; side <= 8; side++)
                {
                    float angle = side * Mathf.PI / 4;
                    var next = center + Vector2.Scale(shape, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                    Stroke(mesh, bounds, prior, next, bounds.width * .00155f, RainInk, alpha, alpha);
                    prior = next;
                }
            }
        }

        private static void DrawSnow(VertexHelper mesh, Rect bounds, float time, int tier)
        {
            int count = 40 + tier * 43;
            for (int i = 0; i < count; i++)
            {
                float depth = Sample(i, 8);
                float drift = Mathf.Sin(time * (1.32f + depth) + i * 1.7f) * (.016f + depth * .022f);
                float travel = time * (.115f + depth * .14f + tier * .028f);
                var uv = new Vector2(Mathf.Repeat(Sample(i, 9) + drift + time * (.028f + tier * .013f), 1),
                    Mathf.Repeat(Sample(i, 10) - travel, 1));
                float alpha = EdgeFade(uv) * (.33f + depth * .55f);
                if (alpha < .01f) continue;
                float radius = bounds.width * (.0025f + depth * depth * .0085f + tier * .0005f);
                AddSnowFluff(mesh, bounds, Point(bounds, uv), radius, i, time, alpha);
            }
        }

        private static void AddSnowFluff(VertexHelper mesh, Rect bounds, Vector2 center, float radius,
            int seed, float time, float alpha)
        {
            const int sides = 6;
            int first = mesh.currentVertCount;
            float phase = seed * 1.8f + time * (.28f + Sample(seed, 13) * .45f);
            float sin = Mathf.Sin(phase), cos = Mathf.Cos(phase);
            mesh.AddVert(Clamp(bounds, center), Tint(SnowPaper, alpha), Vector2.zero);
            for (int i = 0; i < sides; i++)
            {
                var p = FluffOutline[i] * (radius * (.73f + Sample(seed, 20 + i) * .52f));
                var outline = new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
                var shade = Color.Lerp(SnowShade, SnowPaper, .54f + Sample(seed, 30 + i) * .40f);
                mesh.AddVert(Clamp(bounds, center + outline * .53f), Tint(shade, alpha * .67f), Vector2.zero);
                mesh.AddVert(Clamp(bounds, center + outline * 1.26f), Tint(shade, 0), Vector2.zero);
            }
            for (int i = 0; i < sides; i++)
            {
                int inner = first + 1 + i * 2, next = first + 1 + (i + 1) % sides * 2;
                mesh.AddTriangle(first, inner, next);
                mesh.AddTriangle(inner, inner + 1, next + 1);
                mesh.AddTriangle(inner, next + 1, next);
            }
        }

        private static void DrawFog(VertexHelper mesh, Rect bounds, float time, int tier)
        {
            const int columns = 18, rows = 22;
            for (int row = 0; row <= rows; row++)
            for (int column = 0; column <= columns; column++)
            {
                float x = column / (float)columns, y = row / (float)rows;
                float wave = Mathf.Sin(x * 7.6f + time * .72f) * .067f;
                float lower = Mist(y, .28f + wave, .26f);
                float upper = Mist(y, .67f + Mathf.Sin(x * 5.1f - time * .49f) * .087f, .25f);
                float drift = .72f + .28f * Mathf.Sin(x * 12f + y * 5f - time * .91f);
                var uv = new Vector2(x, y);
                float haze = .076f + tier * .059f;
                float bands = (lower + upper) * drift * (.29f + tier * .15f);
                float alpha = EdgeFade(uv) * (haze + bands);
                Color hue = Color.Lerp(FogInk, FogLight, .38f + .27f * Mathf.Sin(x * 13 - time * .7f + y * 6));
                mesh.AddVert(Point(bounds, new Vector2(x, y)), Tint(hue, alpha), Vector2.zero);
                if (row == 0 || column == 0) continue;
                int end = mesh.currentVertCount - 1;
                int bottom = end - columns - 1;
                mesh.AddTriangle(bottom - 1, end - 1, end);
                mesh.AddTriangle(bottom - 1, end, bottom);
            }
        }

        private static float Mist(float height, float center, float width)
        {
            float opacity = Mathf.Clamp01(1 - Mathf.Abs(height - center) / width);
            return opacity * opacity;
        }

        private static float EdgeFade(Vector2 point) => Mathf.Clamp01(Mathf.Min(
            Mathf.Min(point.x, 1 - point.x) * 14, Mathf.Min(point.y, 1 - point.y) * 12));

        private static Vector2 Point(Rect bounds, Vector2 uv) =>
            bounds.min + Vector2.Scale(bounds.size, uv);

        private static float Sample(int index, int channel)
        {
            unchecked
            {
                uint hash = (uint)index * 0x9E3779B9u + (uint)channel * 0x85EBCA6Bu;
                hash = (hash ^ (hash >> 16)) * 0x7FEB352Du;
                hash = (hash ^ (hash >> 15)) * 0x846CA68Bu;
                return ((hash ^ (hash >> 16)) & 0xffffu) / 65536f;
            }
        }

        private static Color Tint(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static void Stroke(VertexHelper mesh, Rect bounds, Vector2 from, Vector2 to, float width,
            Color color, float alpha, float tailAlpha)
        {
            var offset = new Vector2(from.y - to.y, to.x - from.x).normalized * (width * .5f);
            Quad(mesh, bounds, from - offset, to - offset, to + offset, from + offset,
                Tint(color, alpha), Tint(color, tailAlpha));
        }

        private static Vector2 Clamp(Rect bounds, Vector2 point) => new Vector2(
            Mathf.Clamp(point.x, bounds.xMin, bounds.xMax), Mathf.Clamp(point.y, bounds.yMin, bounds.yMax));

        private static void Quad(VertexHelper mesh, Rect bounds, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color from, Color to)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(Clamp(bounds, a), from, Vector2.zero);
            mesh.AddVert(Clamp(bounds, b), to, Vector2.zero);
            mesh.AddVert(Clamp(bounds, c), to, Vector2.zero);
            mesh.AddVert(Clamp(bounds, d), from, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
