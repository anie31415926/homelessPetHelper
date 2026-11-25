#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public IEnumerator BrowseShowsOnlyMapNamesAndBottomLeftBackAndModalBlocksUnderlyingInput()
        {
            yield return Load();
            Assert.That(_view.IsDetailsOpen, Is.False);
            Assert.That(_view.GetComponentsInChildren<Button>().Where(button =>
                button.transform.parent.name != "MapArt").Select(button => button.name),
                Is.EqualTo(new[] { "Cancel" }));
            var back = Button("Cancel");
            var backRect = (RectTransform)back.transform;
            var backPointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, backRect.TransformPoint(backRect.rect.center)) };
            var backHits = new System.Collections.Generic.List<RaycastResult>();
            _view.GetComponent<GraphicRaycaster>().Raycast(backPointer, backHits);
            Assert.That(backHits, Is.Not.Empty);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(backHits[0].gameObject),
                Is.SameAs(back.gameObject));
            Assert.That(_view.GetComponentsInChildren<Text>().Where(label =>
                !string.IsNullOrWhiteSpace(label.text) && label.GetComponentInParent<ToastView>() == null).All(label =>
                label.transform.IsChildOf(Map().content)), Is.True,
                "Only map labels may remain: " + string.Join(",", _view.GetComponentsInChildren<Text>()
                    .Where(label => !string.IsNullOrWhiteSpace(label.text) &&
                        label.GetComponentInParent<ToastView>() == null &&
                        !label.transform.IsChildOf(Map().content)).Select(label => label.name + "=" + label.text)));
            int departures = 0;
            _view.PrimaryClicked += () => departures++;
            Button("Primary").onClick.Invoke();
            Assert.That(departures, Is.Zero, "Hidden departure controls must reject stale callbacks.");
            var art = Button("Yard").image.sprite;
            Button("Yard").onClick.Invoke();
            Assert.That(_view.IsDetailsOpen, Is.True);
            Assert.That(Map().enabled, Is.False);
            Assert.That(back.isActiveAndEnabled, Is.False);
            Assert.That(Button("Primary").isActiveAndEnabled, Is.True);
            Assert.That(((Image)Button("Primary").targetGraphic).sprite.name, Is.EqualTo("Action"));
            string heading = Label("Heading").text;
            Button("Parking").onClick.Invoke();
            Assert.That(Label("Heading").text, Is.EqualTo(heading), "Modal blocks stale landmark callbacks.");
            yield return Settle();
            var pointer = new PointerEventData(EventSystem.current)
            { position = new Vector2(Screen.width * .95f, Screen.height * .95f) };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            _view.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.transform.IsChildOf(Map().content), Is.False);
            Assert.That(Button("MapModalBackdrop").image.raycastTarget, Is.True);
            Assert.That(((RectTransform)Button("MapModalBackdrop").transform).rect.size,
                Is.EqualTo(((RectTransform)_view.transform).rect.size));
            Button("ClosePlace").onClick.Invoke();
            Assert.That(_view.IsDetailsOpen, Is.False);
            Assert.That(Map().enabled, Is.True);
            Assert.That(back.isActiveAndEnabled, Is.True);
            Assert.That(Button("Yard").image.sprite, Is.SameAs(art));
            _view.OpenDetails();
            Button("MapModalBackdrop").onClick.Invoke();
            Assert.That(_view.IsDetailsOpen, Is.False, "Backdrop dismisses the detail without departing.");
            _view.OpenDetails();
            _view.gameObject.SetActive(false); _view.gameObject.SetActive(true);
            yield return null;
            Assert.That(_view.IsDetailsOpen, Is.False, "Reopen returns to a clean browsing state.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FullscreenMapAndPopupRespectPhoneTabletAndInsetBounds()
        {
            yield return Load();
            var canvas = _view.GetComponent<Canvas>();
            var camera = new GameObject("FullscreenMapEvidence", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            var safe = _view.GetComponentInChildren<SafeAreaView>();
            try
            {
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844),
                    new Vector2Int(540, 1170), new Vector2Int(768, 1024) })
                {
                    var target = new RenderTexture(size.x, size.y, 24);
                    try
                    {
                        camera.targetTexture = target;
                        _view.CloseDetails();
                        yield return Settle();
                        AssertFullscreen(canvas);
                        AssertBottomLeftBack((RectTransform)safe.transform);
                        Save(camera, target, "map-" + size.x + "x" + size.y);
                        Button("Yard").onClick.Invoke();
                        yield return Settle();
                        AssertPopup((RectTransform)safe.transform);
                        Save(camera, target, "place-" + size.x + "x" + size.y);
                    }
                    finally { camera.targetTexture = null; target.Release(); Object.Destroy(target); }
                }
                safe.enabled = false;
                var inset = (RectTransform)safe.transform;
                inset.anchorMin = new Vector2(.03f, .05f); inset.anchorMax = new Vector2(.97f, .94f);
                var insetTarget = new RenderTexture(390, 844, 24);
                try
                {
                    camera.targetTexture = insetTarget; _view.CloseDetails();
                    yield return Settle(); AssertFullscreen(canvas);
                    AssertBottomLeftBack(inset);
                    Save(camera, insetTarget, "map-insets");
                    Button("Porch").onClick.Invoke(); yield return Settle();
                    AssertPopup(inset);
                    Assert.That(Button("Primary").IsInteractable(), Is.False);
                    Assert.That(Label("Feedback").text, Does.Contain("暂未开放"));
                    AssertInside((RectTransform)Button("ClosePlace").transform, inset);
                    var backdrop = (RectTransform)Button("MapModalBackdrop").transform;
                    Assert.That(backdrop.rect.size, Is.EqualTo(((RectTransform)canvas.transform).rect.size));
                    Save(camera, insetTarget, "place-unavailable-insets");
                    _view.CloseDetails(); _view.ShowNotice(Label("Feedback").text);
                    yield return null;
                    var toast = _view.GetComponentInChildren<ToastView>();
                    Assert.That(toast.IsShowing, Is.True);
                    Assert.That(toast.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                    _view.gameObject.SetActive(false); _view.gameObject.SetActive(true);
                    Assert.That(toast.IsShowing, Is.False);
                }
                finally { camera.targetTexture = null; insetTarget.Release(); Object.Destroy(insetTarget); }
            }
            finally { canvas.renderMode = RenderMode.ScreenSpaceOverlay; Object.Destroy(camera.gameObject); }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FlowerAndCloudsAnimateWithoutMovingTheHitAreaOrChangingPointLockBehavior()
        {
            yield return Load();
            var market = Button("Market");
            var hitArea = (RectTransform)market.transform;
            var label = market.GetComponentsInChildren<Text>().Single(text => text.enabled);
            const string flowerName = "\u6e56\u7554\u82b1\u6d77";
            Assert.That(label.text, Is.EqualTo(flowerName));
            Assert.That(flowerName.All(label.font.HasCharacter), Is.True);
            var atmosphere = _view.GetComponentInChildren<CityMapAtmosphere>();
            var flower = _view.GetComponentInChildren<CityMapFlowerSway>();
            Assert.That(atmosphere.raycastTarget, Is.False);
            Vector2 hitPosition = hitArea.anchoredPosition;
            Vector2 hitSize = hitArea.rect.size;
            Vector2 flowerPosition = ((RectTransform)flower.transform).anchoredPosition;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(hitArea.anchoredPosition, Is.EqualTo(hitPosition));
            Assert.That(hitArea.rect.size, Is.EqualTo(hitSize));
            Assert.That(((RectTransform)flower.transform).anchoredPosition, Is.EqualTo(flowerPosition));
            Button("Market").onClick.Invoke();
            Assert.That(_view.IsDetailsOpen, Is.True);
            Assert.That(Label("Heading").text, Is.EqualTo(flowerName));
            _view.CloseDetails();

            var canvas = _view.GetComponent<Canvas>();
            var camera = new GameObject("AtmosphereEvidence", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            var target = new RenderTexture(390, 844, 24);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            camera.targetTexture = target;
            try
            {
                yield return Settle();
                Map().FocusOn(hitArea);
                yield return Settle();
                Save(camera, target, "flower-live-a");
                yield return new WaitForSecondsRealtime(.7f);
                yield return Settle();
                Save(camera, target, "flower-live-b");
                _view.RenderPoint("market", flowerName, "Locked", PointDisplayState.Locked, false, true);
                Assert.That(label.enabled, Is.False);
                Assert.That(market.IsInteractable(), Is.False);
                market.onClick.Invoke();
                Assert.That(_view.IsDetailsOpen, Is.False);
                _view.RenderPoint("market", flowerName, "Open", PointDisplayState.NoRequest, false, true);
                Assert.That(label.enabled && market.IsInteractable(), Is.True);
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Object.Destroy(camera.gameObject);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RegionalFogCoversTerrainAndRevealsNeighborWithoutMovingOpenPoints()
        {
            yield return Load();
            var fog = _view.GetComponentInChildren<CityMapFog>();
            var atmosphere = _view.GetComponentInChildren<CityMapAtmosphere>();
            var canvas = _view.GetComponent<Canvas>();
            var camera = new GameObject("RegionFogEvidence", typeof(Camera)).GetComponent<Camera>();
            var target = new RenderTexture(768, 1024, 24);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            camera.targetTexture = target;
            try
            {
                _view.GetComponentInChildren<ToastView>(true).Clear();
                atmosphere.enabled = false;
                Map().normalizedPosition = new Vector2(0, 1);
                yield return Settle();
                Assert.That(fog.raycastTarget, Is.False);
                Assert.That(fog.GetComponentInParent<RectMask2D>(), Is.Not.Null);
                var points = Map().content.GetComponentsInChildren<Button>()
                    .Where(button => button.transform.parent == Map().content).ToArray();
                Assert.That(points.Length, Is.EqualTo(7));
                Assert.That(points.All(button => button.IsInteractable()), Is.True);
                Vector2[] anchors = points.Select(button => ((RectTransform)button.transform).anchorMin).ToArray();
                var dock = new Vector2(.29f, .82f);
                var island = new Vector2(.24f, .945f);
                var outside = new Vector2(.05f, .49f);
                var path = new Vector2(.49f, .50f);
                Vector2[] spots = { dock, island, outside, path };

                Color[] before = ReadRegions(camera, target, spots, "region-fog-before");
                fog.enabled = false;
                yield return Settle();
                Color[] bare = ReadRegions(camera, target, spots, null);
                fog.enabled = true;
                for (int i = 0; i < 3; i++)
                    Assert.That(ColorDistance(before[i], bare[i]), Is.GreaterThan(.22f), spots[i].ToString());
                Assert.That(ColorDistance(before[3], bare[3]), Is.LessThan(.035f));

                fog.PreviewRevealed = true;
                yield return new WaitForSecondsRealtime(1.4f);
                yield return Settle();
                Color[] after = ReadRegions(camera, target, spots, "region-fog-revealed");
                for (int i = 0; i < 2; i++)
                    Assert.That(ColorDistance(after[i], bare[i]), Is.LessThan(.035f));
                for (int i = 2; i < 4; i++)
                    Assert.That(ColorDistance(after[i], before[i]), Is.LessThan(.035f));
                Assert.That(points.Select(button => ((RectTransform)button.transform).anchorMin),
                    Is.EqualTo(anchors));
                Assert.That(points.All(button => button.IsInteractable()), Is.True);
            }
            finally
            {
                fog.PreviewRevealed = false;
                atmosphere.enabled = true;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Object.Destroy(camera.gameObject);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private Color[] ReadRegions(Camera camera, RenderTexture target, Vector2[] spots, string name)
        {
            Texture2D image = Capture(camera, target);
            try
            {
                if (name != null) WriteEvidence(image, name);
                Rect area = Map().content.rect;
                return spots.Select(spot =>
                {
                    Vector2 p = area.min + Vector2.Scale(spot, area.size);
                    Vector3 screen = camera.WorldToScreenPoint(Map().content.TransformPoint(p));
                    Assert.That(screen.x, Is.InRange(1, target.width - 2));
                    Assert.That(screen.y, Is.InRange(1, target.height - 2));
                    return image.GetPixel(Mathf.RoundToInt(screen.x), Mathf.RoundToInt(screen.y));
                }).ToArray();
            }
            finally { Object.Destroy(image); }
        }

        private static float ColorDistance(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        [UnityTest]
        public IEnumerator LongDetailsScrollWithActionsOutsideMaskAndFreshSelectionResetsBody()
        {
            yield return Load(); Button("Yard").onClick.Invoke();
            var details = _view.GetComponentsInChildren<ScrollRect>(true).Single(scroll => scroll != Map());
            string paragraph = Label("Details").text;
            _view.Render("空余安置位 999    ·    已用时 999999 分钟", Label("Heading").text,
                string.Join("\n", Enumerable.Repeat(paragraph, 24)), Label("Departure").text,
                "安置位已满，请先腾出位置。", "前往这里", true, false, false);
            yield return Settle();
            Assert.That(details.content.rect.height, Is.GreaterThan(details.viewport.rect.height));
            Vector3 action = Button("Primary").transform.position;
            details.verticalNormalizedPosition = 0; yield return null;
            Assert.That(details.verticalNormalizedPosition, Is.EqualTo(0).Within(.01f));
            Assert.That(Button("Primary").transform.position, Is.EqualTo(action));
            Assert.That(Button("Primary").GetComponentInParent<RectMask2D>(), Is.Null);
            Assert.That(Button("ClosePlace").GetComponentInParent<RectMask2D>(), Is.Null);
            Button("ClosePlace").onClick.Invoke(); Button("Parking").onClick.Invoke();
            yield return Settle();
            Assert.That(details.verticalNormalizedPosition, Is.EqualTo(1).Within(.01f));
            Assert.That(Button("Primary").IsInteractable(), Is.True);
            _view.SendMessage("OnApplicationFocus", false);
            Assert.That(Button("Primary").IsInteractable(), Is.False);
            _view.SendMessage("OnApplicationFocus", true);
            Assert.That(Button("Primary").IsInteractable(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            Object.FindObjectOfType<StationPanelView>().GetComponentsInChildren<Button>()
                .Single(button => button.name == "Restart").onClick.Invoke();
            _view = Object.FindObjectOfType<CityMapView>();
            _metrics = AssetDatabase.LoadAssetAtPath<UiLayoutMetrics>("Assets/Settings/UI/LayoutMetrics.asset");
            yield return Settle();
        }
        private static IEnumerator Settle()
        { for (int frame = 0; frame < 5; frame++) { yield return null; Canvas.ForceUpdateCanvases(); } }
        private void AssertFullscreen(Canvas canvas)
        {
            var corners = new Vector3[4]; Map().viewport.GetWorldCorners(corners);
            var bounds = ((RectTransform)canvas.transform).rect;
            Assert.That(((Vector2)canvas.transform.InverseTransformPoint(corners[0]) - bounds.min).magnitude,
                Is.LessThan(.5f));
            Assert.That(((Vector2)canvas.transform.InverseTransformPoint(corners[2]) - bounds.max).magnitude,
                Is.LessThan(.5f));
            Assert.That(_view.IsDetailsOpen, Is.False);
        }
        private void AssertPopup(RectTransform safe)
        {
            AssertInside((RectTransform)Label("Heading").transform.parent.parent, safe);
            foreach (string name in new[] { "Primary", "ClosePlace" })
            {
                var rect = (RectTransform)Button(name).transform;
                AssertInside(rect, safe);
                Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(_metrics.touchSize - 1));
                Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(_metrics.touchSize - 1));
            }
            foreach (var label in _view.GetComponentsInChildren<Text>())
            {
                if (string.IsNullOrWhiteSpace(label.text)) continue;
                Assert.That(label.resizeTextForBestFit, Is.False, label.name);
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(_metrics.labelSize), label.name);
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 3),
                    label.name);
            }
        }
        private void AssertBottomLeftBack(RectTransform safe)
        {
            var back = (RectTransform)Button("Cancel").transform;
            AssertInside(back, safe);
            Vector2 bottomLeft = safe.InverseTransformPoint(back.TransformPoint(back.rect.min));
            Vector2 inset = bottomLeft - safe.rect.min;
            Assert.That(inset.x, Is.EqualTo(_metrics.margin).Within(1));
            Assert.That(inset.y, Is.EqualTo(_metrics.margin).Within(1));
            Assert.That(back.rect.width, Is.GreaterThanOrEqualTo(_metrics.touchSize));
            Assert.That(back.rect.height, Is.GreaterThanOrEqualTo(_metrics.touchSize));
        }

        private static void AssertInside(RectTransform rect, RectTransform bounds)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var local = bounds.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(bounds.rect.xMin - 2, bounds.rect.xMax + 2), rect.name);
                Assert.That(local.y, Is.InRange(bounds.rect.yMin - 2, bounds.rect.yMax + 2), rect.name);
            }
        }
        private static void Save(Camera camera, RenderTexture target, string name)
        {
            Texture2D image = Capture(camera, target);
            try { WriteEvidence(image, name); }
            finally { Object.Destroy(image); }
        }

        private static Texture2D Capture(Camera camera, RenderTexture target)
        {
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            RenderTexture.active = previous;
            return image;
        }

        private static void WriteEvidence(Texture2D image, string name)
        {
            Directory.CreateDirectory("FullscreenMapEvidence");
            File.WriteAllBytes("FullscreenMapEvidence/" + name + ".png", image.EncodeToPNG());
        }
        private CityMapScrollRect Map() => _view.GetComponentInChildren<CityMapScrollRect>();
        private Button Button(string name) => _view.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        private Text Label(string name) => _view.GetComponentsInChildren<Text>(true).Single(t => t.name == name);
    }
}
#endif
