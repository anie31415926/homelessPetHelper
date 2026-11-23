#if UNITY_EDITOR
using System;
using HomelessPetHelper.Tests.Support;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class CityMapPreviewTests
    {
        private MainSceneStorageScope _storageScope;
        [SetUp] public void IsolateMainStorage() => _storageScope = new MainSceneStorageScope();
        [TearDown] public void ReleaseMainStorage() => _storageScope.Dispose();
        private const string ScenePath = "Assets/EditorScenes/SC_CityMap.unity";
        private CityMapView _view;
        private TextTable _copy;

        [Serializable] private sealed class TextTable { public Entry[] entries = Array.Empty<Entry>(); }
        [Serializable] private sealed class Entry
        {
            public string key = string.Empty;
            public string value = string.Empty;
        }

        [UnityTest]
        public IEnumerator MainMapAndStationUseExpandedCapacityAcrossRefreshAndReload()
        {
            _storageScope.Dispose();
            var storage = new MemorySaveStorage();
            _storageScope = new MainSceneStorageScope(() => storage);
            _copy = JsonUtility.FromJson<TextTable>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Game/CityMapText.json").text);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
            yield return null;
            var root = UnityEngine.Object.FindObjectOfType<HomelessPetHelper.Game.MainGameRoot>();
            var session = root.Presenter.Session;
            Assert.That(session.Station.TryExpandTemporaryCapacity(session.StationSessionId, 7, out _), Is.True);
            var station = UnityEngine.Object.FindObjectOfType<HomelessPetHelper.Modules.Station.UI.StationPanelView>();
            station.GetComponentsInChildren<Button>().Single(button => button.name == "Restart").onClick.Invoke();
            _view = UnityEngine.Object.FindObjectOfType<CityMapView>();
            Assert.That(Label("Status").text, Is.EqualTo(Copy("status", 7, 0)));
            Assert.That(Button("Primary").IsInteractable(), Is.True);
            Assert.That(session.Station.TryExpandTemporaryCapacity(session.StationSessionId, 9, out _), Is.True);
            Click("Yard");
            Assert.That(Label("Status").text, Is.EqualTo(Copy("status", 9, 0)));
            Click("Cancel");
            var content = AssetDatabase.LoadAssetAtPath<HomelessPetHelper.Game.GameContent>(
                "Assets/Settings/Game/GameContent.asset");
            var copy = new HomelessPetHelper.Game.Text.OptionPreviewText(content.StationText);
            station.GetComponentsInChildren<Button>()
                .Single(button => button.name == "StationMenu").onClick.Invoke();
            Assert.That(station.GetComponentsInChildren<Text>().Any(label =>
                label.text == copy.Get("capacity", 0, 9)), Is.True);
            station.GetComponentsInChildren<Button>()
                .Single(button => button.name == "CloseStationMenu").onClick.Invoke();
            Assert.That(storage.Writes, Is.EqualTo(2), "Opening and returning each save one stable checkpoint.");
            var stationId = session.StationSessionId;
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
            yield return null;
            root = UnityEngine.Object.FindObjectOfType<HomelessPetHelper.Game.MainGameRoot>();
            Assert.That(root.Presenter.Session.StationSessionId, Is.Not.EqualTo(stationId));
            Assert.That(root.Presenter.Session.Station.TemporaryCapacity, Is.EqualTo(9));
            Assert.That(root.Presenter.Session.Station.AvailableTemporaryPlaces, Is.EqualTo(9));
            Assert.That(storage.Writes, Is.EqualTo(2), "Reload must read the same save without rewriting it.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SavedMapSelectsFreelyPausesAndCommitsEachReturnOnce()
        {
            yield return Load();
            string initialStatus = Label("Status").text;
            Image terrain = Button("Parking").transform.parent.GetComponent<Image>();
            Sprite terrainSprite = terrain.sprite;
            Image[] artwork = new[] { Button("Parking").image, Button("Yard").image, Button("Porch").image };
            Sprite[] originalSprites = artwork.Select(image => image.sprite).ToArray();
            Click("Yard");
            Click("Porch");
            Assert.That(artwork.Select(image => image.sprite), Is.EqualTo(originalSprites));
            Assert.That(artwork.All(image => image.color == Color.white), Is.True,
                "Selection must highlight nameplates, not recolor the replaceable artwork.");
            artwork[0].sprite = originalSprites[1];
            Assert.That(terrain.sprite, Is.SameAs(terrainSprite));
            artwork[0].sprite = originalSprites[0];
            Assert.That(Label("Status").text, Is.EqualTo(initialStatus));
            Assert.That(Label("Heading").text, Is.EqualTo(Copy("porch")));
            Toggle pause = _view.GetComponentInChildren<Toggle>();
            pause.isOn = true;
            Assert.That(Button("Primary").interactable, Is.False);
            Click("Primary");
            Click("Yard");
            Assert.That(Label("Heading").text, Is.EqualTo(Copy("porch")));
            pause.isOn = false;
            Click("Cancel");
            Assert.That(Label("Details").text, Is.EqualTo(Copy("cancelled")));
            Assert.That(Label("Status").text, Is.EqualTo(initialStatus));
            Click("Primary");
            for (int index = 0; index < 3; index++)
            {
                Click("Parking");
                Click("Primary");
                Assert.That(Label("Heading").text, Is.EqualTo(Copy("arrived", Copy("parking"))));
                Assert.That(Button("Parking").interactable, Is.False);
                Click("Yard");
                Click("Primary");
                Assert.That(Label("Heading").text, Is.EqualTo(Copy("station")));
                Assert.That(Label("Status").text, Is.EqualTo(Copy("status", 2, (index + 1) * 5)));
                Assert.That(Label("Details").text, Is.EqualTo(Copy("returned", 5)));
                Click("Primary");
            }
            Click("Yard");
            Click("Primary");
            Click("Primary");
            Assert.That(Label("Details").text, Is.EqualTo(Copy("returned", 5)));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
            yield return Load();
            Assert.That(Label("Status").text, Is.EqualTo(initialStatus));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator FocusApplicationAndManualPauseComposeAndReopenedMapIgnoresHiddenInput()
        {
            yield return Load();
            string status = Label("Status").text;
            Toggle pause = _view.GetComponentInChildren<Toggle>();
            pause.isOn = true;
            _view.SendMessage("OnApplicationFocus", false);
            _view.SendMessage("OnApplicationPause", true);
            _view.SendMessage("OnApplicationFocus", true);
            pause.isOn = false;
            Assert.That(Button("Primary").IsInteractable(), Is.False);
            Click("Primary");
            Assert.That(Label("Status").text, Is.EqualTo(status));
            _view.SendMessage("OnApplicationPause", false);
            Assert.That(Button("Primary").IsInteractable(), Is.True);
            pause.isOn = true;
            _view.gameObject.SetActive(false);
            Click("Primary");
            Click("Cancel");
            _view.gameObject.SetActive(true);
            Assert.That(_view.IsSuspended, Is.False, "A new visible map resets the manual pause input.");
            Assert.That(Label("Heading").text, Is.EqualTo(Copy("parking")));
            Assert.That(Button("Primary").IsInteractable(), Is.True);
            Assert.That(Label("Departure").text,
                Is.EqualTo(Copy("departure.preview", 5, Copy("space.required"))));
            Assert.That(Label("Status").text, Is.EqualTo(status));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator MapRendersSixRatiosWithBoundMarkersAndInsetControls()
        {
            yield return Load();
            Canvas canvas = _view.GetComponent<Canvas>();
            var camera = new GameObject("MapCaptureCamera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            string folder = Path.GetFullPath("../../city-map-screenshots");
            Directory.CreateDirectory(folder);
            var sizes = new[]
            {
                new Vector2Int(540, 960), new Vector2Int(390, 844), new Vector2Int(768, 1024),
                new Vector2Int(540, 1080), new Vector2Int(540, 1170), new Vector2Int(540, 1200)
            };
            foreach (Vector2Int size in sizes)
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertLayout(canvas);
                AssertLayeredMarkers(camera);
                PrepareCapture("Parking");
                Capture(camera, target, Path.Combine(folder, $"map-{size.x}x{size.y}.png"));
                if (size == sizes[0]) CaptureTerrainOnly(camera, target, folder);
                Click("Porch");
                Click("Primary");
                Canvas.ForceUpdateCanvases();
                AssertLayout(canvas);
                PrepareCapture("Porch");
                Capture(camera, target, Path.Combine(folder, $"arrived-{size.x}x{size.y}.png"));
                Click("Primary");
                Click("Primary");
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            var safe = _view.GetComponentInChildren<SafeAreaView>();
            safe.enabled = false;
            var safeRect = (RectTransform)safe.transform;
            safeRect.anchorMin = new Vector2(0.03f, 0.05f);
            safeRect.anchorMax = new Vector2(0.97f, 0.94f);
            var insetTarget = new RenderTexture(540, 1170, 24);
            camera.targetTexture = insetTarget;
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertLayout(canvas);
            PrepareCapture("Parking");
            Capture(camera, insetTarget, Path.Combine(folder, "map-simulated-insets.png"));
            camera.targetTexture = null;
            insetTarget.Release();
            UnityEngine.Object.Destroy(insetTarget);
            UnityEngine.Object.Destroy(camera.gameObject);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        private IEnumerator Load()
        {
            _copy = JsonUtility.FromJson<TextTable>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Game/CityMapText.json").text);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters());
            yield return null;
            _view = UnityEngine.Object.FindObjectOfType<CityMapView>();
            Assert.That(_view, Is.Not.Null);
        }

        private void PrepareCapture(string point)
        {
            _view.GetComponentInChildren<CityMapScrollRect>().FocusOn((RectTransform)Button(point).transform);
            foreach (var scroll in _view.GetComponentsInChildren<ScrollRect>())
                if (!(scroll is CityMapScrollRect)) scroll.verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
        }

        private void AssertLayout(Canvas canvas)
        {
            Rect bounds = ((RectTransform)canvas.transform).rect;
            var corners = new Vector3[4];
            foreach (Selectable control in _view.GetComponentsInChildren<Selectable>())
            {
                LevelUiPlayTests.ScrollIntoView(control);
                var rect = (RectTransform)control.transform;
                bool isPlace = control.transform.parent.name == "MapArt";
                Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(144), control.name);
                Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(144), control.name);
                if (!isPlace) Assert.That(rect.rect.height, Is.LessThanOrEqualTo(180), control.name);
                rect.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = canvas.transform.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(bounds.xMin - 1, bounds.xMax + 1), control.name);
                    Assert.That(local.y, Is.InRange(bounds.yMin - 1, bounds.yMax + 1), control.name);
                }
            }
            foreach (Text label in _view.GetComponentsInChildren<Text>())
            {
                LevelUiPlayTests.ScrollIntoView(label);
                var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                if (label.resizeTextForBestFit && !string.IsNullOrWhiteSpace(label.text))
                {
                    // Measure the font size actually rendered on the current aspect ratio.
                    settings.fontSize = label.cachedTextGenerator.fontSizeUsedForBestFit;
                    settings.resizeTextForBestFit = false;
                }
                float height = new TextGenerator().GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
                Assert.That(height, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), label.name);
                if (!string.IsNullOrWhiteSpace(label.text))
                    Assert.That(label.cachedTextGenerator.vertexCount, Is.GreaterThan(4), label.name);
            }
        }

        private void AssertLayeredMarkers(Camera camera)
        {
            Transform terrain = Button("Parking").transform.parent;
            Assert.That(terrain.GetComponent<Image>().sprite, Is.Not.Null);
            foreach (Button button in _view.GetComponentsInChildren<Button>()
                .Where(item => item.transform.parent == terrain))
            {
                Assert.That(button.transform.parent, Is.SameAs(terrain));
                Assert.That(button.image.sprite, Is.Not.Null);
                var rect = (RectTransform)button.transform;
                Assert.That(rect.anchorMax.x, Is.GreaterThan(rect.anchorMin.x));
                Assert.That(rect.anchorMax.y, Is.GreaterThan(rect.anchorMin.y));
                Assert.That(rect.rect.width / rect.rect.height,
                    Is.EqualTo(button.image.sprite.rect.width / button.image.sprite.rect.height).Within(0.01f));
                Image plate = button.GetComponentsInChildren<Image>().Single(image => image.name == "Nameplate");
                AssertHit(button, rect, camera);
                AssertHit(button, plate.rectTransform, camera);
            }
        }

        private void AssertHit(Button expected, RectTransform rect, Camera camera)
        {
            LevelUiPlayTests.ScrollIntoView(expected);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            _view.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), expected.name);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(expected), expected.name);
        }

        private void CaptureTerrainOnly(Camera camera, RenderTexture target, string folder)
        {
            Image terrain = Button("Parking").transform.parent.GetComponent<Image>();
            Sprite original = terrain.sprite;
            GameObject[] layers = terrain.GetComponentsInChildren<Image>()
                .Where(image => image.transform.parent == terrain.transform)
                .Select(image => image.gameObject).ToArray();
            Assert.That(layers.Length, Is.GreaterThanOrEqualTo(_view.GetComponentsInChildren<Button>()
                .Count(button => button.transform.parent == terrain.transform)));
            try
            {
                foreach (GameObject layer in layers) layer.SetActive(false);
                Canvas.ForceUpdateCanvases();
                Assert.That(terrain.sprite, Is.SameAs(original));
                Capture(camera, target, Path.Combine(folder, "map-terrain-only.png"));
            }
            finally
            {
                foreach (GameObject layer in layers) layer.SetActive(true);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void Capture(Camera camera, RenderTexture target, string path)
        {
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            Color32[] pixels = image.GetPixels32();
            int illustrated = pixels.Count(p => Math.Max(p.r, Math.Max(p.g, p.b))
                - Math.Min(p.r, Math.Min(p.g, p.b)) > 25 && p.g > p.b);
            Assert.That(illustrated, Is.GreaterThan(pixels.Length / 5), "Missing map artwork.");
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
        }

        private string Copy(string key, params object[] args) =>
            string.Format(_copy.entries.Single(entry => entry.key == key).value, args);
        private Button Button(string name) => _view.GetComponentsInChildren<Button>(true)
            .Single(button => button.name == name);
        private Text Label(string name) => _view.GetComponentsInChildren<Text>()
            .Single(label => label.name == name);
        private void Click(string name) => Button(name).onClick.Invoke();
    }
}
#endif
