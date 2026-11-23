#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class CityMapUiRefinementTests
    {
        private MainSceneStorageScope _storage;
        private CityMapView _view;
        private UiLayoutMetrics _metrics;

        [SetUp] public void SetUp() => _storage = new MainSceneStorageScope();
        [TearDown] public void TearDown() => _storage.Dispose();

        [UnityTest]
        public IEnumerator MapUsesSharedPaperAndSelectedOutlineWithoutRecoloringLandmarks()
        {
            yield return Load();
            var primary = Button("Primary");
            Assert.That(((Image)primary.targetGraphic).sprite, Is.Not.Null, "Primary must use shared paper.");
            Assert.That(((Image)primary.targetGraphic).sprite.name, Is.EqualTo("Action"));
            Assert.That(((Image)Button("Cancel").targetGraphic).sprite.name, Is.EqualTo("Secondary"));
            Assert.That(primary.GetComponentInChildren<Text>().color, Is.EqualTo(_metrics.ink));
            var points = _view.GetComponentsInChildren<Button>()
                .Where(button => button.transform.parent.name == "MapArt").ToArray();
            var sprites = points.Select(point => point.image.sprite).ToArray();
            Button("Yard").onClick.Invoke();
            Assert.That(Plate("Yard").GetComponent<Outline>().enabled, Is.True);
            Assert.That(Plate("Parking").GetComponent<Outline>().enabled, Is.False);
            Assert.That(Plate("Yard").sprite.name, Is.EqualTo("Action"));
            _view.RenderPoint("riverside", Plate("Riverside").GetComponentsInChildren<Text>()
                .First(text => text.name != "State").text, "尚未开放",
                PointDisplayState.Locked, false, false);
            Assert.That(Button("Riverside").IsInteractable(), Is.False);
            Assert.That(Plate("Riverside").sprite.name, Is.EqualTo("Paper"));
            Assert.That(Plate("Riverside").GetComponent<Outline>().enabled, Is.False);
            var pause = _view.GetComponentInChildren<Toggle>();
            Assert.That(((Image)pause.graphic).sprite.name, Is.EqualTo("Paw"));
            pause.isOn = true;
            yield return new WaitUntil(() => pause.graphic.canvasRenderer.GetAlpha() >= .99f);
            Assert.That(primary.IsInteractable(), Is.False);
            Assert.That(((Image)primary.targetGraphic).sprite, Is.SameAs(_metrics.disabledButtonPaper));
            Assert.That(((Image)primary.targetGraphic).color, Is.EqualTo(_metrics.disabledPaperTint));
            Assert.That(Label("Feedback").text, Is.EqualTo("已暂停"));
            pause.isOn = false;
            yield return null;
            Assert.That(primary.IsInteractable(), Is.True);
            Assert.That(((Image)primary.targetGraphic).sprite.name, Is.EqualTo("Action"));
            Assert.That(points.Select(point => point.image.sprite), Is.EqualTo(sprites));
            Assert.That(points.All(point => point.image.color == Color.white), Is.True);
            _view.gameObject.SetActive(false); _view.gameObject.SetActive(true);
            yield return null;
            Assert.That(_view.IsSuspended, Is.False);
            Assert.That(Plate("Yard").GetComponent<Outline>().enabled, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PhoneTabletAndInsetLayoutsKeepTextActionsAndMapUsable()
        {
            yield return Load();
            var canvas = _view.GetComponent<Canvas>();
            var camera = new GameObject("MapStyleEvidence", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            var map = _view.GetComponentInChildren<CityMapScrollRect>();
            var details = _view.GetComponentsInChildren<ScrollRect>().Single(scroll => scroll != map);
            var safe = _view.GetComponentInChildren<SafeAreaView>();
            try
            {
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(360, 640),
                    new Vector2Int(390, 844), new Vector2Int(540, 960), new Vector2Int(540, 1080),
                    new Vector2Int(540, 1170), new Vector2Int(432, 960), new Vector2Int(768, 1024) })
                {
                    var target = new RenderTexture(size.x, size.y, 24);
                    try
                    {
                        camera.targetTexture = target;
                        for (int frame = 0; frame < 5; frame++) { yield return null; Canvas.ForceUpdateCanvases(); }
                        AssertLayout(map, details, (RectTransform)safe.transform);
                        Save(camera, target, "map-" + size.x + "x" + size.y);
                    }
                    finally { camera.targetTexture = null; target.Release(); Object.Destroy(target); }
                }
                safe.enabled = false;
                var inset = (RectTransform)safe.transform;
                inset.anchorMin = new Vector2(.03f, .05f); inset.anchorMax = new Vector2(.97f, .94f);
                var insetTarget = new RenderTexture(390, 844, 24);
                try
                {
                    camera.targetTexture = insetTarget;
                    for (int frame = 0; frame < 5; frame++) { yield return null; Canvas.ForceUpdateCanvases(); }
                    AssertLayout(map, details, inset);
                    Save(camera, insetTarget, "map-insets");
                    Button("Porch").onClick.Invoke();
                    yield return null;
                    Assert.That(Button("Primary").IsInteractable(), Is.False);
                    Assert.That(Label("Feedback").text, Does.Contain("暂未开放"));
                    Save(camera, insetTarget, "map-unavailable");
                    _view.GetComponentInChildren<Toggle>().isOn = true;
                    yield return null;
                    Save(camera, insetTarget, "map-paused");
                }
                finally { camera.targetTexture = null; insetTarget.Release(); Object.Destroy(insetTarget); }
            }
            finally { canvas.renderMode = RenderMode.ScreenSpaceOverlay; Object.Destroy(camera.gameObject); }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LongDetailAndLargeCountsScrollIndependentlyAndRecoverOnSelection()
        {
            yield return Load();
            var canvas = _view.GetComponent<Canvas>();
            var camera = new GameObject("MapLongDetailEvidence", typeof(Camera)).GetComponent<Camera>();
            var target = new RenderTexture(390, 844, 24);
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            try { yield return VerifyLongDetails(camera, target); }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                camera.targetTexture = null; target.Release();
                Object.Destroy(target); Object.Destroy(camera.gameObject);
            }
        }

        private IEnumerator VerifyLongDetails(Camera camera, RenderTexture target)
        {
            var map = _view.GetComponentInChildren<CityMapScrollRect>();
            var details = _view.GetComponentsInChildren<ScrollRect>().Single(scroll => scroll != map);
            string heading = Label("Heading").text;
            string paragraph = Label("Details").text;
            _view.Render("空余安置位 999    ·    已用时 999999 分钟", heading,
                string.Join("\n", Enumerable.Repeat(paragraph, 24)), Label("Departure").text,
                "安置位已满，请先腾出位置。", "前往这里", true, false, false);
            for (int frame = 0; frame < 5; frame++) { yield return null; Canvas.ForceUpdateCanvases(); }
            Vector3 primaryPosition = Button("Primary").transform.position;
            Vector2 mapPosition = map.content.anchoredPosition;
            Assert.That(details.content.rect.height, Is.GreaterThan(details.viewport.rect.height));
            details.verticalNormalizedPosition = 0;
            yield return null;
            Assert.That(details.verticalNormalizedPosition, Is.EqualTo(0).Within(.01f));
            Assert.That(map.content.anchoredPosition, Is.EqualTo(mapPosition));
            Assert.That(Button("Primary").transform.position, Is.EqualTo(primaryPosition));
            Save(camera, target, "map-long-details");
            AssertLayout(map, details, (RectTransform)_view.GetComponentInChildren<SafeAreaView>().transform);
            Assert.That(Label("Feedback").cachedTextGenerator.vertexCount, Is.GreaterThan(4));
            Assert.That(_view.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "DetailsOverflow"),
                Is.True, "Long details need a visible scroll affordance.");
            Button("Yard").onClick.Invoke();
            yield return null;
            Assert.That(details.verticalNormalizedPosition, Is.EqualTo(1).Within(.01f));
            Assert.That(Button("Primary").IsInteractable(), Is.True);
            _view.Render("空余安置位 2", heading, paragraph, string.Empty, string.Empty,
                "返回救助站", false, false, true);
            Assert.That(Label("Departure").gameObject.activeSelf, Is.False);
            Assert.That(Label("Feedback").gameObject.activeSelf, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            Object.FindObjectOfType<StationPanelView>().GetComponentsInChildren<Button>()
                .Single(button => button.name == "Restart").onClick.Invoke();
            _view = Object.FindObjectOfType<CityMapView>();
            _metrics = AssetDatabase.LoadAssetAtPath<UiLayoutMetrics>("Assets/Settings/UI/LayoutMetrics.asset");
            yield return null; Canvas.ForceUpdateCanvases();
        }

        private void AssertLayout(CityMapScrollRect map, ScrollRect details, RectTransform safe)
        {
            Assert.That(map.viewport.rect.height, Is.GreaterThan(details.viewport.rect.height));
            Assert.That(map.GetComponent<RectMask2D>().enabled, Is.True);
            foreach (var label in _view.GetComponentsInChildren<Text>())
            {
                if (string.IsNullOrWhiteSpace(label.text)) continue;
                Assert.That(label.resizeTextForBestFit, Is.False, label.name);
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(_metrics.labelSize), label.name);
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 3),
                    label.name);
                Assert.That(label.color, Is.EqualTo(_metrics.ink), label.name);
            }
            foreach (string name in new[] { "Departure", "Feedback" })
            {
                var label = Label(name);
                if (!label.gameObject.activeInHierarchy) continue;
                Assert.That(label.GetComponentInParent<RectMask2D>(), Is.Null,
                    name + " must remain outside scrolling.");
                var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = safe.InverseTransformPoint(corner);
                    Assert.That(local.y, Is.InRange(safe.rect.yMin, safe.rect.yMax), name);
                }
            }
            foreach (var control in _view.GetComponentsInChildren<Selectable>())
            {
                if (control.transform.parent == map.content) continue;
                var rect = (RectTransform)control.transform;
                Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(_metrics.touchSize), control.name);
                Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(_metrics.touchSize), control.name);
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 local = safe.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(safe.rect.xMin, safe.rect.xMax), control.name);
                    Assert.That(local.y, Is.InRange(safe.rect.yMin, safe.rect.yMax), control.name);
                }
                var feedback = control.GetComponent<UiMotionFeedback>();
                Assert.That(feedback, Is.Not.Null, control.name);
                Assert.That(feedback.GetComponentInParent<RectMask2D>(), Is.Null,
                    "New animated controls stay outside map/detail masks; landmarks keep fixed visuals.");
            }
        }

        private static void Save(Camera camera, RenderTexture target, string name)
        {
            camera.Render();
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                Directory.CreateDirectory("MapUiEvidence");
                File.WriteAllBytes("MapUiEvidence/" + name + ".png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.Destroy(image); }
        }

        private Button Button(string name) => _view.GetComponentsInChildren<Button>(true)
            .Single(button => button.name == name);
        private Text Label(string name) => _view.GetComponentsInChildren<Text>(true)
            .Single(text => text.name == name);
        private Image Plate(string name) => Button(name).GetComponentsInChildren<Image>()
            .Single(image => image.name == "Nameplate");
    }
}
#endif
