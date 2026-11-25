#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Loading;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.Rescue.UI;
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
    public sealed class LoadingPlayTests
    {
        private MainSceneStorageScope _storage;
        private HomelessPetHelper.Tests.Support.MemorySaveStorage[] _slots;
        private void ConfigureSlots(Scene scene, LoadSceneMode mode)
        {
            if (scene.path == LoadingSceneRoot.ScenePath)
                Object.FindObjectOfType<LoadingSceneRoot>().ConfigureSaveSlots(slot => _slots[slot]);
        }
        [SetUp] public void SetUp()
        {
            _storage = new MainSceneStorageScope();
            _slots = Enumerable.Range(0, 3).Select(_ => new MemorySaveStorage()).ToArray();
            SceneManager.sceneLoaded += ConfigureSlots;
        }
        [TearDown] public void TearDown()
        {
            Time.timeScale = 1f;
            SceneManager.sceneLoaded -= Preview;
            SceneManager.sceneLoaded -= ConfigureSlots;
            var host = Object.FindObjectOfType<LoadingSceneRoot>();
            if (host != null) host.enabled = false;
            _storage.Dispose();
        }

        private static void Preview(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != LoadingSceneRoot.ScenePath) return;
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LoadingSceneRoot>())
                .Single().ConfigurePreview();
        }

        private IEnumerator LoadPreview()
        {
            SceneManager.sceneLoaded += Preview;
            yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
            SceneManager.sceneLoaded -= Preview;
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartupStaysOnCoverUntilPlayerClicksEnter()
        {
            yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
            var view = Object.FindObjectOfType<LoadingView>();
            var entry = view.GetComponentsInChildren<Button>(true).Single(button => button.name == "EnterStation");
            yield return null;
            Assert.That(entry.gameObject.activeInHierarchy, Is.True);
            var host = Object.FindObjectOfType<LoadingSceneRoot>();
            var settings = new SerializedObject(host);
            settings.FindProperty("_timeoutSeconds").floatValue = 1f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LoadingSceneRoot.ScenePath));
            Assert.That(Object.FindObjectOfType<MainGameRoot>(), Is.Null);
            Assert.That(view.Progress, Is.Zero);
            Assert.That(view.GetComponentInChildren<LoadingArtworkMotion>(), Is.Null);
            var pets = view.GetComponentsInChildren<PetPortraitView>();
            Assert.That(pets.Length, Is.EqualTo(2));
            var frames = pets.Select(pet => pet.GetComponentInChildren<RawImage>()).ToArray();
            Assert.That(frames.Select(frame => frame.texture.name), Is.EquivalentTo(
                new[] { "T_CatPortraitMotion", "T_Station_ShibaMotion" }));
            var firstPoses = frames.Select(frame => frame.uvRect).ToArray();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(frames.Select((frame, index) => frame.uvRect != firstPoses[index]).All(changed => changed),
                Is.True);
            Assert.That(host.HasFailed, Is.False);
            Assert.That(entry.isActiveAndEnabled && entry.interactable, Is.True);
            Assert.That(entry.GetComponentInChildren<Text>().text, Is.EqualTo("进入救助站"));
            settings.FindProperty("_timeoutSeconds").floatValue = 60f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(ClickEntry(entry), Is.True);
            Assert.That(entry.gameObject.activeSelf, Is.False);
            Assert.That(ClickEntry(entry), Is.False);
            yield return WaitForMain();
            Assert.That(Object.FindObjectOfType<MainGameRoot>().Presenter, Is.Not.Null);
            Assert.That(Object.FindObjectOfType<LoadingSceneRoot>(), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        private static bool ClickEntry(Button entry) => ExecuteEvents.Execute(entry.gameObject,
            new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
            ExecuteEvents.pointerClickHandler);

        private static IEnumerator EnterAndWaitForLoading(LoadingView view)
        {
            yield return null;
            Assert.That(ClickEntry(view.GetComponentsInChildren<Button>(true).Single(button => button.name == "EnterStation")), Is.True);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (view.GetComponentInChildren<LoadingArtworkMotion>() == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(view.GetComponentInChildren<LoadingArtworkMotion>(), Is.Not.Null);
            yield return null; yield return null; yield return null;
        }

        private static IEnumerator WaitForMain()
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().path != LoadingSceneRoot.MainScenePath &&
                Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LoadingSceneRoot.MainScenePath));
        }

        private static IEnumerator LoadWithPreparation(ILoadingTask task, float timeout = 60f,
            string destination = null, bool enter = true)
        {
            void Configure(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != LoadingSceneRoot.ScenePath) return;
                var host = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<LoadingSceneRoot>()).Single();
                var data = new SerializedObject(host);
                data.FindProperty("_timeoutSeconds").floatValue = timeout;
                if (destination != null) data.FindProperty("_destination").stringValue = destination;
                data.ApplyModifiedPropertiesWithoutUndo();
                host.ConfigurePreparation(task);
            }
            SceneManager.sceneLoaded += Configure;
            try
            {
                yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                yield return null;
                if (enter) yield return EnterAndWaitForLoading(Object.FindObjectOfType<LoadingView>());
            }
            finally { SceneManager.sceneLoaded -= Configure; }
        }

        [UnityTest]
        public IEnumerator InjectedPreparationStartsOnClickAndKeepsArtworkUntilComplete()
        {
            var task = new ManualPreparation { Progress = .4f };
            yield return LoadWithPreparation(task, enter: false);
            var host = Object.FindObjectOfType<LoadingSceneRoot>();
            var view = Object.FindObjectOfType<LoadingView>();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(task.TickCount, Is.Zero);
            Assert.That(view.GetComponentInChildren<LoadingArtworkMotion>(), Is.Null);
            yield return EnterAndWaitForLoading(view);
            Assert.That(view.Progress, Is.EqualTo(.36f).Within(.001f));
            Assert.That(task.TickCount, Is.GreaterThan(0));
            var rejected = new ManualPreparation();
            Assert.Throws<System.InvalidOperationException>(() => host.ConfigurePreparation(rejected));
            Assert.Throws<System.InvalidOperationException>(() => host.ConfigurePreview());
            Assert.That(rejected.DisposeCount, Is.Zero, "Rejected injection remains caller-owned.");
            rejected.Dispose();
            task.Progress = .1f;
            yield return null; yield return null;
            Assert.That(view.Progress, Is.EqualTo(.36f).Within(.001f), "Progress must not move backwards.");
            task.Progress = 1f;
            yield return null; yield return null;
            Assert.That(view.Progress, Is.LessThan(.9f));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LoadingSceneRoot.ScenePath));
            var entry = view.GetComponentsInChildren<Button>(true).Single(button => button.name == "EnterStation");
            Assert.That(ClickEntry(entry), Is.False);
            task.IsComplete = true;
            yield return WaitForMain();
            Assert.That(task.DisposeCount, Is.EqualTo(1));
            Assert.That(Object.FindObjectOfType<MainGameRoot>().Presenter, Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PreparationFailureStaysOnLoadingAndReleasesOnce()
        {
            var task = new ManualPreparation { ThrowOnTick = true };
            LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("Injected preparation failure"));
            yield return LoadWithPreparation(task);
            var host = Object.FindObjectOfType<LoadingSceneRoot>();
            Assert.That(host.HasFailed, Is.True);
            Assert.That(task.DisposeCount, Is.EqualTo(1));
            Assert.That(Object.FindObjectOfType<LoadingView>().Progress, Is.LessThan(1f));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LoadingSceneRoot.ScenePath));
            host.enabled = false;
            Assert.That(task.DisposeCount, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DestinationIsValidatedAfterInjectedPreparationCompletes()
        {
            var task = new ManualPreparation { Progress = .4f };
            yield return LoadWithPreparation(task, destination: "NotAvailableUntilPreparationFinishes");
            var host = Object.FindObjectOfType<LoadingSceneRoot>();
            Assert.That(host.HasFailed, Is.False, "A real task may first load a bundle containing the destination.");
            Assert.That(task.TickCount, Is.GreaterThan(0));
            Assert.That(task.DisposeCount, Is.Zero);
            LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("Loading destination is not in Build Settings"));
            task.IsComplete = true;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!host.HasFailed && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(host.HasFailed, Is.True, "Completion must not bypass destination validation.");
            Assert.That(task.DisposeCount, Is.EqualTo(1));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LoadingSceneRoot.ScenePath));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PreparationTimeoutUsesUnscaledTimeAndDoesNotEnterMain()
        {
            var task = new ManualPreparation();
            Time.timeScale = 0f;
            LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("Loading task timed out"));
            yield return LoadWithPreparation(task, .05f);
            var host = Object.FindObjectOfType<LoadingSceneRoot>();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!host.HasFailed && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(host.HasFailed, Is.True);
            Assert.That(task.DisposeCount, Is.EqualTo(1));
            Assert.That(Object.FindObjectOfType<LoadingView>().Progress, Is.LessThan(1f));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LoadingSceneRoot.ScenePath));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InvalidTaskProgressIsFailureNotACompletedBar()
        {
            var task = new ManualPreparation { Progress = float.NaN };
            LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("Invalid Loading task progress"));
            yield return LoadWithPreparation(task);
            Assert.That(Object.FindObjectOfType<LoadingSceneRoot>().HasFailed, Is.True);
            Assert.That(task.DisposeCount, Is.EqualTo(1));
            Assert.That(Object.FindObjectOfType<LoadingView>().Progress, Is.Zero);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LoadingSceneRoot.ScenePath));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DisablingOwnerCancelsPreparationAndIgnoresLateCompletion()
        {
            var task = new ManualPreparation { Progress = .3f };
            yield return LoadWithPreparation(task);
            var host = Object.FindObjectOfType<LoadingSceneRoot>();
            var view = Object.FindObjectOfType<LoadingView>();
            host.enabled = false;
            int ticks = task.TickCount;
            float progress = view.Progress;
            Assert.That(task.DisposeCount, Is.EqualTo(1));
            task.Progress = 1f;
            task.IsComplete = true;
            host.enabled = true;
            yield return null; yield return null; yield return null;
            Assert.That(task.TickCount, Is.EqualTo(ticks));
            Assert.That(view.Progress, Is.EqualTo(progress));
            Assert.That(task.DisposeCount, Is.EqualTo(1));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LoadingSceneRoot.ScenePath));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CancellationInsideTaskTickCannotPublishAStaleProgressSample()
        {
            var task = new ManualPreparation { Progress = .2f };
            yield return LoadWithPreparation(task);
            var host = Object.FindObjectOfType<LoadingSceneRoot>();
            var view = Object.FindObjectOfType<LoadingView>();
            float before = view.Progress;
            task.Progress = .8f;
            task.OnTick = () => host.enabled = false;
            yield return null; yield return null;
            Assert.That(task.DisposeCount, Is.EqualTo(1));
            Assert.That(view.Progress, Is.EqualTo(before));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LoadingSceneRoot.ScenePath));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ReleasingSceneTaskUnblocksUnityActivationQueue()
        {
            yield return LoadPreview();
            var task = new UnitySceneLoadingTask(LoadingSceneRoot.MainScenePath);
            try
            {
                float deadline = Time.realtimeSinceStartup + 30f;
                while (!task.IsComplete && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(task.IsComplete, Is.True);
                Assert.That(task.HasActivated, Is.False);
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LoadingSceneRoot.ScenePath));
                task.Dispose();
                task.Dispose();
                while (!task.HasActivated && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(task.HasActivated, Is.True);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main"));
            }
            finally { task.Dispose(); }
            LogAssert.NoUnexpectedReceived();
        }

        private sealed class ManualPreparation : ILoadingTask
        {
            public float Progress { get; set; }
            public bool IsComplete { get; set; }
            public bool ThrowOnTick { get; set; }
            public System.Action OnTick { get; set; }
            public int TickCount { get; private set; }
            public int DisposeCount { get; private set; }
            public void Tick(float unscaledDeltaSeconds)
            {
                TickCount++;
                OnTick?.Invoke();
                if (ThrowOnTick) throw new System.InvalidOperationException("Injected preparation failure");
            }
            public void Dispose() => DisposeCount++;
        }

        [UnityTest]
        public IEnumerator MotionRunsWhileTimeScaleIsZeroAndResetsOnReuse()
        {
            yield return LoadPreview();
            var motion = Object.FindObjectOfType<LoadingArtworkMotion>();
            var data = new SerializedObject(motion);
            var orbit = (RectTransform)data.FindProperty("_orbit").objectReferenceValue;
            var animal = (RectTransform)data.FindProperty("_animals").GetArrayElementAtIndex(0).objectReferenceValue;
            var door = (RectTransform)data.FindProperty("_doorAnimals").GetArrayElementAtIndex(0).objectReferenceValue;
            motion.Sample(0);
            Quaternion before = orbit.localRotation, doorBefore = door.localRotation;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Quaternion.Angle(before, orbit.localRotation), Is.GreaterThan(.1f));
            motion.Sample(3);
            Assert.That(Quaternion.Angle(before, orbit.localRotation), Is.GreaterThan(40));
            Assert.That(Quaternion.Angle(doorBefore, door.localRotation), Is.GreaterThan(1));
            Assert.That(animal.localEulerAngles.z, Is.Not.EqualTo(0));
            motion.enabled = false;
            Assert.That(orbit.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(door.localRotation, Is.EqualTo(Quaternion.identity));
            motion.enabled = true;
            Assert.That(orbit.localRotation, Is.EqualTo(Quaternion.identity));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ProgressClampsAndFailureDoesNotReportSuccess()
        {
            yield return LoadPreview();
            var view = Object.FindObjectOfType<LoadingView>();
            view.SetProgress(-1); Assert.That(view.Progress, Is.Zero);
            view.SetProgress(.5f); Assert.That(view.Progress, Is.EqualTo(.5f));
            view.SetProgress(4); Assert.That(view.Progress, Is.EqualTo(1));
            view.ShowFailure();
            Assert.That(view.GetComponentsInChildren<Text>().Any(label => label.text == "准备失败，请重开"), Is.True);
            Assert.That(view.GetComponentsInChildren<Text>().Any(label => label.text == "100%"), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private static void ConfigureMissingDestination(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != LoadingSceneRoot.ScenePath) return;
            var host = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<LoadingSceneRoot>()).Single();
            var data = new SerializedObject(host);
            data.FindProperty("_destination").stringValue = "MissingLoadingDestination";
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        [UnityTest]
        public IEnumerator MissingDestinationStaysOnLoadingAndShowsFailure()
        {
            LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("Loading destination is not in Build Settings"));
            SceneManager.sceneLoaded += ConfigureMissingDestination;
            try
            {
                yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                yield return EnterAndWaitForLoading(Object.FindObjectOfType<LoadingView>());
                Assert.That(Object.FindObjectOfType<LoadingSceneRoot>().HasFailed, Is.True);
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LoadingSceneRoot.ScenePath));
                Assert.That(Object.FindObjectOfType<LoadingView>().GetComponentsInChildren<Text>()
                    .Any(label => label.text == "准备失败，请重开"), Is.True);
            }
            finally { SceneManager.sceneLoaded -= ConfigureMissingDestination; }
        }

        [UnityTest]
        public IEnumerator ArtworkAndProgressFitPortraitAndTabletWithAnimationEvidence()
        {
            yield return LoadPreview();
            var view = Object.FindObjectOfType<LoadingView>();
            var motion = Object.FindObjectOfType<LoadingArtworkMotion>();
            motion.enabled = false;
            var safe = view.GetComponentInChildren<SafeAreaView>();
            safe.enabled = false;
            var rect = (RectTransform)safe.transform;
            rect.anchorMin = new Vector2(.02f, .035f);
            rect.anchorMax = new Vector2(.98f, .94f);
            foreach (Vector2Int size in new[] { new Vector2Int(320, 569), new Vector2Int(360, 640),
                new Vector2Int(390, 780), new Vector2Int(390, 845), new Vector2Int(360, 800),
                new Vector2Int(768, 1024) })
                yield return Capture(view, motion, size.x, size.y);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator Capture(LoadingView view, LoadingArtworkMotion motion, int width, int height)
        {
            var canvas = view.GetComponent<Canvas>();
            var camera = new GameObject("LoadingCapture", typeof(Camera)).GetComponent<Camera>();
            var target = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture before = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                string folder = Path.GetFullPath("LoadingEvidence");
                Directory.CreateDirectory(folder);
                for (int frame = 0; frame < 3; frame++)
                {
                    motion.Sample(frame * 2f);
                    view.SetProgress(frame * .5f);
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    foreach (var image in view.GetComponentsInChildren<Image>())
                    {
                        if (image.name == "Paper") continue;
                        var corners = new Vector3[4];
                        image.rectTransform.GetWorldCorners(corners);
                        foreach (Vector3 corner in corners)
                        {
                            Vector3 point = camera.WorldToViewportPoint(corner);
                            Assert.That(point.x, Is.InRange(0f, 1f), image.name + " x");
                            Assert.That(point.y, Is.InRange(0f, 1f), image.name + " y");
                        }
                    }
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, $"loading-{width}x{height}-{frame}.png"),
                        texture.EncodeToPNG());
                }
                if (width == 360 && height == 640)
                {
                    view.SetProgress(.5f);
                    string animation = Path.Combine(folder, "animation");
                    Directory.CreateDirectory(animation);
                    for (int frame = 0; frame < 48; frame++)
                    {
                        motion.Sample(frame / 12f);
                        Canvas.ForceUpdateCanvases();
                        camera.Render();
                        RenderTexture.active = target;
                        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                        File.WriteAllBytes(Path.Combine(animation, $"frame-{frame:D3}.png"), texture.EncodeToPNG());
                    }
                }
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
                RenderTexture.active = before; camera.targetTexture = null; target.Release();
                Object.Destroy(texture); Object.Destroy(target); Object.Destroy(camera.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator CoverEntryFitsTheInsetAndReceivesRaycasts()
        {
            yield return LoadPreview();
            var view = Object.FindObjectOfType<LoadingView>();
            view.ShowCover();
            var canvas = view.GetComponent<Canvas>();
            var camera = new GameObject("TitleCapture", typeof(Camera)).GetComponent<Camera>();
            var safe = view.GetComponentInChildren<SafeAreaView>();
            safe.enabled = false;
            var inset = (RectTransform)safe.transform;
            inset.anchorMin = new Vector2(.04f, .05f);
            inset.anchorMax = new Vector2(.96f, .93f);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            var entry = view.GetComponentsInChildren<Button>().Single(button => button.name == "EnterStation");
            try
            {
                foreach (var size in new[] { new Vector2Int(320, 569), new Vector2Int(390, 845),
                    new Vector2Int(360, 840), new Vector2Int(768, 1024) })
                    yield return CaptureCover(canvas, camera, entry, size);
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                Object.Destroy(camera.gameObject);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator CaptureCover(Canvas canvas, Camera camera, Button entry, Vector2Int size)
        {
            var target = new RenderTexture(size.x, size.y, 24);
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                yield return null;
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4];
                var buttonRect = (RectTransform)entry.transform;
                buttonRect.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = camera.WorldToViewportPoint(corner);
                    Assert.That(point.x, Is.InRange(.039f, .961f));
                    Assert.That(point.y, Is.InRange(.049f, .931f));
                }
                var pointer = new PointerEventData(EventSystem.current)
                    { position = camera.WorldToScreenPoint(buttonRect.position) };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
                Assert.That(hits.Any(hit => hit.gameObject == entry.gameObject), Is.True);
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("LoadingEvidence");
                File.WriteAllBytes($"LoadingEvidence/title-{size.x}x{size.y}.png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(texture);
            }
        }
    }
}
#endif
