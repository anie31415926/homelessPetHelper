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
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.Audio.UI;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Game;
using HomelessPetHelper.Tests.Support;
using HomelessPetHelper.Modules.Rescue.UI;

namespace HomelessPetHelper.Tests.StationHabitat.PlayMode
{
    public sealed class StationSceneUiTests
    {
        private StationPanelView _view;
        private Camera _fixtureCamera;
        private RenderTexture _fixtureTarget;
        [TearDown]
        public void TearDown()
        {
            if (_fixtureCamera != null) UnityEngine.Object.DestroyImmediate(_fixtureCamera.gameObject);
            if (_fixtureTarget != null)
            { _fixtureTarget.Release(); UnityEngine.Object.DestroyImmediate(_fixtureTarget); }
        }
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
            _fixtureCamera = new GameObject("Portrait station fixture", typeof(Camera)).GetComponent<Camera>();
            _fixtureTarget = new RenderTexture(390, 844, 24);
            _fixtureCamera.targetTexture = _fixtureTarget;
            var canvas = _view.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _fixtureCamera;
            canvas.planeDistance = 10;
            yield return null; Canvas.ForceUpdateCanvases(); yield return null;
        }

        private static void AssertFixedToast(ToastView toast)
        {
            var rect = (RectTransform)toast.transform;
            var safe = (RectTransform)rect.parent;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector3 local = safe.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(safe.rect.xMin - 1, safe.rect.xMax + 1));
                Assert.That(local.y, Is.InRange(safe.rect.yMin - 1, safe.rect.yMax + 1));
            }
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(.5f, .70f)));
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
            Assert.That(_view.Scene.Room, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            foreach (var cell in cells) RayClick(cell.SelectButton, false);
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
            yield return null;
            Canvas.ForceUpdateCanvases();
            var toast = _view.GetComponentInChildren<ToastView>();
            AssertFixedToast(toast);
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
            Assert.That(_view.Scene.gameObject.activeInHierarchy, Is.True, "Full-room background remains behind care.");
            Assert.That(_view.Scene.Cells[0].gameObject.activeInHierarchy, Is.False);
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
        public IEnumerator FurnitureDepthFollowsFeetWithoutReorderingAStationaryScene()
        {
            yield return Load();
            var room = _view.Scene.Room;
            var bed = room.GetComponentsInChildren<Image>().Single(image => image.name == "Furniture_bed");
            var area = room.CreateArea();
            for (int frame = 0; frame < 40; frame++)
            {
                foreach (var cell in _view.Scene.Cells.Where(cell => cell.gameObject.activeSelf && !cell.Observing))
                    for (int tick = 0; tick < 100; tick++) cell.Motion.Tick(.05);
                yield return null;
                yield return null;
                foreach (var cell in _view.Scene.Cells.Where(cell => cell.gameObject.activeSelf))
                {
                    Assert.That(cell.transform.GetSiblingIndex() < bed.transform.GetSiblingIndex(),
                        Is.EqualTo(cell.GroundY >= .255f));
                    if (!cell.Observing) Assert.That(area.Contains(cell.Motion.Position), Is.True);
                }
            }
            _view.Scene.SetSuspended(true);
            var order = room.Content.Cast<Transform>().Select(child => child.GetInstanceID()).ToArray();
            yield return null;
            yield return null;
            Assert.That(room.Content.Cast<Transform>().Select(child => child.GetInstanceID()), Is.EqualTo(order));
        }

        [UnityTest]
        public IEnumerator MenuUsesOneFullscreenMaskAndDoesNotLeakInputToAnimals()
        {
            yield return Load();
            RayClick(Button("StationMenu"));
            yield return null;
            Assert.That(_view.GetComponent<StationSceneChrome>().MenuIsOpen, Is.True);
            var mask = Button("DismissAnimal");
            Assert.That(mask.gameObject.activeInHierarchy, Is.True);
            Assert.That(mask.GetComponent<Image>().color, Is.EqualTo((Color)new Color32(0, 0, 0, 88)));
            var rect = (RectTransform)mask.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
            var animal = (RectTransform)_view.Scene.Cells[0].SelectButton.transform;
            var data = new PointerEventData(EventSystem.current) { position =
                RectTransformUtility.WorldToScreenPoint(_view.GetComponent<Canvas>().worldCamera,
                    animal.TransformPoint(animal.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits[0].gameObject.GetComponentInParent<StationResidentView>(), Is.Null);
            yield return Capture("menu");
            RayClick(Button("CloseStationMenu"));
            yield return null;
            Assert.That(mask.gameObject.activeSelf, Is.False);
            Assert.That(Detail.activeSelf, Is.False);
            RayClick(_view.Scene.Cells[0].SelectButton);
            yield return null;
            RayClick(Button("Back"));
            yield return null;
            Assert.That(mask.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator SharedPressFeedbackKeepsHitboxStableAndResetsOnDisableAndPause()
        {
            yield return Load();
            var button = Button("StationMenu");
            var feedback = button.GetComponent<UiMotionFeedback>();
            var visual = (RectTransform)new SerializedObject(feedback).FindProperty("_visual").objectReferenceValue;
            var rect = (RectTransform)button.transform;
            var size = rect.rect.size; var anchor = rect.anchoredPosition;
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 0 };
            feedback.OnPointerDown(pointer);
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(visual.localScale.x, Is.LessThan(.99f));
            Assert.That(rect.rect.size, Is.EqualTo(size)); Assert.That(rect.anchoredPosition, Is.EqualTo(anchor));
            feedback.OnPointerUp(pointer);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(visual.localScale.x, Is.EqualTo(1).Within(.001f));
            button.gameObject.SetActive(false); button.gameObject.SetActive(true);
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            feedback.SendMessage("OnApplicationFocus", false);
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator LongDetailScrollRetainsFixedCloseAndFullSharedMotionAtBothEdges()
        {
            yield return Load();
            _view.Scene.Cells[0].SelectButton.onClick.Invoke();
            var condition = (Text)new SerializedObject(_view).FindProperty("_condition").objectReferenceValue;
            condition.text = string.Join("\n", Enumerable.Repeat(condition.text, 24));
            yield return null; Canvas.ForceUpdateCanvases(); yield return null;
            var scroll = Detail.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll.vertical, Is.True);
            Assert.That(condition.rectTransform.rect.height, Is.GreaterThanOrEqualTo(condition.preferredHeight - 1));
            var closeRect = (RectTransform)Button("Back").transform;
            var closePosition = closeRect.position;
            int checkedControls = 0;
            foreach (float edge in new[] { 1f, 0f })
            {
                scroll.verticalNormalizedPosition = edge;
                yield return null; Canvas.ForceUpdateCanvases();
                Assert.That(closeRect.position, Is.EqualTo(closePosition));
                foreach (var button in scroll.content.GetComponentsInChildren<Button>())
                {
                    var corners = new Vector3[4];
                    ((RectTransform)button.transform).GetWorldCorners(corners);
                    var bottom = scroll.viewport.InverseTransformPoint(corners[0]);
                    var top = scroll.viewport.InverseTransformPoint(corners[2]);
                    if (bottom.y < scroll.viewport.rect.yMin || top.y > scroll.viewport.rect.yMax) continue;
                    var motion = button.GetComponent<UiMotionFeedback>();
                    motion.OnPointerEnter(new PointerEventData(EventSystem.current) { pointerId = -1 });
                    yield return new WaitForSecondsRealtime(.18f);
                    var visual = (RectTransform)new SerializedObject(motion)
                        .FindProperty("_visual").objectReferenceValue;
                    Assert.That(visual.localScale.x, Is.GreaterThan(1));
                    visual.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = scroll.viewport.InverseTransformPoint(corner);
                        Assert.That(point.x, Is.InRange(scroll.viewport.rect.xMin, scroll.viewport.rect.xMax));
                        Assert.That(point.y, Is.InRange(scroll.viewport.rect.yMin, scroll.viewport.rect.yMax));
                    }
                    motion.OnPointerExit(new PointerEventData(EventSystem.current) { pointerId = -1 });
                    checkedControls++;
                }
            }
            Assert.That(checkedControls, Is.GreaterThan(0));
            RayClick(Button("Back"));
            Assert.That(Detail.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator MainNavigationUsesExistingOpenAndTapClipsWithoutExtraAudioSources()
        {
            using (var storage = new MainSceneStorageScope())
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return null;
                _view = UnityEngine.Object.FindObjectOfType<StationPanelView>();
                var host = UnityEngine.Object.FindObjectOfType<AudioHost>();
                host.SendMessage("OnApplicationFocus", true);
                var main = UnityEngine.Object.FindObjectOfType<MainGameRoot>();
                main.Audio.Playback.StopEffects();
                Button("StationMenu").OnPointerClick(new PointerEventData(EventSystem.current));
                Assert.That(UnityEngine.Object.FindObjectsOfType<AudioSource>().Count(), Is.EqualTo(9));
                Assert.That(UnityEngine.Object.FindObjectsOfType<AudioSource>().Count(source =>
                    source.clip != null && source.clip.name == "SFX_UI_Open"), Is.EqualTo(1));
                yield return new WaitForSecondsRealtime(.2f);
                main.Audio.Playback.StopEffects();
                Button("CloseStationMenu").OnPointerClick(new PointerEventData(EventSystem.current));
                Assert.That(UnityEngine.Object.FindObjectsOfType<AudioSource>().Any(source =>
                    source.clip != null && source.clip.name == "SFX_UI_Tap"), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator HandbookTabsKeepEntriesInsideOnePaper()
        {
            yield return Load();
            RayClick(Button("StationMenu"));
            yield return null; Canvas.ForceUpdateCanvases();
            RectTransform Page(string name) => _view.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == name);
            Assert.That(Page("PageStation").gameObject.activeSelf, Is.True);
            Assert.That(Page("PageEconomy").gameObject.activeSelf, Is.False);
            Assert.That(Button("History").gameObject.activeInHierarchy, Is.True);
            RayClick(Button("TabEconomy"));
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(Page("PageEconomy").gameObject.activeSelf, Is.True);
            Assert.That(Page("PageStation").gameObject.activeSelf, Is.False);
            Assert.That(Visible("EconomyOffer", "EconomyEmpty"), Is.True);
            yield return Capture("handbook-economy");
            RayClick(Button("TabMemorial"));
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(Visible("MemorialCard", "MemorialEmpty"), Is.True);
            yield return Capture("handbook-memorial");
            RayClick(Button("TabSettings"));
            Assert.That(Button("StationSettings").gameObject.activeInHierarchy, Is.True);
            yield return Capture("handbook-settings");
            RayClick(Button("TabStation"));
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(Page("PageStation").gameObject.activeSelf, Is.True);
            Assert.That(_view.GetComponent<StationSceneChrome>().MenuIsOpen, Is.True);
            RayClick(Button("CloseStationMenu"));
        }

        [UnityTest]
        public IEnumerator HandbookPauseRowSwapsPauseAndContinue()
        {
            yield return Load();
            RayClick(Button("StationMenu"));
            yield return null; Canvas.ForceUpdateCanvases();
            Toggle pause = _view.GetComponentsInChildren<Toggle>(true)
                .First(toggle => toggle.name == "Pause" && toggle.gameObject.activeInHierarchy);
            Text label = pause.GetComponentInChildren<Text>(true);
            GameObject pauseIcon = _view.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "IconPause").gameObject;
            GameObject continueIcon = _view.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "IconPlay").gameObject;
            Assert.That(label.text, Is.EqualTo("暂停"));
            Assert.That(pauseIcon.activeSelf, Is.True);
            Assert.That(continueIcon.activeSelf, Is.False);
            pause.isOn = true;
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(label.text, Is.EqualTo("继续"));
            Assert.That(pauseIcon.activeSelf, Is.False);
            Assert.That(continueIcon.activeSelf, Is.True);
            yield return Capture("menu-paused");
            pause.isOn = false;
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(label.text, Is.EqualTo("暂停"));
            Assert.That(pauseIcon.activeSelf, Is.True);
            Assert.That(continueIcon.activeSelf, Is.False);
            RayClick(Button("CloseStationMenu"));
        }

        [UnityTest]
        public IEnumerator HandbookPopulatedPagesFillRowsAndGrid()
        {
            yield return Load();
            var sheet = _view.GetComponentInChildren<StationHandbookSheet>(true);
            var art = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/UI/T_UI_LevelControls.png")
                .OfType<Sprite>().ToArray();
            sheet.SetOffers("援助金 120", new[]
            {
                new HandbookOffer("扩建安置区", "需要 80 援助金", null, false),
                new HandbookOffer("清洁台升级", "已升级", null, true),
                new HandbookOffer("木夹子", "已加入外出编队", null, true)
            }, _ => { });
            sheet.SetMemories(new[]
            {
                new HandbookMemory("栗子", art.First(sprite => sprite.name == "Paw")),
                new HandbookMemory("小麦", art.First(sprite => sprite.name == "Home")),
                new HandbookMemory("松果", art.First(sprite => sprite.name == "Objectives")),
                new HandbookMemory("团团", art.First(sprite => sprite.name == "Paw"))
            }, _ => { });
            RayClick(Button("StationMenu"));
            yield return null; Canvas.ForceUpdateCanvases();
            RayClick(Button("TabEconomy"));
            yield return null; Canvas.ForceUpdateCanvases();
            yield return Capture("handbook-economy-filled");
            RayClick(Button("TabMemorial"));
            yield return null; Canvas.ForceUpdateCanvases();
            yield return Capture("handbook-memorial-filled");
            RayClick(Button("CloseStationMenu"));
        }

        [UnityTest]
        public IEnumerator HomeSheetAndCareRemainInsideFourPortraitViewports()
        {
            yield return Load();
            _view.GetComponentInChildren<ToastView>().Clear();
            yield return Capture("home");
            _view.SetFeedback("已从上一次有效记录恢复进度。");
            yield return Capture("toast");
            _view.Scene.Cells[2].SelectButton.onClick.Invoke();
            yield return Capture("observation-detail");
            Button("Back").onClick.Invoke();
            _view.Scene.Cells[0].SelectButton.onClick.Invoke();
            yield return Capture("healthy-detail");
            Button("Clean").onClick.Invoke();
            _view.SetFeedback("护理中，请继续轻轻清洁。");
            yield return Capture("care");
            var animal = (Image)new SerializedObject(_view.Surface).FindProperty("_animal").objectReferenceValue;
            var summaryArt = animal.sprite;
            Assert.That(summaryArt, Is.Not.Null);
            Button("Finish").onClick.Invoke(); Button("Back").onClick.Invoke();
            Button("StationMenu").onClick.Invoke(); Button("History").onClick.Invoke();
            yield return Capture("history");
            _view.ShowSummary("救助猫咪 1 已平安回站，等待检查与护理。", summaryArt);
            _view.ShowPage(true, false);
            yield return Capture("arrival-summary");
        }

        [UnityTest]
        public IEnumerator FullBackgroundStaysFullWhileControlsRespectSimulatedSafeArea()
        {
            yield return Load();
            foreach (var safe in _view.GetComponentsInChildren<SafeAreaView>(true))
            {
                safe.enabled = false;
                var rect = (RectTransform)safe.transform;
                rect.anchorMin = new Vector2(.04f, .04f); rect.anchorMax = new Vector2(.96f, .92f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            _view.GetComponentInChildren<ToastView>().Clear();
            yield return Capture("safe-home");
            RayClick(Button("StationMenu"));
            yield return Capture("safe-menu");
            RayClick(Button("CloseStationMenu"));
        }

        private Button Button(string name) => _view.GetComponentsInChildren<Button>(true)
            .Single(button => button.name == name);

        private bool Visible(params string[] names) => _view.GetComponentsInChildren<RectTransform>(true)
            .Any(rect => names.Any(name => rect.name.StartsWith(name)) && rect.gameObject.activeInHierarchy);

        private static void RayClick(Button button, bool execute = true)
        {
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,
                rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            var target = hits[0].gameObject.GetComponentInParent<Button>();
            Assert.That(target, Is.SameAs(button));
            if (execute) ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private IEnumerator Capture(string state)
        {
            var canvas = _view.GetComponent<Canvas>();
            var oldCamera = canvas.worldCamera; var oldMode = canvas.renderMode;
            var camera = new GameObject("StationSceneCapture", typeof(Camera)).GetComponent<Camera>();
            camera.cullingMask = 1 << canvas.gameObject.layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            string folder = Path.GetFullPath("StationSceneEvidence");
            Directory.CreateDirectory(folder);
            foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(360, 720),
                new Vector2Int(375, 667), new Vector2Int(390, 844),
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
                if (state.EndsWith("home", StringComparison.Ordinal))
                {
                    var background = _view.Scene.Room.GetComponentsInChildren<Image>()
                        .Single(item => item.name == "RoomBackground");
                    var roomCorners = new Vector3[4]; background.rectTransform.GetWorldCorners(roomCorners);
                    Vector3 low = camera.WorldToViewportPoint(roomCorners[0]);
                    Vector3 high = camera.WorldToViewportPoint(roomCorners[2]);
                    Assert.That(low.x, Is.LessThanOrEqualTo(.001f));
                    Assert.That(low.y, Is.LessThanOrEqualTo(.001f));
                    Assert.That(high.x, Is.GreaterThanOrEqualTo(.999f));
                    Assert.That(high.y, Is.GreaterThanOrEqualTo(.999f));
                    Assert.That(_view.GetComponentsInChildren<Button>().Count(), Is.EqualTo(5),
                        "Only 3 resident hit targets, menu and outing remain on the home screen.");
                    foreach (var control in new[] { Button("StationMenu"), Button("Restart") })
                    {
                        var box = ((RectTransform)control.transform).rect;
                        Assert.That(box.height, Is.EqualTo(144).Within(.1f));
                        Assert.That(box.width, Is.GreaterThanOrEqualTo(144));
                    }
                }
                if (state == "toast")
                {
                    var notice = _view.GetComponentInChildren<ToastView>();
                    var label = notice.GetComponentInChildren<Text>();
                    Assert.That(label.text, Is.Not.Empty);
                    Assert.That(label.cachedTextGenerator.vertexCount, Is.GreaterThan(4),
                        "Toast must render glyphs, not just a blank paper.");
                    Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1));
                }
                if (state == "arrival-summary")
                {
                    var animal = (Image)new SerializedObject(_view)
                        .FindProperty("_summaryAnimal").objectReferenceValue;
                    Assert.That(animal.sprite, Is.Not.Null);
                    Assert.That(animal.rectTransform.rect.height, Is.GreaterThanOrEqualTo(300));
                    var scroll = animal.GetComponentInParent<ScrollRect>();
                    var points = new Vector3[4]; animal.rectTransform.GetWorldCorners(points);
                    foreach (var point in points)
                    {
                        var local = scroll.viewport.InverseTransformPoint(point);
                        Assert.That(local.y, Is.InRange(scroll.viewport.rect.yMin - 1, scroll.viewport.rect.yMax + 1),
                            size + " arrival animal must remain fully visible above the fixed enter action.");
                    }
                }
                if (state == "care")
                {
                    var scroll = Detail.GetComponentInChildren<ScrollRect>();
                    var points = new Vector3[4];
                    ((RectTransform)_view.Surface.transform).GetWorldCorners(points);
                    foreach (var point in points)
                    {
                        var local = scroll.viewport.InverseTransformPoint(point);
                        Assert.That(local.y, Is.InRange(scroll.viewport.rect.yMin - 1, scroll.viewport.rect.yMax + 1),
                            size + " full care surface must fit; never clip the paws behind the footer.");
                    }
                    var message = Detail.GetComponentsInChildren<Text>()
                        .Single(text => text.name == "CareFeedback");
                    Assert.That(message.text, Is.EqualTo("护理中，请继续轻轻清洁。"));
                    Assert.That(message.rectTransform.rect.height,
                        Is.GreaterThanOrEqualTo(message.preferredHeight - 1));
                }
                if (state.EndsWith("detail", StringComparison.Ordinal))
                    AssertFixedToast(_view.GetComponentInChildren<ToastView>());
                if (state == "home")
                    foreach (var cell in _view.Scene.Cells.Where(cell => cell.gameObject.activeSelf))
                        Assert.That(((RectTransform)cell.SelectButton.transform).rect.width,
                            Is.GreaterThanOrEqualTo(144));
                UnityEngine.Object.Destroy(texture);
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            UnityEngine.Object.Destroy(camera.gameObject);
        }

        private void AssertLayout(Camera camera)
        {
            var corners = new Vector3[4];
            foreach (var button in _view.GetComponentsInChildren<Selectable>())
            {
                var hit = ((RectTransform)button.transform).rect;
                Assert.That(hit.width, Is.GreaterThanOrEqualTo(144 - .01f), button.name);
                Assert.That(hit.height, Is.GreaterThanOrEqualTo(144 - .01f), button.name);
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
                    var safe = button.GetComponentInParent<SafeAreaView>();
                    if (safe == null) continue;
                    var bounds = (RectTransform)safe.transform;
                    var local = bounds.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(bounds.rect.xMin - 1, bounds.rect.xMax + 1), button.name);
                    Assert.That(local.y, Is.InRange(bounds.rect.yMin - 1, bounds.rect.yMax + 1), button.name);
                }
            }
        }
    }
}
#endif
