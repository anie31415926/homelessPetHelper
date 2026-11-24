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
        public IEnumerator SixSamplesDirectDeployAnywhereInFormationAndKeepFinishedScene()
        {
            foreach (string point in new[] { "parking", "yard", "porch" })
            foreach (int seed in new[] { 0, 1 })
            {
                _point = point; _seed = seed; yield return Open();
                var opening = Object.FindObjectOfType<ApproachPanelView>();
                MainApproachDriver.Solve(opening);
                var snapshot = opening.FieldScene.Snapshot;
                MainApproachDriver.Click(opening, "Continue"); yield return null;
                var siege = Object.FindObjectOfType<SiegeTrialView>();
                var scene = siege.GetComponent<OutingFieldView>();
                Assert.That(scene.Snapshot.Boxes, Is.EqualTo(snapshot.Boxes));
                Assert.That(scene.Snapshot.Bridge, Is.EqualTo(snapshot.Bridge));
                var root = Object.FindObjectOfType<MainGameRoot>();
                var battle = ((SiegeRescueActivity)root.Presenter.Session.Activity).Battle;
                Assert.That(battle.Realtime, Is.True);
                Assert.That(MainApproachDriver.Button(siege, "Submit").gameObject.activeSelf, Is.False);
                var inputCamera = PortraitInput(siege.GetComponent<Canvas>(), out var inputTarget);
                _temporary.Add(inputCamera.gameObject); _temporary.Add(inputTarget);
                yield return null; yield return null; Canvas.ForceUpdateCanvases();
                var card = siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First();
                var before = battle.Snapshot;
                var origin = battle.Catalog.Lanes[0].PointAt(0);
                var exact = new SiegePoint(origin.X + 3, origin.Y + 2);
                Assert.That(SiegeDeployment.CanDeploy(battle.Catalog, exact), Is.True);
                var pointer = Pointer(card); card.OnBeginDrag(pointer);
                pointer.position = ScreenPoint(siege.Scene.Field, exact, battle.Catalog);
                Assert.That(siege.Scene.Field.PickDeployment(pointer.position, inputCamera, out var picked), Is.True);
                card.OnDrag(pointer); card.OnEndDrag(pointer);
                var after = battle.Snapshot;
                Assert.That(after.Units.Count, Is.EqualTo(1), point + seed);
                Assert.That(after.Energy,
                    Is.EqualTo(before.Energy - battle.Catalog.Unit(before.Hand[0].DefinitionId).Cost));
                Assert.That(battle.Position(after.Units[0]).Distance(picked), Is.LessThan(.001));
                Assert.That(after.Hand.Count, Is.EqualTo(before.Hand.Count));
                Assert.That(siege.Scene.Plan.HasAction, Is.False);
                // Late release cannot spend twice. A failed outside drop also preserves resource/card identity.
                card.OnEndDrag(pointer);
                Assert.That(battle.Snapshot.Units.Count, Is.EqualTo(1));
                card = siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First();
                pointer = Pointer(card); card.OnBeginDrag(pointer); pointer.position = new Vector2(-100, -100);
                card.OnDrag(pointer); card.OnEndDrag(pointer);
                Assert.That(battle.Snapshot.Energy, Is.EqualTo(after.Energy));
                Assert.That(battle.Snapshot.Units.Count, Is.EqualTo(1));
                yield return Capture(siege, point + "-realtime-" + seed);
                MainApproachDriver.Click(siege, "Pause"); MainApproachDriver.Click(siege, "Retry");
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator RealtimeSettingsHideObsoleteToggleAndMenusBackgroundStillPause()
        {
            yield return Open();
            var opening = Object.FindObjectOfType<ApproachPanelView>();
            MainApproachDriver.Click(opening, "Pause"); MainApproachDriver.Click(opening, "Settings");
            var toggle = opening.GetComponentsInChildren<Toggle>(true).Single(item => item.name == "PauseAtDecision");
            Assert.That(toggle.gameObject.activeSelf, Is.False);
            MainApproachDriver.Click(opening, "SaveSettings"); MainApproachDriver.Click(opening, "Resume");
            MainApproachDriver.Solve(opening); MainApproachDriver.Click(opening, "Continue"); yield return null;
            var root = Object.FindObjectOfType<MainGameRoot>();
            var siege = Object.FindObjectOfType<SiegeTrialView>();
            var battle = ((SiegeRescueActivity)root.Presenter.Session.Activity).Battle;
            Assert.That(battle.PauseAtDecision, Is.False);
            int before = battle.ElapsedMs; battle.Advance(1000);
            Assert.That(battle.ElapsedMs, Is.GreaterThan(before));
            MainApproachDriver.Click(siege, "Pause"); before = battle.ElapsedMs;
            battle.Advance(1000); Assert.That(battle.ElapsedMs, Is.EqualTo(before));
            MainApproachDriver.Click(siege, "Resume");
            siege.SendMessage("OnApplicationFocus", false); siege.SendMessage("OnApplicationFocus", true);
            before = battle.ElapsedMs; battle.Advance(1000);
            Assert.That(battle.ElapsedMs, Is.EqualTo(before));
            MainApproachDriver.Click(siege, "Resume"); battle.Advance(1000);
            Assert.That(battle.ElapsedMs, Is.GreaterThan(before));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator OneCapturedDragCompletesTeachingAndTeachingStillHoldsRealtime()
        {
            _scope.Dispose();
            _scope = new MainSceneStorageScope(freshGuidance: true, createPlayStorage: () => _play);
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
            pointer.position = ScreenPoint(siege.Scene.Field, battle.Catalog.Lanes[0].PointAt(0), battle.Catalog);
            card.OnDrag(pointer); card.OnEndDrag(pointer);
            Assert.That(guide.IsComplete, Is.True);
            Assert.That(battle.Snapshot.Units.Count, Is.EqualTo(1));
            yield return null;
            before = battle.ElapsedMs; battle.Advance(1000);
            Assert.That(battle.ElapsedMs, Is.GreaterThan(before));
            LogAssert.NoUnexpectedReceived();
        }

        private static Vector2 ScreenPoint(SiegeBattlefield field, SiegePoint point, SiegeCatalog catalog)
        {
            var rect = field.rectTransform;
            var local = new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, (float)(point.X / catalog.Width)),
                Mathf.Lerp(rect.rect.yMax, rect.rect.yMin, (float)(point.Y / catalog.Height)));
            return RectTransformUtility.WorldToScreenPoint(RenderCamera(field), rect.TransformPoint(local));
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
