#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class StrategyUiPlayTests
    {
        private MainSceneStorageScope _scope;
        private MemorySaveStorage _play;
        private readonly List<Object> _temporary = new List<Object>();
        private string _point = "parking";
        private int _seed;
        [SetUp] public void SetUp()
        {
            _play = new MemorySaveStorage();
            _scope = new MainSceneStorageScope(createPlayStorage: () => _play);
            SceneManager.sceneLoaded += Configure;
        }
        [TearDown] public void TearDown()
        {
            SceneManager.sceneLoaded -= Configure; _scope.Dispose();
            foreach (var temporary in _temporary) Object.DestroyImmediate(temporary);
            _temporary.Clear();
        }
        private void Configure(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Main") return;
            var root = scene.GetRootGameObjects().SelectMany(item =>
                item.GetComponentsInChildren<MainGameRoot>(true)).Single();
            root.ConfigureOutingSeedSource(() => _seed);
            var binding = new SerializedObject(root).FindProperty("_content");
            // Exercise the same Editor-only composition used by the six actual sample menu entries.
            var preview = Type.GetType("HomelessPetHelper.Editor.LevelUi.FieldSamplePreview, HomelessPetHelper.Editor");
            var content = (GameContent)preview.GetMethod("CreateContent").Invoke(null,
                new object[] { binding.objectReferenceValue, _point });
            _temporary.Add(content);
            _temporary.Add(new SerializedObject(content).FindProperty("_mapConfig").objectReferenceValue);
            binding.objectReferenceValue = content; binding.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
        private IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            MainApproachDriver.Click(Object.FindObjectOfType<CityMapView>(), "Primary");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SixSamplesKeepTheirFinishedSceneAndDragToActualThreePathsWithoutSpending()
        {
            foreach (string point in new[] { "parking", "yard", "porch" })
                foreach (int seed in new[] { 0, 1 })
                {
                    _point = point; _seed = seed; yield return Open();
                    var opening = Object.FindObjectOfType<ApproachPanelView>();
                    Assert.That(opening.FieldScene.Layout.PointId, Is.EqualTo(point));
                    yield return Capture(opening, point + "-opening-" + seed);
                    MainApproachDriver.Solve(opening);
                    var snapshot = opening.FieldScene.Snapshot;
                    MainApproachDriver.Click(opening, "Continue"); yield return null;
                    var siege = Object.FindObjectOfType<SiegeTrialView>();
                    var scene = siege.GetComponent<OutingFieldView>();
                    Assert.That(scene.Snapshot.Boxes, Is.EqualTo(snapshot.Boxes));
                    Assert.That(scene.Snapshot.Bridge, Is.EqualTo(snapshot.Bridge));
                    Assert.That(scene.Snapshot.Remaining, Is.EqualTo(snapshot.Remaining));
                    var root = Object.FindObjectOfType<MainGameRoot>();
                    var battle = ((SiegeRescueActivity)root.Presenter.Session.Activity).Battle;
                    var before = battle.Snapshot;
                    var inputCamera = PortraitInput(siege.GetComponent<Canvas>(), out var inputTarget);
                    _temporary.Add(inputCamera.gameObject); _temporary.Add(inputTarget);
                    yield return null; yield return null; Canvas.ForceUpdateCanvases();
                    // These are real scene segments well beyond the removed paper route buttons.
                    for (int lane = 0; lane < 3; lane++)
                    {
                        var card = siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First();
                        var pointer = Pointer(card);
                        card.OnBeginDrag(pointer);
                        pointer.position = Screen(siege.Scene.Field, scene.Layout, 8 + lane * 2);
                        Assert.That(siege.Scene.Field.PickLane(pointer.position, inputCamera),
                            Is.EqualTo(battle.Catalog.Lanes[lane].Id), "geometry before drag");
                        card.OnDrag(pointer); card.OnEndDrag(pointer);
                        Assert.That(siege.Scene.Plan.DeployLane, Is.EqualTo(battle.Catalog.Lanes[lane].Id));
                        Assert.That(battle.Snapshot.Energy, Is.EqualTo(before.Energy));
                        Assert.That(battle.Snapshot.Hand.Count, Is.EqualTo(before.Hand.Count));
                        Assert.That(battle.Snapshot.Units, Is.Empty);
                        MainApproachDriver.Click(siege, "Clear");
                    }
                    var selected = siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First();
                    var drag = Pointer(selected); selected.OnBeginDrag(drag);
                    drag.position = Screen(siege.Scene.Field, scene.Layout, 52);
                    selected.OnDrag(drag); selected.OnEndDrag(drag);
                    Assert.That(siege.Scene.Plan.HasAction, Is.False, "Shared paths reject ambiguous deployment.");
                    selected.OnBeginDrag(drag); drag.position = new Vector2(-100, -100);
                    selected.OnDrag(drag); selected.OnEndDrag(drag);
                    Assert.That(siege.Scene.Plan.HasAction, Is.False);
                    var hint = (Text)new SerializedObject(siege.Scene).FindProperty("_hint").objectReferenceValue;
                    string rejected = hint.text;
                    selected.OnBeginDrag(drag); drag.position = Screen(siege.Scene.Field, scene.Layout, 10);
                    selected.OnDrag(drag); selected.OnEndDrag(drag);
                    Assert.That(hint.text, Is.Not.EqualTo(rejected), "Valid drops clear the previous rejection.");
                    yield return Capture(siege, point + "-deploy-" + seed);
                    MainApproachDriver.Click(siege, "Submit");
                    Assert.That(battle.Snapshot.Units.Count, Is.EqualTo(1));
                    Assert.That(battle.Snapshot.Units[0].LaneId, Is.EqualTo("mid"));
                    MainApproachDriver.Click(siege, "Pause");
                    MainApproachDriver.Click(siege, "Retry");
                    LogAssert.NoUnexpectedReceived();
                }
        }

        [UnityTest]
        public IEnumerator SettingsCancelResetSaveReloadAndContinuousBackgroundPauseUseActualControls()
        {
            yield return Open();
            var root = Object.FindObjectOfType<MainGameRoot>();
            var opening = Object.FindObjectOfType<ApproachPanelView>();
            MainApproachDriver.Click(opening, "Pause"); MainApproachDriver.Click(opening, "Settings");
            var toggle = opening.GetComponentsInChildren<Toggle>().Single(item => item.name == "PauseAtDecision");
            toggle.isOn = false;
            MainApproachDriver.Click(opening, "CancelSettings");
            Assert.That(root.PlaySettings.PauseAtDecision, Is.True);
            Assert.That(_play.Writes, Is.Zero);
            MainApproachDriver.Click(opening, "Settings"); toggle.isOn = false;
            MainApproachDriver.Click(opening, "ResetAudio"); MainApproachDriver.Click(opening, "Confirm");
            Assert.That(toggle.isOn, Is.True);
            toggle.isOn = false; _play.FailWrites = true;
            MainApproachDriver.Click(opening, "SaveSettings");
            Assert.That(root.PlaySettings.PauseAtDecision, Is.True);
            _play.FailWrites = false; MainApproachDriver.Click(opening, "SaveSettings");
            Assert.That(root.PlaySettings.PauseAtDecision, Is.False);
            MainApproachDriver.Click(opening, "Resume");
            MainApproachDriver.Solve(opening); MainApproachDriver.Click(opening, "Continue"); yield return null;
            var siege = Object.FindObjectOfType<SiegeTrialView>();
            var battle = ((SiegeRescueActivity)root.Presenter.Session.Activity).Battle;
            Assert.That(battle.PauseAtDecision, Is.False);
            Assert.That(siege.IsBattleRunning, Is.True, "Animation follows continuous Decision time.");
            int before = battle.ElapsedMs; battle.Advance(1000);
            Assert.That(battle.ElapsedMs, Is.GreaterThan(before));
            MainApproachDriver.Click(siege, "Pause"); before = battle.ElapsedMs;
            battle.Advance(1000); Assert.That(battle.ElapsedMs, Is.EqualTo(before));
            MainApproachDriver.Click(siege, "Settings");
            yield return LevelUiPlayTests.Capture(siege, "strategy-settings");
            MainApproachDriver.Click(siege, "CancelSettings"); MainApproachDriver.Click(siege, "Resume");
            siege.SendMessage("OnApplicationFocus", false); siege.SendMessage("OnApplicationFocus", true);
            before = battle.ElapsedMs; battle.Advance(1000);
            Assert.That(battle.ElapsedMs, Is.EqualTo(before));
            MainApproachDriver.Click(siege, "Resume"); battle.Advance(1000);
            Assert.That(battle.ElapsedMs, Is.GreaterThan(before));
            yield return Open();
            Assert.That(Object.FindObjectOfType<MainGameRoot>().PlaySettings.PauseAtDecision, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator OneCapturedDragAdvancesFirstGuideAndTeachingStillHoldsContinuousMode()
        {
            _scope.Dispose();
            _scope = new MainSceneStorageScope(freshGuidance: true, createPlayStorage: () => _play);
            Assert.That(new Game.Settings.PlaySettingsSession(_play).TrySave(false), Is.True);
            yield return Open();
            var opening = Object.FindObjectOfType<ApproachPanelView>();
            MainApproachDriver.Solve(opening); MainApproachDriver.Click(opening, "Continue"); yield return null;
            var root = Object.FindObjectOfType<MainGameRoot>();
            var siege = Object.FindObjectOfType<SiegeTrialView>();
            var battle = ((SiegeRescueActivity)root.Presenter.Session.Activity).Battle;
            var guide = root.Presenter.Overlays.Guide;
            Assert.That(guide.IsActive, Is.True);
            int before = battle.ElapsedMs; battle.Advance(1000);
            Assert.That(battle.ElapsedMs, Is.EqualTo(before));
            var card = siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First();
            var inputCamera = PortraitInput(siege.GetComponent<Canvas>(), out var inputTarget);
            _temporary.Add(inputCamera.gameObject); _temporary.Add(inputTarget);
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            var pointer = Pointer(card); card.OnBeginDrag(pointer);
            Assert.That(guide.StepIndex, Is.EqualTo(1));
            pointer.position = Screen(siege.Scene.Field, siege.GetComponent<OutingFieldView>().Layout, 10);
            card.OnDrag(pointer); card.OnEndDrag(pointer);
            Assert.That(guide.StepIndex, Is.EqualTo(2));
            Assert.That(siege.Scene.Plan.DeployLane, Is.EqualTo("mid"));
            MainApproachDriver.Click(siege, "Submit"); yield return null;
            Assert.That(guide.IsComplete, Is.True);
            before = battle.ElapsedMs; battle.Advance(1000);
            Assert.That(battle.ElapsedMs, Is.GreaterThan(before));
            LogAssert.NoUnexpectedReceived();
        }

        private static PointerEventData Pointer(Component card) => new PointerEventData(EventSystem.current)
        { pointerId = 41, button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(RenderCamera(card), card.transform.position) };
        private static Vector2 Screen(SiegeBattlefield field, OutingFieldLayout layout, int cell)
        {
            var rect = field.rectTransform;
            var point = layout.CellPoint(cell);
            var local = new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, point.x / layout.Width),
                Mathf.Lerp(rect.rect.yMax, rect.rect.yMin, point.y / layout.Height));
            return RectTransformUtility.WorldToScreenPoint(RenderCamera(field), rect.TransformPoint(local));
        }
        private static Camera RenderCamera(Component owner)
        {
            var canvas = owner.GetComponentInParent<Canvas>();
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
        internal static Camera PortraitInput(Canvas canvas, out RenderTexture target)
        {
            var camera = new GameObject("PortraitInputCamera", typeof(Camera)).GetComponent<Camera>();
            target = new RenderTexture(390, 844, 24); camera.targetTexture = target;
            camera.cullingMask = 1 << canvas.gameObject.layer;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            return camera;
        }
        private static IEnumerator Capture(Component owner, string name)
        {
            var canvas = owner.GetComponent<Canvas>();
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            var camera = new GameObject("StrategyCapture", typeof(Camera)).GetComponent<Camera>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            camera.cullingMask = 1 << canvas.gameObject.layer;
            foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844),
                new Vector2Int(768, 1024) })
            {
                var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                yield return null; yield return null; Canvas.ForceUpdateCanvases();
                camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply(); RenderTexture.active = previous;
                string folder = Path.GetFullPath("StrategyEvidence"); Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + "-" + size.x + "x" + size.y + ".png"),
                    image.EncodeToPNG());
                Object.Destroy(image); camera.targetTexture = null; target.Release(); Object.Destroy(target);
            }
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
            Object.Destroy(camera.gameObject); yield return null; Canvas.ForceUpdateCanvases();
        }
    }
}
#endif
