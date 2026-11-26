#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class OutingWeatherPlayTests
    {
        [Test]
        public void ClearIsCanonicalAndInvalidWeatherIsRejected()
        {
            var clear = new OutingWeather(OutingWeatherKind.Clear, OutingWeatherIntensity.Heavy);
            Assert.That(clear.Kind, Is.EqualTo(default(OutingWeather).Kind));
            Assert.That(clear.Intensity, Is.EqualTo(OutingWeatherIntensity.Light));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new OutingWeather((OutingWeatherKind)7, OutingWeatherIntensity.Light));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new OutingWeather(OutingWeatherKind.Rain, (OutingWeatherIntensity)(-1)));
        }

        [UnityTest]
        public IEnumerator MapWeatherSetsOutingOnceAndCanPauseSwapStrengthAndClear()
        {
            using var storage = new MainSceneStorageScope();
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            var presenter = Object.FindObjectOfType<MainGameRoot>().Presenter;
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            var map = Object.FindObjectOfType<CityMapView>();
            var seed = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset")
                .CreateMapConfig().Seed;
            var clock = presenter.Session.Clock;
            var scheduled = WorldWeatherSchedule.Sample(seed, clock.Current.ElapsedMinutes,
                clock.Current.MinutesPerDay);
            Assert.That(presenter.WorldWeather.Kind, Is.EqualTo(scheduled.Kind),
                "World weather follows the clock schedule on construction.");
            Assert.That(presenter.WorldWeather.Intensity, Is.EqualTo(scheduled.Intensity));
            Assert.That(clock.TryAdvance(clock.Current.Version, WorldWeatherSchedule.BlockMinutes,
                out _, out _), Is.True);
            presenter.ResampleWorldWeather();
            var next = WorldWeatherSchedule.Sample(seed, clock.Current.ElapsedMinutes,
                clock.Current.MinutesPerDay);
            Assert.That(presenter.WorldWeather.Kind, Is.EqualTo(next.Kind),
                "Resampling follows the advanced clock.");
            Assert.That(presenter.WorldWeather.Intensity, Is.EqualTo(next.Intensity));
            yield return VerifyMapWeather(map, presenter);
            var start = presenter.WorldWeather;
            var originalTime = clock.Current;
            Assert.That(clock.TryAdvance(originalTime.Version, originalTime.MinutesPerDay * 7L / 12,
                out _, out _), Is.True);
            MainApproachDriver.Click(map, "Yard");
            MainApproachDriver.Click(map, "Primary"); yield return null;
            var opening = Object.FindObjectOfType<ApproachPanelView>();
            var scene = opening.FieldScene;
            Assert.That(scene.Weather, Is.EqualTo(start));
            var nightTint = Background(scene).color;
            Assert.That(nightTint.r, Is.LessThan(nightTint.b));
            Assert.That(nightTint.r, Is.LessThan(.8f));
            presenter.SetWorldWeather(new OutingWeather(OutingWeatherKind.Fog, OutingWeatherIntensity.Heavy));
            Assert.That(scene.Weather, Is.EqualTo(start));
            yield return VerifyEffects(opening.GetComponent<Canvas>(), scene);

            scene.SetWeather(start);
            scene.SetPresentationPaused(false);
            MainApproachDriver.Solve(opening);
            MainApproachDriver.Click(opening, "Continue"); yield return null;
            var battle = Object.FindObjectOfType<SiegeTrialView>();
            var field = battle.GetComponent<OutingFieldView>();
            Assert.That(field.Weather, Is.EqualTo(start), "Opening and rescue must share the departure snapshot.");
            Assert.That(Background(field).color, Is.EqualTo(nightTint));
            Assert.That(scene.GetComponentInChildren<OutingWeatherGraphic>(true).isActiveAndEnabled, Is.False);
            Assert.That(field.GetComponentInChildren<OutingWeatherGraphic>().raycastTarget, Is.False);
            yield return VerifyBattleFog(battle.GetComponent<Canvas>(), field);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ParkingUsesCurrentWorldWeatherWithoutASitePreset()
        {
            using var storage = new MainSceneStorageScope();
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            var state = new OutingWeather(OutingWeatherKind.Fog, OutingWeatherIntensity.Medium);
            Object.FindObjectOfType<MainGameRoot>().Presenter.SetWorldWeather(state);
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            var map = Object.FindObjectOfType<CityMapView>();
            MainApproachDriver.Click(map, "Parking");
            MainApproachDriver.Click(map, "Primary"); yield return null;
            var scene = Object.FindObjectOfType<ApproachPanelView>()?.FieldScene ??
                Object.FindObjectOfType<SiegeTrialView>()?.GetComponent<OutingFieldView>();
            Assert.That(scene, Is.Not.Null);
            Assert.That(scene.Layout.PointId, Is.EqualTo("parking"));
            Assert.That(scene.Weather, Is.EqualTo(state));
            Assert.That(Background(scene).color, Is.EqualTo(Color.white));
        }

        private static Image Background(OutingFieldView scene) =>
            (Image)new SerializedObject(scene).FindProperty("_background").objectReferenceValue;

        private static IEnumerator VerifyMapWeather(CityMapView map, MainLoopPresenter presenter)
        {
            var graphic = map.GetComponentInChildren<OutingWeatherGraphic>();
            Assert.That(graphic, Is.Not.Null);
            Assert.That(graphic.raycastTarget, Is.False);
            Assert.That(graphic.transform.parent.GetComponent<UnityEngine.UI.ScrollRect>(), Is.Not.Null);
            var canvas = map.GetComponent<Canvas>();
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            float oldDistance = canvas.planeDistance;
            var camera = StrategyUiPlayTests.PortraitInput(canvas, out var target);
            try
            {
                presenter.SetWorldWeather(default);
                yield return null;
                Assert.That(VertexCount(graphic), Is.Zero);
                var clear = Capture(camera, target, "Map-Clear");
                presenter.SetWorldWeather(new OutingWeather(OutingWeatherKind.Rain, OutingWeatherIntensity.Medium));
                yield return null;
                Assert.That(VertexCount(graphic), Is.GreaterThan(80).And.LessThanOrEqualTo((62 + 46) * 7));
                var rain = Capture(camera, target, "Map-Rain-Medium");
                Assert.That(clear.Zip(rain, (a, b) => !a.Equals(b)).Count(value => value), Is.GreaterThan(200));
                presenter.SetWorldWeather(new OutingWeather(OutingWeatherKind.Snow, OutingWeatherIntensity.Medium));
                yield return null;
                Assert.That(VertexCount(graphic), Is.GreaterThan(80));
                Assert.That(graphic.canvasRenderer.GetMesh().colors32.Count(color => color.a == 0) * 3,
                    Is.GreaterThan(VertexCount(graphic)), "Every snow clump needs transparent perimeter vertices.");
                var snow = Capture(camera, target, "Map-Snow-Medium");
                Assert.That(clear.Zip(snow, (a, b) => !a.Equals(b)).Count(value => value), Is.GreaterThan(200));
                Assert.That(rain.Zip(snow, (a, b) => !a.Equals(b)).Count(value => value), Is.GreaterThan(200));
            }
            finally
            {
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
                camera.targetTexture = null; target.Release(); Object.Destroy(target);
                Object.Destroy(camera.gameObject);
            }
        }

        private static IEnumerator VerifyEffects(Canvas canvas, OutingFieldView field)
        {
            var graphic = field.GetComponentInChildren<OutingWeatherGraphic>();
            Assert.That(graphic, Is.Not.Null);
            Assert.That(graphic.raycastTarget, Is.False);
            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            var camera = StrategyUiPlayTests.PortraitInput(canvas, out var target);
            try
            {
                yield return null;
                field.SetPresentationPaused(true);
                foreach (var kind in new[] { OutingWeatherKind.Rain, OutingWeatherKind.Snow, OutingWeatherKind.Fog })
                    yield return VerifyKind(field, graphic, kind, camera, target);
                field.SetWeather(default);
                Canvas.ForceUpdateCanvases();
                Assert.That(VertexCount(graphic), Is.Zero);
                field.SetWeather(new OutingWeather(OutingWeatherKind.Rain, OutingWeatherIntensity.Medium));
                field.SetPresentationPaused(false);
                var before = Vertices(graphic);
                for (int i = 0; i < 8 && Vertices(graphic).SequenceEqual(before); i++) yield return null;
                Assert.That(Vertices(graphic), Is.Not.EqualTo(before));
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                camera.targetTexture = null; target.Release();
                Object.Destroy(target); Object.Destroy(camera.gameObject);
            }
        }

        private static IEnumerator VerifyBattleFog(Canvas canvas, OutingFieldView field)
        {
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            float oldDistance = canvas.planeDistance;
            var camera = StrategyUiPlayTests.PortraitInput(canvas, out var target);
            try
            {
                field.SetWeather(new OutingWeather(OutingWeatherKind.Fog, OutingWeatherIntensity.Heavy));
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(768, 1024) })
                {
                    camera.targetTexture = null; target.Release(); Object.Destroy(target);
                    target = new RenderTexture(size.x, size.y, 24);
                    camera.targetTexture = target;
                    yield return null; yield return null;
                    var graphic = field.GetComponentInChildren<OutingWeatherGraphic>();
                    Assert.That(VertexCount(graphic), Is.EqualTo(437));
                    Capture(camera, target, "Battle-Fog-" + size.x + "x" + size.y);
                }
            }
            finally
            {
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
                camera.targetTexture = null; target.Release(); Object.Destroy(target);
                Object.Destroy(camera.gameObject);
            }
        }

        private static IEnumerator VerifyKind(OutingFieldView field, OutingWeatherGraphic graphic,
            OutingWeatherKind kind, Camera camera, RenderTexture target)
        {
            field.SetWeather(new OutingWeather(kind, OutingWeatherIntensity.Light));
            int light = VertexCount(graphic);
            int lightAlpha = MaxAlpha(graphic);
            var lightPixels = Capture(camera, target, kind + "-Light");
            field.SetWeather(new OutingWeather(kind, OutingWeatherIntensity.Medium));
            int medium = VertexCount(graphic);
            int mediumAlpha = MaxAlpha(graphic);
            Capture(camera, target, kind + "-Medium");
            field.SetWeather(new OutingWeather(kind, OutingWeatherIntensity.Heavy));
            int heavy = VertexCount(graphic);
            Assert.That(light, Is.GreaterThan(80));
            Assert.That(heavy, Is.LessThan(2100));
            if (kind == OutingWeatherKind.Fog)
            {
                Assert.That(mediumAlpha, Is.GreaterThan(lightAlpha));
                Assert.That(MaxAlpha(graphic), Is.GreaterThan(mediumAlpha));
            }
            else
            {
                Assert.That(medium, Is.GreaterThan(light));
                Assert.That(heavy, Is.GreaterThan(medium));
            }
            var heavyPixels = Capture(camera, target, kind + "-Heavy");
            int changed = 0;
            for (int i = 0; i < lightPixels.Length; i++)
                if (!lightPixels[i].Equals(heavyPixels[i])) changed++;
            Assert.That(changed, Is.GreaterThan(100), kind + " strength must affect actual pixels.");
            var frozen = Vertices(graphic);
            yield return null; yield return null;
            Assert.That(Vertices(graphic), Is.EqualTo(frozen), "Weather must not move while paused.");
            var rect = graphic.rectTransform.rect;
            Assert.That(Array.TrueForAll(frozen, point =>
                point.x >= rect.xMin - .001f && point.x <= rect.xMax + .001f &&
                point.y >= rect.yMin - .001f && point.y <= rect.yMax + .001f), Is.True);
        }

        private static int VertexCount(OutingWeatherGraphic graphic) => Vertices(graphic).Length;

        private static Vector3[] Vertices(OutingWeatherGraphic graphic)
        {
            Canvas.ForceUpdateCanvases();
            var mesh = graphic.canvasRenderer.GetMesh();
            return mesh == null ? Array.Empty<Vector3>() : mesh.vertices;
        }

        private static int MaxAlpha(OutingWeatherGraphic graphic) =>
            graphic.canvasRenderer.GetMesh().colors32.Max(color => color.a);

        private static Color32[] Capture(Camera camera, RenderTexture target, string name)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                Directory.CreateDirectory("WeatherEvidence");
                File.WriteAllBytes("WeatherEvidence/" + name + ".png", image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.Destroy(image); }
        }
    }
}
#endif
