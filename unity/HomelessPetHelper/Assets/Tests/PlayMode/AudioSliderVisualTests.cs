#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class AudioSliderVisualTests
    {
        private MainSceneStorageScope _storage;

        [SetUp] public void SetUp() => _storage = new MainSceneStorageScope(freshGuidance: true);
        [TearDown] public void TearDown() => _storage.Dispose();

        [UnityTest]
        public IEnumerator SlidersKeepFillAndThumbOnSameRailAtEveryVolumeAndPointerDrag()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            var map = Object.FindObjectOfType<CityMapView>();
            MainApproachDriver.Click(map, "Yard"); MainApproachDriver.Click(map, "Primary");
            yield return null;
            var view = Object.FindObjectOfType<ApproachPanelView>();
            MainApproachDriver.Click(view, "Pause"); MainApproachDriver.Click(view, "OverlaySettings");
            var sliders = view.GetComponentsInChildren<Slider>();
            Assert.That(sliders.Length, Is.EqualTo(3));
            foreach (float value in new[] { 0, .01f, .25f, .5f, .99f, 1 })
            {
                foreach (var slider in sliders) slider.value = value;
                yield return null; Canvas.ForceUpdateCanvases();
                foreach (var slider in sliders)
                {
                    LevelUiPlayTests.ScrollIntoView(slider); yield return null;
                    Canvas.ForceUpdateCanvases(); AssertGeometry(slider, value);
                }
                yield return LevelUiPlayTests.Capture(view, "volume-" + Mathf.RoundToInt(value * 100));
            }
            foreach (var slider in sliders)
            {
                LevelUiPlayTests.ScrollIntoView(slider); yield return null;
                var rail = (RectTransform)slider.handleRect.parent;
                slider.value = 0;
                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(null,
                        rail.TransformPoint(rail.rect.min + new Vector2(0, rail.rect.height / 2)))
                };
                slider.OnPointerDown(pointer);
                pointer.position = RectTransformUtility.WorldToScreenPoint(null,
                    rail.TransformPoint(new Vector3(Mathf.Lerp(rail.rect.xMin, rail.rect.xMax, .75f),
                        rail.rect.center.y)));
                slider.OnDrag(pointer); slider.OnPointerUp(pointer);
                Assert.That(slider.value, Is.EqualTo(.75f).Within(.01f), slider.name);
                Canvas.ForceUpdateCanvases(); AssertGeometry(slider, slider.value);
            }
            Assert.That(Object.FindObjectOfType<MainGameRoot>().Audio.Playback.Preferences.Music.Volume,
                Is.EqualTo(.75f).Within(.01f));
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertGeometry(Slider slider, float value)
        {
            var fill = slider.fillRect.GetComponent<Image>();
            var rail = (RectTransform)slider.fillRect.parent;
            var thumb = slider.handleRect;
            var center = rail.InverseTransformPoint(thumb.TransformPoint(thumb.rect.center));
            Assert.That(center.x, Is.EqualTo(Mathf.Lerp(rail.rect.xMin, rail.rect.xMax, value)).Within(2),
                slider.name + ": the thumb center and visible fill endpoint must share one rail.");
            Assert.That(center.y, Is.EqualTo(rail.rect.center.y).Within(2), slider.name);
            Assert.That(thumb.rect.width, Is.EqualTo(thumb.rect.height).Within(1),
                slider.name + ": the round thumb must not stretch with the bar.");
            var mesh = fill.canvasRenderer.GetMesh();
            if (value == 0)
            {
                Assert.That(mesh == null || mesh.vertexCount == 0 || mesh.bounds.size.x < 2,
                    Is.True, slider.name + ": zero volume must leave no colored fill.");
                return;
            }
            Assert.That(mesh, Is.Not.Null, slider.name);
            var vertices = mesh.vertices.Select(vertex => rail.InverseTransformPoint(
                fill.rectTransform.TransformPoint(vertex))).ToArray();
            Assert.That(vertices.Max(vertex => vertex.x), Is.EqualTo(center.x).Within(3), slider.name);
            Assert.That(vertices.Max(vertex => vertex.y) - vertices.Min(vertex => vertex.y),
                Is.LessThanOrEqualTo(rail.rect.height + 2), slider.name);
        }
    }
}
#endif
