#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.Station.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class StationWhiteboardTests
    {
        private const string ScenePath = "Assets/EditorScenes/SC_StationWhiteboard.unity";

        [UnityTest]
        public IEnumerator SummaryCarePartialCleaningPauseAndSingleTimeCommitWorkThroughUI()
        {
            yield return Load();
            StationPanelView panel = Object.FindObjectOfType<StationPanelView>();
            string initialTime = Label(panel, "Clock").text;
            Assert.That(panel.GetComponentsInChildren<Button>().Select(button => button.name),
                Does.Not.Contain("Clean"));
            Button(panel, "Enter").onClick.Invoke();
            yield return null;
            Assert.That(panel.Animals.All(animal => animal.gameObject.activeInHierarchy), Is.True);
            Assert.That(panel.Animals[0].GetComponentsInChildren<Image>()
                .Single(image => image.name == "Animal").raycastTarget, Is.False);
            ClickAnimal(panel, 2);
            Assert.That(Button(panel, "Clean").interactable, Is.False);
            Button(panel, "Check").onClick.Invoke();
            Button(panel, "Pet").onClick.Invoke();
            Assert.That(Label(panel, "Clock").text, Is.EqualTo(initialTime));
            Button(panel, "Clean").onClick.Invoke();
            RawImage overlay = panel.Surface.GetComponentInChildren<RawImage>();
            long before = DirtAmount(overlay);
            Stroke(panel, new Vector2(0.35f, 0.60f), 32);
            long partial = DirtAmount(overlay);
            Assert.That(partial, Is.LessThan(before).And.GreaterThan(0));
            Assert.That(Button(panel, "Finish").interactable, Is.False);
            Button(panel, "Back").onClick.Invoke();
            Assert.That(Label(panel, "Clock").text, Is.EqualTo(initialTime));
            panel.Animals[2].GetComponent<Button>().onClick.Invoke();
            Assert.That(DirtAmount(panel.Surface.GetComponentInChildren<RawImage>()), Is.EqualTo(partial));
            Button(panel, "Clean").onClick.Invoke();
            Toggle pause = panel.GetComponentsInChildren<Toggle>().Single();
            pause.isOn = true;
            Stroke(panel, new Vector2(0.35f, 0.60f), 32);
            Assert.That(DirtAmount(overlay), Is.EqualTo(partial));
            pause.isOn = false;
            panel.Surface.SendMessage("OnApplicationFocus", false);
            Stroke(panel, new Vector2(0.35f, 0.60f), 32);
            Assert.That(DirtAmount(overlay), Is.EqualTo(partial));
            panel.Surface.SendMessage("OnApplicationFocus", true);
            yield return null;
            Assert.That(DirtAmount(overlay), Is.EqualTo(partial), "Old held input survived focus loss.");
            WashAll(panel);
            Assert.That(Button(panel, "Finish").interactable, Is.True);
            Button finish = Button(panel, "Finish");
            finish.onClick.Invoke();
            string committedTime = Label(panel, "Clock").text;
            Assert.That(committedTime, Does.Contain("09:20"));
            finish.onClick.Invoke();
            Assert.That(Label(panel, "Clock").text, Is.EqualTo(committedTime));
            Assert.That(DirtAmount(overlay), Is.Zero);
            Button(panel, "Back").onClick.Invoke();
            Button(panel, "History").onClick.Invoke();
            Assert.That(Label(panel, "Outcome").text, Does.Contain("65/100"));
            Button(panel, "Enter").onClick.Invoke();
            Assert.That(Label(panel, "Capacity").text, Does.Contain("3/4"));
            panel.Animals[1].GetComponent<Button>().onClick.Invoke();
            Assert.That(DirtAmount(overlay), Is.Zero, "Clean arrival was dirtied to force a minigame.");
            Button(panel, "Check").onClick.Invoke();
            Button(panel, "Clean").onClick.Invoke();
            Assert.That(Button(panel, "Finish").interactable, Is.True);
            Button(panel, "Finish").onClick.Invoke();
            Assert.That(Label(panel, "Clock").text, Does.Contain("09:40"));
            Button(panel, "Restart").onClick.Invoke();
            Assert.That(Label(panel, "Clock").text, Is.EqualTo(initialTime));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator PagesRenderAtSixRatiosWithVisibleArtAndReachableControls()
        {
            yield return Load();
            StationPanelView panel = Object.FindObjectOfType<StationPanelView>();
            Canvas canvas = panel.GetComponent<Canvas>();
            var cameraRoot = new GameObject("StationCaptureCamera");
            Camera camera = cameraRoot.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.orthographic = true;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var sizes = new[] { new Vector2Int(540, 960), new Vector2Int(390, 844), new Vector2Int(768, 1024),
                new Vector2Int(540, 1080), new Vector2Int(540, 1170), new Vector2Int(540, 1200) };
            foreach (Vector2Int size in sizes)
            {
                Button(panel, "Restart").onClick.Invoke();
                yield return Capture(panel, camera, size, "summary");
                Button(panel, "Enter").onClick.Invoke();
                yield return Capture(panel, camera, size, "station");
                panel.Animals[2].GetComponent<Button>().onClick.Invoke();
                Button(panel, "Check").onClick.Invoke();
                Button(panel, "Clean").onClick.Invoke();
                yield return Capture(panel, camera, size, "dirty");
                WashAll(panel);
                Button(panel, "Finish").onClick.Invoke();
                yield return Capture(panel, camera, size, "clean");
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            Object.Destroy(cameraRoot);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator DisableReenableAndSceneReloadDoNotRetainHeldInputOrListeners()
        {
            yield return Load();
            StationPanelView panel = Object.FindObjectOfType<StationPanelView>();
            Button(panel, "Enter").onClick.Invoke();
            panel.Animals[0].GetComponent<Button>().onClick.Invoke();
            Button(panel, "Check").onClick.Invoke();
            Button(panel, "Clean").onClick.Invoke();
            Stroke(panel, new Vector2(0.35f, 0.6f), 1, false);
            long amount = DirtAmount(panel.Surface.GetComponentInChildren<RawImage>());
            panel.gameObject.SetActive(false);
            panel.gameObject.SetActive(true);
            yield return null;
            Assert.That(DirtAmount(panel.Surface.GetComponentInChildren<RawImage>()), Is.EqualTo(amount));
            WashAll(panel);
            Button(panel, "Finish").onClick.Invoke();
            Assert.That(Label(panel, "Clock").text, Does.Contain("09:20"));
            yield return Load();
            panel = Object.FindObjectOfType<StationPanelView>();
            Assert.That(Label(panel, "Clock").text, Does.Contain("09:00"));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator HoldingCleansLocallyAndForeignPointerCannotTakeOverTheBrush()
        {
            yield return Load();
            StationPanelView panel = Object.FindObjectOfType<StationPanelView>();
            Button(panel, "Enter").onClick.Invoke();
            yield return null;
            ClickAnimal(panel, 2);
            Button(panel, "Check").onClick.Invoke();
            Button(panel, "Clean").onClick.Invoke();
            RawImage overlay = panel.Surface.GetComponentInChildren<RawImage>();
            Stroke(panel, new Vector2(0.35f, 0.60f), 1, false);
            long beforeHold = DirtAmount(overlay);
            var foreign = new PointerEventData(EventSystem.current) { pointerId = 99 };
            panel.Surface.OnPointerUp(foreign);
            panel.Surface.OnDrag(foreign);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(DirtAmount(overlay), Is.LessThan(beforeHold).And.GreaterThan(0));
            panel.SendMessage("OnApplicationPause", true);
            long paused = DirtAmount(overlay);
            yield return new WaitForSecondsRealtime(0.1f);
            panel.SendMessage("OnApplicationPause", false);
            yield return null;
            Assert.That(DirtAmount(overlay), Is.EqualTo(paused));
            Assert.That(Label(panel, "Clock").text, Does.Contain("09:00"));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        private static IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters());
            yield return null;
        }

        private static void WashAll(StationPanelView panel)
        {
            Canvas.ForceUpdateCanvases();
            for (int y = 0; y <= 10; y++)
            {
                for (int x = 0; x <= 10; x++)
                {
                    Stroke(panel, new Vector2(x / 10f, y / 10f), 12);
                }
            }
        }

        private static void Stroke(StationPanelView panel, Vector2 normalized, int repetitions, bool release = true)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)panel.Surface.transform;
            Vector2 local = rect.rect.min + Vector2.Scale(rect.rect.size, normalized);
            Canvas canvas = panel.GetComponent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(local));
            var data = new PointerEventData(EventSystem.current) { position = screen, pointerId = -1 };
            if (camera != null)
            {
                data.pointerPressRaycast = new RaycastResult { module = panel.GetComponent<GraphicRaycaster>() };
            }

            for (int index = 0; index < repetitions; index++)
            {
                panel.Surface.OnPointerDown(data);
                if (release) panel.Surface.OnPointerUp(data);
            }
        }

        private static long DirtAmount(RawImage image)
        {
            return ((Texture2D)image.texture).GetPixels32().Sum(pixel => (long)pixel.a);
        }

        private static void ClickAnimal(StationPanelView panel, int index)
        {
            Canvas.ForceUpdateCanvases();
            Image art = panel.Animals[index].GetComponentsInChildren<Image>()
                .Single(value => value.name == "Animal");
            var rect = (RectTransform)art.transform;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var data = new PointerEventData(EventSystem.current) { position = screen, pointerId = -1 };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "No clickable animal surface.");
            GameObject handler = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data,
                ExecuteEvents.pointerClickHandler);
            Assert.That(handler, Is.EqualTo(panel.Animals[index].gameObject),
                "Top raycast: " + hits[0].gameObject.name + ", screen: " + screen);
        }

        private static Button Button(StationPanelView panel, string name)
        {
            return panel.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
        }

        private static Text Label(StationPanelView panel, string name)
        {
            return panel.GetComponentsInChildren<Text>(true).Single(label => label.name == name);
        }

        private static IEnumerator Capture(StationPanelView panel, Camera camera, Vector2Int size, string page)
        {
            var target = new RenderTexture(size.x, size.y, 24);
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (Image art in panel.GetComponentsInChildren<Image>().Where(value => value.name == "Animal"))
            {
                Assert.That(art.sprite, Is.Not.Null, page + " animal sprite missing");
                Assert.That(((RectTransform)art.transform).rect.width, Is.GreaterThan(20));
                Assert.That(((RectTransform)art.transform).rect.height, Is.GreaterThan(20));
            }
            foreach (Button button in panel.GetComponentsInChildren<Button>().Where(button => button.interactable))
            {
                var corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Vector3 bottom = camera.WorldToViewportPoint(corners[0]);
                Vector3 top = camera.WorldToViewportPoint(corners[2]);
                Assert.That(bottom.x, Is.GreaterThanOrEqualTo(-0.001f), button.name);
                Assert.That(bottom.y, Is.GreaterThanOrEqualTo(-0.001f), button.name);
                Assert.That(top.x, Is.LessThanOrEqualTo(1.001f), button.name);
                Assert.That(top.y, Is.LessThanOrEqualTo(1.001f), button.name);
            }

            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            Color32[] pixels = image.GetPixels32();
            string folder = Path.GetFullPath("../../station-screenshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, $"{page}-{size.x}x{size.y}.png"), image.EncodeToPNG());
            Assert.That(pixels.Count(pixel => pixel.r > pixel.b + 15 && pixel.g > pixel.b + 5),
                Is.GreaterThan(size.x * size.y / 300), "Sample animal art is missing: " + page);
            Object.Destroy(image);
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
        }
    }
}
#endif
