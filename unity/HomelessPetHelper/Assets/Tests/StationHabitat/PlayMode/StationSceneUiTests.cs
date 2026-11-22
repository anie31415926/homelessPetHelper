#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.StationHabitat.PlayMode
{
    public sealed class StationSceneUiTests
    {
        private StationPanelView _view;
        private GameObject Detail => (GameObject)new SerializedObject(_view)
            .FindProperty("_detail").objectReferenceValue;
        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/EditorScenes/StationHabitat/SC_StationScene.unity", new LoadSceneParameters());
            yield return null;
            yield return null;
            yield return null;
            _view = UnityEngine.Object.FindObjectOfType<StationPanelView>();
        }

        [UnityTest]
        public IEnumerator SceneUsesSeveralIdsAndCagedAnimalOpensItsOwnCompactSheet()
        {
            yield return Load();
            Assert.That(_view.IsSceneLayout, Is.True);
            Assert.That(_view.Scene.Count, Is.EqualTo(3));
            var cells = _view.Scene.Cells.Take(3).ToArray();
            Assert.That(cells.Select(cell => cell.AnimalId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(cells.Select(cell => cell.Observing), Is.EqualTo(new[] { false, false, true }));
            Assert.That(Detail.activeSelf, Is.False);
            Assert.That(Button("Clean").isActiveAndEnabled, Is.False);
            Assert.That(Button("Finish").isActiveAndEnabled, Is.False);
            Assert.That(Button("History").isActiveAndEnabled, Is.False);
            Assert.That(cells.Select(cell => cell.MotionImage.texture).Distinct().Count(), Is.EqualTo(3));
            var cageId = cells[2].AnimalId;
            RayClick(cells[2].SelectButton);
            yield return null;
            Assert.That(Detail.activeSelf, Is.True);
            Assert.That(_view.Scene.gameObject.activeInHierarchy, Is.True);
            Assert.That(_view.GetComponentsInChildren<Text>().Any(text => text.text == "救助猫咪 3"), Is.True);
            var sheet = (RectTransform)Detail.transform;
            Canvas.ForceUpdateCanvases(); yield return null;
            Assert.That(sheet.rect.height / ((RectTransform)sheet.parent).rect.height, Is.InRange(.1f, .9f));
            Assert.That(((RectTransform)Button("Back").transform).rect.height, Is.GreaterThanOrEqualTo(144));
            Assert.That(cells[2].AnimalId, Is.EqualTo(cageId));
            Button("DismissAnimal").onClick.Invoke();
            Assert.That(Detail.activeSelf, Is.False);
            Assert.That(cells[2].Observing, Is.True, "Looking does not assess or release an animal.");
        }

        [UnityTest]
        public IEnumerator CareHasItsOwnCloseupAndLeavingReusesTheSamePresentation()
        {
            yield return Load();
            var motion = _view.Scene.Cells[0].Motion;
            _view.Scene.Cells[0].SelectButton.onClick.Invoke();
            Button("Clean").onClick.Invoke();
            yield return null;
            Assert.That(_view.Scene.gameObject.activeInHierarchy, Is.False);
            Assert.That(_view.Scene.Cells[0].MotionImage.texture, Is.Null);
            Assert.That(_view.Surface.gameObject.activeInHierarchy, Is.True);
            Button("Finish").onClick.Invoke();
            Assert.That(_view.Scene.gameObject.activeInHierarchy, Is.True);
            Assert.That(Detail.activeSelf, Is.True);
            Button("Back").onClick.Invoke();
            Assert.That(Detail.activeSelf, Is.False);
            Assert.That(_view.Scene.Cells[0].Motion, Is.SameAs(motion));
            Button("StationMenu").onClick.Invoke();
            var pause = _view.GetComponentInChildren<Toggle>(true);
            pause.isOn = true;
            double seconds = motion.ActionSeconds;
            yield return null;
            yield return null;
            Assert.That(motion.ActionSeconds, Is.EqualTo(seconds));
            pause.isOn = false;
            yield return null;
            Assert.That(motion.ActionSeconds, Is.GreaterThan(seconds));
            _view.gameObject.SetActive(false);
            yield return null;
            Assert.That(UnityEngine.Object.FindObjectsOfType<StationHabitatRenderer>(), Is.Empty);
            _view.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(_view.Scene.Cells[0].Motion, Is.SameAs(motion));
            Assert.That(_view.Scene.Cells[0].MotionImage.texture, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator FocusRecoveryCannotOverridePauseAndEmptySceneReleasesAllActors()
        {
            yield return Load();
            var motion = _view.Scene.Cells[0].Motion;
            _view.Scene.SendMessage("OnApplicationFocus", false);
            _view.Scene.SendMessage("OnApplicationPause", true);
            _view.Scene.SendMessage("OnApplicationFocus", true);
            double seconds = motion.ActionSeconds;
            yield return null;
            Assert.That(motion.ActionSeconds, Is.EqualTo(seconds));
            Assert.That(_view.Scene.Cells[0].SelectButton.IsInteractable(), Is.False);
            _view.Scene.SendMessage("OnApplicationPause", false);
            yield return null;
            Assert.That(motion.ActionSeconds, Is.GreaterThan(seconds));
            _view.Scene.ClearResidents();
            yield return null;
            Assert.That(_view.Scene.Count, Is.Zero);
            Assert.That(_view.Scene.Cells.All(cell => !cell.gameObject.activeSelf), Is.True);
            Assert.That(UnityEngine.Object.FindObjectsOfType<StationHabitatRenderer>(), Is.Empty);
            Assert.That(_view.GetComponentsInChildren<Text>().Any(text => text.name == "EmptyStation"), Is.True);
        }

        [UnityTest]
        public IEnumerator HomeSheetAndCareRemainInsideFourPortraitViewports()
        {
            yield return Load();
            yield return Capture("home");
            _view.Scene.Cells[2].SelectButton.onClick.Invoke();
            yield return Capture("observation-detail");
            Button("Back").onClick.Invoke();
            _view.Scene.Cells[0].SelectButton.onClick.Invoke();
            yield return Capture("healthy-detail");
            Button("Clean").onClick.Invoke();
            yield return Capture("care");
        }

        private Button Button(string name) => _view.GetComponentsInChildren<Button>(true)
            .Single(button => button.name == name);

        private static void RayClick(Button button)
        {
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            var target = hits[0].gameObject.GetComponentInParent<Button>();
            Assert.That(target, Is.SameAs(button));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private IEnumerator Capture(string state)
        {
            var canvas = _view.GetComponent<Canvas>();
            var camera = new GameObject("StationSceneCapture", typeof(Camera)).GetComponent<Camera>();
            camera.cullingMask = 1 << canvas.gameObject.layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            string folder = Path.GetFullPath("StationSceneEvidence");
            Directory.CreateDirectory(folder);
            foreach (var size in new[] { new Vector2Int(375, 667), new Vector2Int(390, 844),
                new Vector2Int(540, 1200), new Vector2Int(768, 1024) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                camera.aspect = (float)size.x / size.y;
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                Assert.That(texture.GetPixels32().Count(pixel => pixel.r > pixel.g * 1.12f &&
                    pixel.g > pixel.b * 1.1f), Is.GreaterThan(200), state);
                File.WriteAllBytes(Path.Combine(folder, state + "-" + size.x + "x" + size.y + ".png"),
                    texture.EncodeToPNG());
                AssertLayout(camera);
                UnityEngine.Object.Destroy(texture);
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            UnityEngine.Object.Destroy(camera.gameObject);
        }

        private void AssertLayout(Camera camera)
        {
            var corners = new Vector3[4];
            foreach (var button in _view.GetComponentsInChildren<Selectable>())
            {
                foreach (var scroll in button.GetComponentsInParent<ScrollRect>())
                {
                    Canvas.ForceUpdateCanvases();
                    ((RectTransform)button.transform).GetWorldCorners(corners);
                    float bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
                    float top = scroll.viewport.InverseTransformPoint(corners[1]).y;
                    float delta = top > scroll.viewport.rect.yMax ? scroll.viewport.rect.yMax - top :
                        bottom < scroll.viewport.rect.yMin ? scroll.viewport.rect.yMin - bottom : 0;
                    scroll.content.anchoredPosition += new Vector2(0, delta);
                    Canvas.ForceUpdateCanvases();
                }
                ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = camera.WorldToViewportPoint(corner);
                    Assert.That(point.x, Is.InRange(-.005f, 1.005f), button.name);
                    Assert.That(point.y, Is.InRange(-.005f, 1.005f), button.name);
                }
            }
        }
    }
}
#endif
