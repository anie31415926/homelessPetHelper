#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class LevelUiPlayTests
    {
        private MainSceneStorageScope _storage;
        private MemorySaveStorage _guidance, _audio;
        private int _seed;
        [SetUp] public void SetUp()
        {
            _guidance = new MemorySaveStorage();
            _audio = new MemorySaveStorage();
            _seed = 0;
            _storage = new MainSceneStorageScope(freshGuidance: true,
                createGuidanceStorage: () => _guidance, createAudioStorage: () => _audio);
            SceneManager.sceneLoaded += Configure;
        }
        [TearDown] public void TearDown()
        { SceneManager.sceneLoaded -= Configure; _storage.Dispose(); }
        private void Configure(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Main") return;
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainGameRoot>(true))
                .Single().ConfigureOutingSeedSource(() => _seed);
        }
        private static void Click(Component owner, string name)
        {
            var button = owner.GetComponentsInChildren<Button>().Single(item => item.name == name);
            Assert.That(button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }
        private static void Depart()
        {
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            var map = Object.FindObjectOfType<CityMapView>();
            MainApproachDriver.Click(map, "Yard");
            MainApproachDriver.Click(map, "Primary");
        }

        [UnityTest]
        public IEnumerator ModalBackdropCoversCanvasOutsideTheSafeArea()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            Depart(); yield return null;
            var view = Object.FindObjectOfType<ApproachPanelView>();
            MainApproachDriver.Click(view, "Pause");
            foreach (var area in view.GetComponentsInChildren<SafeAreaView>())
            {
                area.enabled = false;
                var rect = (RectTransform)area.transform;
                rect.anchorMin = new Vector2(.04f, .06f);
                rect.anchorMax = new Vector2(.96f, .89f);
            }
            Canvas.ForceUpdateCanvases();
            var overlay = view.GetComponentInChildren<LevelOverlayView>();
            var backdrop = overlay.GetComponentsInChildren<Image>().Single(image =>
                image.name == "ModalBackdrop" || image.name == "Modal");
            AssertCanvasCoverage(view.GetComponent<Canvas>(), backdrop);
        }

        private static void AssertCanvasCoverage(Canvas canvas, Graphic backdrop)
        {
            var root = (RectTransform)canvas.transform;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, backdrop.transform);
            Assert.That(bounds.min.x, Is.LessThanOrEqualTo(root.rect.xMin + 1), "Left edge must be dimmed.");
            Assert.That(bounds.max.x, Is.GreaterThanOrEqualTo(root.rect.xMax - 1), "Right edge must be dimmed.");
            Assert.That(bounds.min.y, Is.LessThanOrEqualTo(root.rect.yMin + 1), "Bottom unsafe area must be dimmed.");
            Assert.That(bounds.max.y, Is.GreaterThanOrEqualTo(root.rect.yMax - 1), "Top cutout area must be dimmed.");
        }

        [UnityTest]
        public IEnumerator SettingsAndConfirmationsKeepParentPauseAndRestoreDraftOrPersistExplicitly()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            Depart(); yield return null;
            var root = Object.FindObjectOfType<MainGameRoot>();
            var view = Object.FindObjectOfType<ApproachPanelView>();
            var overlay = root.Presenter.Overlays;
            int guideStep = overlay.Guide.StepIndex;
            MainApproachDriver.Click(view, "Pause");
            Assert.That(view.Session.IsSuspended, Is.True);
            yield return Capture(view, "pause");
            Click(view, "OverlaySettings");
            var music = view.GetComponentsInChildren<Slider>().Single(item => item.name == "MusicVolume");
            music.value = .25f;
            Assert.That(root.Audio.Playback.Preferences.Music.Volume, Is.EqualTo(.25f));
            view.GetComponentsInChildren<Toggle>().Single(item => item.name == "MusicMute").isOn = true;
            Assert.That(root.Audio.Playback.Preferences.Music.IsMuted, Is.True);
            Assert.That(_audio.Writes, Is.Zero);
            yield return Capture(view, "settings");
            Click(view, "OverlayResetAudio");
            Assert.That(overlay.Depth, Is.EqualTo(3));
            yield return Capture(view, "settings-confirmation");
            view.SendMessage("OnApplicationFocus", false);
            view.SendMessage("OnApplicationFocus", true);
            Assert.That(overlay.Depth, Is.EqualTo(3));
            Assert.That(overlay.Guide.StepIndex, Is.EqualTo(guideStep));
            Click(view, "OverlayCancel");
            Assert.That(overlay.Top, Is.EqualTo(LevelPopupKind.Settings));
            Assert.That(view.Session.IsSuspended, Is.True);
            Click(view, "OverlayResetAudio");
            Click(view, "OverlayConfirm");
            Assert.That(root.Audio.Playback.Preferences.Music.Volume, Is.EqualTo(1));
            Assert.That(root.Audio.Playback.Preferences.Music.IsMuted, Is.False);
            Assert.That(_audio.Writes, Is.Zero);
            Click(view, "OverlayCancelSettings");
            Assert.That(root.Audio.Playback.Preferences.Music.Volume, Is.EqualTo(1));
            Click(view, "OverlaySettings");
            music.value = .35f;
            _audio.FailWrites = true;
            Click(view, "OverlaySaveSettings");
            Assert.That(overlay.Top, Is.EqualTo(LevelPopupKind.Settings));
            yield return Capture(view, "settings-save-failed");
            _audio.FailWrites = false;
            Click(view, "OverlaySaveSettings");
            Assert.That(overlay.Top, Is.EqualTo(LevelPopupKind.Pause));
            Assert.That(_audio.Writes, Is.GreaterThan(0));
            Click(view, "OverlayReturn");
            yield return Capture(view, "return-confirmation");
            Click(view, "OverlayCancel");
            Assert.That(overlay.Top, Is.EqualTo(LevelPopupKind.Pause));
            Click(view, "OverlayResume");
            Assert.That(view.Session.IsSuspended, Is.False);
            MainApproachDriver.Click(view, "Objectives");
            Assert.That(view.Session.IsSuspended, Is.True);
            yield return Capture(view, "objectives");
            Click(view, "OverlayCloseObjectives");
            Assert.That(view.Session.IsSuspended, Is.False);
            Assert.That(view.GetComponentsInChildren<Image>(true).Single(image =>
                image.name == "ModalBackdrop").gameObject.activeSelf, Is.False,
                "Closing the last modal removes its fullscreen input shield.");
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            Assert.That(Object.FindObjectOfType<MainGameRoot>().Audio.Playback.Preferences.Music.Volume,
                Is.EqualTo(.35f));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FirstGuideRestrictsInputsAdvancesOnRealActionsAndReplayUsesIndependentCompletion()
        {
            foreach (int seed in new[] { 0, 1 })
            {
                _seed = seed;
                yield return SceneManager.LoadSceneAsync("Main"); yield return null;
                Depart(); yield return null;
                var root = Object.FindObjectOfType<MainGameRoot>();
                var view = Object.FindObjectOfType<ApproachPanelView>();
                var guide = root.Presenter.Overlays.Guide;
                Assert.That(guide, Is.Not.Null);
                int version = view.Session.Version;
                var blocked = MainApproachDriver.Pointer(view, 45);
                view.Board.OnPointerDown(blocked);
                view.AdvanceContact(view.Session.MaxStepSeconds);
                view.Board.OnPointerUp(blocked);
                Assert.That(view.Session.Version, Is.EqualTo(version));
                Assert.That(guide.StepIndex, Is.Zero);
                yield return Capture(view, "first-guide-" + seed);
                var mask = view.GetComponentInChildren<GuideMaskGraphic>();
                Assert.That(mask.IsRaycastLocationValid(MainApproachDriver.Pointer(view, 45).position, null),
                    Is.True, "The mask intercepts input outside allowed targets.");
                var pauseRect = view.PauseTarget;
                Assert.That(mask.IsRaycastLocationValid(RectTransformUtility.WorldToScreenPoint(null,
                    pauseRect.TransformPoint(pauseRect.rect.center)), null), Is.False,
                    "The safety pause button remains accessible through the guide mask.");
                if (view.Session.Template.Mode == ApproachMode.Puzzle)
                {
                    MainApproachDriver.Drag(view, 43, 43);
                    Assert.That(guide.StepIndex, Is.EqualTo(1));
                    MainApproachDriver.Click(view, "Pause");
                    Click(view, "OverlayResume");
                }
                MainApproachDriver.Solve(view);
                Assert.That(guide.IsComplete, Is.True);
                yield return null;
                Assert.That(root.Guidance.HasCompleted(guide.MechanicId), Is.True);
                MainApproachDriver.Click(view, "Continue"); yield return null;
                var siege = Object.FindObjectOfType<SiegeTrialView>();
                var deployment = root.Presenter.Overlays.Guide;
                if (deployment != null)
                {
                    Assert.That(deployment.StepIndex, Is.Zero);
                    var submit = MainApproachDriver.Button(siege, "Submit");
                    submit.onClick.Invoke();
                    Assert.That(deployment.StepIndex, Is.Zero);
                    siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First()
                        .GetComponent<Button>().onClick.Invoke();
                    Assert.That(deployment.StepIndex, Is.EqualTo(1));
                    MainApproachDriver.Click(siege, "Top");
                    Assert.That(deployment.StepIndex, Is.EqualTo(2));
                    yield return Capture(siege, "deploy-guide");
                    MainApproachDriver.Click(siege, "Submit");
                    Assert.That(deployment.IsComplete, Is.True);
                    yield return null;
                    Assert.That(root.Guidance.HasCompleted(deployment.MechanicId), Is.True);
                    MainApproachDriver.Click(siege, "Objectives");
                    Assert.That(siege.GetComponentsInChildren<Button>().Single(item =>
                        item.name == "OverlayReplay").IsInteractable(), Is.False,
                        "A running battle offers instructions; replay starts only at a decision.");
                    Click(siege, "OverlayCloseObjectives");
                }
                MainApproachDriver.Click(siege, "Pause");
                yield return Capture(siege, "siege-pause-" + seed);
                Click(siege, "OverlayReturn"); Click(siege, "OverlayConfirm");
                Assert.That(Object.FindObjectOfType<StationPanelView>(), Is.Not.Null);
                yield return SceneManager.LoadSceneAsync("Main"); yield return null;
                Depart(); yield return null;
                root = Object.FindObjectOfType<MainGameRoot>();
                Assert.That(root.Presenter.Overlays.Guide, Is.Null);
                view = Object.FindObjectOfType<ApproachPanelView>();
                MainApproachDriver.Click(view, "Objectives");
                Click(view, "OverlayReplay"); Click(view, "OverlayConfirm");
                Assert.That(root.Presenter.Overlays.Guide.IsActive, Is.True);
            }
            LogAssert.NoUnexpectedReceived();
        }

        internal static IEnumerator Capture(Component owner, string name)
        {
            var canvas = owner.GetComponent<Canvas>();
            var camera = new GameObject("LevelUiCapture", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            camera.cullingMask = 1 << canvas.gameObject.layer;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            string folder = Path.GetFullPath("LevelUiEvidence"); Directory.CreateDirectory(folder);
            var sizes = new[] { new Vector2Int(390, 844), new Vector2Int(540, 960),
                new Vector2Int(768, 1024), new Vector2Int(432, 960), new Vector2Int(540, 1080),
                new Vector2Int(390, 844) };
            var safeViews = owner.GetComponentsInChildren<SafeAreaView>();
            var safe = (RectTransform)safeViews[0].transform;
            for (int index = 0; index < sizes.Length; index++)
            {
                var size = sizes[index];
                bool cutout = index == sizes.Length - 1;
                var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                yield return null; yield return null; Canvas.ForceUpdateCanvases();
                var originalAnchors = safeViews.Select(view =>
                    (((RectTransform)view.transform).anchorMin, ((RectTransform)view.transform).anchorMax))
                    .ToArray();
                if (cutout)
                {
                    foreach (var view in safeViews)
                    {
                        view.enabled = false;
                        var rect = (RectTransform)view.transform;
                        rect.anchorMin = new Vector2(0, .045f); rect.anchorMax = new Vector2(1, .91f);
                    }
                    Canvas.ForceUpdateCanvases();
                }
                var corners = new Vector3[4];
                foreach (var selectable in owner.GetComponentsInChildren<Selectable>()
                    .Where(item => item.IsInteractable()))
                {
                    ScrollIntoView(selectable);
                    ((RectTransform)selectable.transform).GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var local = safe.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(safe.rect.xMin - 2, safe.rect.xMax + 2), selectable.name);
                        Assert.That(local.y, Is.InRange(safe.rect.yMin - 2, safe.rect.yMax + 2), selectable.name);
                    }
                }
                AssertCompactOverlay(owner, safe);
                AssertDimLayer(owner, canvas, camera);
                camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
                var guideMask = owner.GetComponentInChildren<GuideMaskGraphic>();
                if (guideMask != null)
                {
                    var pause = owner is ApproachPanelView opening ? opening.PauseTarget :
                        ((SiegeTrialView)owner).Scene.PauseTarget;
                    Assert.That(guideMask.IsRaycastLocationValid(RectTransformUtility.WorldToScreenPoint(camera,
                        pause.TransformPoint(pause.rect.center)), camera), Is.False,
                        "Guide holes track the current safe area in the same render.");
                }
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply(); RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(folder, name + "-" + size.x + "x" + size.y +
                    (cutout ? "-cutout" : "") + ".png"),
                    image.EncodeToPNG());
                Object.Destroy(image); camera.targetTexture = null; target.Release(); Object.Destroy(target);
                for (int safeIndex = 0; safeIndex < safeViews.Length; safeIndex++)
                {
                    var rect = (RectTransform)safeViews[safeIndex].transform;
                    rect.anchorMin = originalAnchors[safeIndex].anchorMin;
                    rect.anchorMax = originalAnchors[safeIndex].anchorMax;
                    safeViews[safeIndex].enabled = true;
                }
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            Object.Destroy(camera.gameObject); yield return null;
        }

        internal static void ScrollIntoView(Component control)
        {
            foreach (var scroll in control.GetComponentsInParent<ScrollRect>())
                ScrollIntoView(control, scroll);
        }

        private static void ScrollIntoView(Component control, ScrollRect scroll)
        {
            if (!scroll.vertical) return;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            ((RectTransform)control.transform).GetWorldCorners(corners);
            float bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
            float top = scroll.viewport.InverseTransformPoint(corners[1]).y;
            float delta = top > scroll.viewport.rect.yMax ? scroll.viewport.rect.yMax - top :
                bottom < scroll.viewport.rect.yMin ? scroll.viewport.rect.yMin - bottom : 0;
            scroll.content.anchoredPosition += new Vector2(0, delta);
            Canvas.ForceUpdateCanvases();
        }

        private static void AssertDimLayer(Component owner, Canvas canvas, Camera camera)
        {
            var overlay = owner.GetComponentInChildren<LevelOverlayView>();
            var backdrop = overlay.GetComponentsInChildren<Graphic>().Where(graphic =>
                graphic is GuideMaskGraphic || graphic.name == "ModalBackdrop").ToArray();
            Assert.That(backdrop, Has.Length.EqualTo(1), "A modal and guide must never stack dim layers.");
            var dim = backdrop.Single();
            AssertCanvasCoverage(canvas, dim);
            Assert.That(dim.color, Is.EqualTo((Color)new Color32(0, 0, 0, 88)), "Shared neutral dim style.");
            Assert.That(dim.raycastTarget, Is.True);
            var mesh = dim.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null);
            var rect = dim.rectTransform.rect;
            Assert.That(mesh.bounds.min.x, Is.LessThanOrEqualTo(rect.xMin + 1));
            Assert.That(mesh.bounds.max.y, Is.GreaterThanOrEqualTo(rect.yMax - 1));
            foreach (var point in new[] { new Vector2(.01f, .01f), new Vector2(.99f, .01f),
                new Vector2(.01f, .99f), new Vector2(.99f, .99f) })
            {
                var root = (RectTransform)canvas.transform;
                var local = root.rect.min + Vector2.Scale(point, root.rect.size);
                var screen = RectTransformUtility.WorldToScreenPoint(camera, root.TransformPoint(local));
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(dim.rectTransform, screen,
                    camera), Is.True, "Unsafe screen corners must be inside the input shield.");
                Assert.That(dim.Raycast(screen, camera), Is.True, "Unsafe corners must intercept input.");
            }
        }

        private static Bounds VisibleBounds(RectTransform page, Selectable control)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(page, control.transform);
            foreach (var mask in control.GetComponentsInParent<RectMask2D>())
            {
                var maskRect = (RectTransform)mask.transform;
                var corners = new Vector3[4];
                maskRect.GetWorldCorners(corners);
                var clip = new Bounds(page.InverseTransformPoint(corners[0]), Vector3.zero);
                foreach (var corner in corners) clip.Encapsulate(page.InverseTransformPoint(corner));
                var min = Vector3.Max(bounds.min, clip.min);
                var max = Vector3.Min(bounds.max, clip.max);
                if (min.x >= max.x || min.y >= max.y)
                    return new Bounds(new Vector3(-100000, control.GetInstanceID(), 0), Vector3.zero);
                bounds.SetMinMax(min, max);
            }
            return bounds;
        }

        private static void AssertCompactOverlay(Component owner, RectTransform safe)
        {
            var overlay = owner.GetComponentInChildren<LevelOverlayView>(true);
            foreach (var label in overlay.GetComponentsInChildren<Text>())
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 3),
                    label.name + " must display its full text at the authored readable font size.");
            var pages = overlay.GetComponentsInChildren<RectTransform>().Where(rect =>
                rect.name == "PausePage" || rect.name == "SettingsPage" ||
                rect.name == "ObjectivesPage" || rect.name == "ConfirmationPage");
            foreach (var page in pages)
            {
                Assert.That(page.rect.height, Is.LessThan(safe.rect.height * .92f), page.name);
                if (page.name == "PausePage" || page.name == "ConfirmationPage")
                    Assert.That(page.rect.height, Is.LessThan(safe.rect.height * .55f),
                        "Short dialogs must not retain the old near-fullscreen blank card.");
                var controls = page.GetComponentsInChildren<Selectable>();
                for (int first = 0; first < controls.Length; first++)
                    for (int second = first + 1; second < controls.Length; second++)
                    {
                        var a = VisibleBounds(page, controls[first]);
                        var b = VisibleBounds(page, controls[second]);
                        Assert.That(a.Intersects(b), Is.False,
                            controls[first].name + " overlaps " + controls[second].name);
                    }
            }
        }
    }
}
#endif
