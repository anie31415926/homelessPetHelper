#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class CityMapPreviewTests
    {
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
        public IEnumerator SavedMapSelectsFreelyPausesAndCommitsEachReturnOnce()
        {
            yield return Load();
            string initialStatus = Label("Status").text;
            Click("Yard");
            Click("Porch");
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
            Assert.That(Label("Details").text, Is.EqualTo(Copy("returned.exploring", 5)));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
            yield return Load();
            Assert.That(Label("Status").text, Is.EqualTo(initialStatus));
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
                AssertMarkerAnchors();
                Capture(camera, target, Path.Combine(folder, $"map-{size.x}x{size.y}.png"));
                Click("Porch");
                Click("Primary");
                Canvas.ForceUpdateCanvases();
                AssertLayout(canvas);
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
                "Assets/EditorScenes/CityMapText.json").text);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters());
            yield return null;
            _view = UnityEngine.Object.FindObjectOfType<CityMapView>();
            Assert.That(_view, Is.Not.Null);
        }

        private void AssertLayout(Canvas canvas)
        {
            Rect bounds = ((RectTransform)canvas.transform).rect;
            var corners = new Vector3[4];
            foreach (Selectable control in _view.GetComponentsInChildren<Selectable>())
            {
                var rect = (RectTransform)control.transform;
                Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(87), control.name);
                Assert.That(rect.rect.height, Is.LessThanOrEqualTo(128), control.name);
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
                var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                float height = new TextGenerator().GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
                Assert.That(height, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), label.name);
            }
        }

        private void AssertMarkerAnchors()
        {
            var parking = (RectTransform)Button("Parking").transform;
            var yard = (RectTransform)Button("Yard").transform;
            var porch = (RectTransform)Button("Porch").transform;
            Assert.That(parking.parent, Is.SameAs(yard.parent));
            Assert.That(yard.parent, Is.SameAs(porch.parent));
            Assert.That(parking.parent.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(parking.anchorMin, Is.EqualTo(new Vector2(0.16f, 0.405f)));
            Assert.That(yard.anchorMin, Is.EqualTo(new Vector2(0.78f, 0.64f)));
            Assert.That(porch.anchorMin, Is.EqualTo(new Vector2(0.81f, 0.245f)));
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
            int illustrated = pixels.Count(p => p.g > p.r * 1.15f && p.g > p.b * 1.1f);
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
