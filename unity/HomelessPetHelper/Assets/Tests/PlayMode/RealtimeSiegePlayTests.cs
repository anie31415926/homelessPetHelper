#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEditor;
using Unity.Profiling;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class RealtimeSiegePlayTests
    {
        private MemorySaveStorage _storage;
        private MainSceneStorageScope _scope;
        [SetUp] public void Setup()
        { _storage = new MemorySaveStorage(); _scope = new MainSceneStorageScope(() => _storage); }
        [TearDown] public void Cleanup() => _scope.Dispose();

        [UnityTest]
        public IEnumerator OpeningObjectsStayPlayableWithQuieterSceneryAtPortraitSizes()
        {
            for (int seed = 0; seed < 2; seed++)
            {
                _storage = new MemorySaveStorage();
                int currentSeed = seed;
                void Configure(Scene scene, LoadSceneMode mode)
                {
                    foreach (var item in scene.GetRootGameObjects())
                    {
                        var root = item.GetComponentInChildren<MainGameRoot>(true);
                        if (root != null) root.ConfigureOutingSeedSource(() => currentSeed);
                    }
                }
                SceneManager.sceneLoaded += Configure;
                try
                {
                    yield return SceneManager.LoadSceneAsync("Main"); yield return null;
                    MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
                    MainApproachDriver.Click(Object.FindObjectOfType<CityMapView>(), "Primary");
                    yield return null;
                    var opening = Object.FindObjectOfType<ApproachPanelView>();
                    Assert.That(opening, Is.Not.Null);
                    yield return CaptureHierarchy(opening, "opening-" + seed);
                    MainApproachDriver.Solve(opening);
                    MainApproachDriver.Click(opening, "Continue"); yield return null;
                    Assert.That(Object.FindObjectOfType<SiegeTrialView>().Scene.gameObject.activeInHierarchy, Is.True);
                }
                finally { SceneManager.sceneLoaded -= Configure; }
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator CaptureHierarchy(Component owner, string name)
        {
            var canvas = owner.GetComponent<Canvas>();
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            var camera = StrategyUiPlayTests.PortraitInput(canvas, out var target);
            try
            {
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844),
                    new Vector2Int(768, 1024) })
                {
                    camera.targetTexture = null; target.Release(); Object.Destroy(target);
                    target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                    yield return null; yield return null; Canvas.ForceUpdateCanvases(); camera.Render();
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    var picture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    picture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); picture.Apply();
                    RenderTexture.active = previous;
                    System.IO.Directory.CreateDirectory("HierarchyEvidence");
                    System.IO.File.WriteAllBytes("HierarchyEvidence/" + name + "-" + size.x + "x" + size.y +
                        ".png", picture.EncodeToPNG());
                    Object.Destroy(picture);
                }
            }
            finally
            {
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
                camera.targetTexture = null; target.Release(); Object.Destroy(target);
                Object.Destroy(camera.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator YardPresentationPlaysOneCuePerEventAndFreezesInPause()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            var map = Object.FindObjectOfType<CityMapView>();
            MainApproachDriver.Click(map, "Yard"); MainApproachDriver.Click(map, "Primary");
            MainApproachDriver.Complete(); yield return null;
            var siege = Object.FindObjectOfType<SiegeTrialView>();
            var scene = siege.Scene;
            scene.enabled = false; // Drive a deterministic fixture using the real scene graphic.
            var data = JsonUtility.FromJson<SiegeCatalogData>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Siege/SiegeTrialData.json").text);
            data.encounters.First(item => item.id == "cat").initialTowerIds = new[] { "paper" };
            var paper = data.towerTypes.First(item => item.id == "paper");
            paper.damage = 50;
            var battle = new SiegeSession(new SiegeCatalog(data), 19, "calm", "yard-fx-test", "cat");
            battle.EnableRealtime();
            var state = new SiegeState();
            var cues = new List<string>();
            scene.SetPresentationAudio(cues.Add);
            void Refresh()
            {
                battle.CopyPresentationTo(state);
                scene.Field.Show(battle.Catalog, state, Array.Empty<int>(), null, false);
                Canvas.ForceUpdateCanvases();
            }

            var canvas = siege.GetComponent<Canvas>();
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            var camera = StrategyUiPlayTests.PortraitInput(canvas, out var target);
            try
            {
                yield return null;
                Refresh();
                var drop = battle.Catalog.Lanes[1].PointAt(0);
                Assert.That(battle.TryOrder(new SiegeOrder { SessionId = state.SessionId, Turn = state.Turn,
                    DeployCardId = state.Hand[0].Id, DeployPoint = drop }, true, out var reason, out _),
                    Is.True, reason);
                Refresh(); Refresh();
                var deployed = state.Effects.Single(effect => effect.Kind == SiegeState.EffectKind.Deploy);
                int unitId = deployed.UnitId;
                Assert.That(deployed.From, Is.EqualTo(drop));
                Assert.That(cues, Is.EqualTo(new[] { "siege.toy-drop" }));
                SaveBattleFrame(camera, target, "01-drop");
                battle.Advance(150); Refresh();
                SaveBattleFrame(camera, target, "02-hop");

                for (int step = 0; step < 300 &&
                    !state.Effects.Any(effect => effect.Kind == SiegeState.EffectKind.UnitLost); step++)
                { battle.Advance(50); Refresh(); }
                var lost = state.Effects.Single(effect => effect.Kind == SiegeState.EffectKind.UnitLost);
                var shot = state.Effects.First(effect => effect.Kind == SiegeState.EffectKind.TowerShot);
                Assert.That(lost.UnitId, Is.EqualTo(unitId).And.EqualTo(shot.UnitId));
                Assert.That(lost.From.Distance(shot.To), Is.LessThan(.01));
                Assert.That(state.Units, Is.Empty);
                Assert.That(cues.Count(cue => cue == "siege.paper-shot"), Is.EqualTo(1));
                Assert.That(cues, Does.Not.Contain("siege.paper-impact"));
                Assert.That(cues, Does.Not.Contain("siege.toy-retire"));
                SaveBattleFrame(camera, target, "03a-paper-launch");
                battle.Advance(50); Refresh();
                SaveBattleFrame(camera, target, "03-paper-in-flight");

                battle.SetPaused(true); Refresh();
                int frozen = state.ElapsedMs;
                var frozenVertices = scene.Field.canvasRenderer.GetMesh().vertices;
                battle.Advance(300); Refresh();
                Assert.That(state.ElapsedMs, Is.EqualTo(frozen));
                Assert.That(scene.Field.canvasRenderer.GetMesh().vertices, Is.EqualTo(frozenVertices));
                Assert.That(cues, Does.Not.Contain("siege.paper-impact"));
                battle.SetPaused(false);
                battle.Advance(100); Refresh(); Refresh();
                Assert.That(cues.Count(cue => cue == "siege.paper-impact"), Is.EqualTo(1));
                Assert.That(cues.Count(cue => cue == "siege.toy-retire"), Is.EqualTo(1));
                SaveBattleFrame(camera, target, "04-paper-hit-and-retire");
                battle.Advance(150); Refresh();
                SaveBattleFrame(camera, target, "05-retire-fade");

                scene.Bind(battle, AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/Settings/Siege/SiegeTrialText.json"), () => { }, () => { }, battle.SetPaused);
                int previousCues = cues.Count;
                Refresh(); Refresh();
                Assert.That(cues.Count, Is.EqualTo(previousCues), "Restore cannot replay old Foley.");
                for (int i = 0; i < 10; i++) battle.Advance(50);
                Refresh();
                Assert.That(state.Effects.Any(effect => effect.Kind == SiegeState.EffectKind.UnitLost), Is.False);
            }
            finally
            {
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
                camera.targetTexture = null; target.Release(); Object.Destroy(target);
                Object.Destroy(camera.gameObject);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void SaveBattleFrame(Camera camera, RenderTexture target, string name)
        {
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                Directory.CreateDirectory("YardCombatEvidence");
                File.WriteAllBytes("YardCombatEvidence/" + name + ".png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.Destroy(image); }
        }

        [UnityTest]
        public IEnumerator NinetyUnitsRenderWithinSingleGraphicVertexLimitAtPortraitSizes()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            MainApproachDriver.Click(Object.FindObjectOfType<CityMapView>(), "Primary");
            MainApproachDriver.Complete(); yield return null;
            var siege = Object.FindObjectOfType<SiegeTrialView>();
            var data = JsonUtility.FromJson<SiegeCatalogData>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Siege/SiegeTrialData.json").text);
            data.rules.openingEnergy = data.rules.energyCap = 100;
            foreach (var unit in data.contentUnits) { unit.cost = 1; unit.speed = .1; }
            var battle = new SiegeSession(new SiegeCatalog(data), 19, "calm", "render-stress", "cat");
            battle.EnableRealtime();
            for (int index = 0; index < 90; index++)
            {
                var state = battle.Snapshot;
                var origin = battle.Catalog.Lanes[0].PointAt(0);
                var point = new SiegePoint(origin.X + index % 10 * 3, origin.Y - index / 10 * 2);
                Assert.That(battle.TryOrder(new SiegeOrder
                {
                    SessionId = state.SessionId, Turn = state.Turn,
                    DeployCardId = state.Hand[0].Id, DeployPoint = point
                }, true, out var reason, out _), Is.True, reason);
            }
            yield return null;
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 8);
            Assert.That(recorder.Valid, Is.True);
            try
            {
                yield return null;
                long baseline = recorder.LastValue;
                for (int index = 0; index < 1000; index++) GC.KeepAlive(battle.Snapshot);
                yield return null;
                long snapshots = recorder.LastValue;
                var buffer = new SiegeState(); battle.CopyPresentationTo(buffer);
                yield return null;
                for (int index = 0; index < 1000; index++) battle.CopyPresentationTo(buffer);
                yield return null;
                long reused = recorder.LastValue;
                var state = battle.Snapshot;
                var preview = new SiegeOrder { SessionId = state.SessionId, Turn = state.Turn,
                    DeployCardId = state.Hand[0].Id, DeployPoint = battle.Catalog.Lanes[0].PointAt(0) };
                yield return null;
                for (int index = 0; index < 10000; index++) battle.TryOrder(preview, false, out _, out _);
                yield return null;
                long previews = recorder.LastValue;
                TestContext.WriteLine("Profiler GC allocated frame bytes (includes Editor/frame overhead): baseline=" +
                    baseline + ", 1000 full snapshots=" + snapshots + ", 1000 reuse=" + reused +
                    ", 10000 previews=" + previews);
                Assert.That(snapshots, Is.GreaterThan(reused));
            }
            finally { recorder.Dispose(); }
            var scene = siege.Scene;
            scene.gameObject.SetActive(false);
            scene.Bind(battle, AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Siege/SiegeTrialText.json"), () => { }, () => { }, battle.SetPaused);
            scene.gameObject.SetActive(true);
            var canvas = siege.GetComponent<Canvas>();
            var camera = StrategyUiPlayTests.PortraitInput(canvas, out var target);
            Mesh mesh;
            try
            {
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844),
                    new Vector2Int(768, 1024) })
                {
                    camera.targetTexture = null; target.Release(); Object.Destroy(target);
                    target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                    yield return null; yield return null; Canvas.ForceUpdateCanvases(); camera.Render();
                    var tower = battle.Snapshot.Towers.First(item => item.Status != SiegeState.TowerStatus.Destroyed);
                    var towerPoint = SiegeGeometry.Position(battle.Catalog, tower);
                    var rect = scene.Field.rectTransform;
                    var local = new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax,
                        (float)(towerPoint.X / battle.Catalog.Width)), Mathf.Lerp(rect.rect.yMax, rect.rect.yMin,
                        (float)(towerPoint.Y / battle.Catalog.Height)));
                    string clickedTower = null;
                    void ObserveTower(string id) => clickedTower = id;
                    scene.Field.TowerClicked += ObserveTower;
                    scene.Field.OnPointerClick(new PointerEventData(EventSystem.current)
                    {
                        position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(local)),
                        pointerPressRaycast = new RaycastResult
                        { module = scene.GetComponentInParent<GraphicRaycaster>() }
                    });
                    scene.Field.TowerClicked -= ObserveTower;
                    Assert.That(clickedTower, Is.EqualTo(tower.Id), "Tower hit testing must follow its grid position.");
                    mesh = scene.Field.canvasRenderer.GetMesh();
                    Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                    Assert.That(mesh.vertexCount, Is.LessThan(65000));
                    TestContext.WriteLine("90-unit render " + size + ": " + mesh.vertexCount + " vertices");
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                    RenderTexture.active = previous;
                    System.IO.Directory.CreateDirectory("RealtimeEvidence");
                    System.IO.File.WriteAllBytes("RealtimeEvidence/stress-" + size.x + "x" + size.y + ".png",
                        image.EncodeToPNG());
                    Object.Destroy(image);
                }
            }
            finally
            {
                camera.targetTexture = null; target.Release(); Object.Destroy(target);
                Object.Destroy(camera.gameObject);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FirstBattleUsesSharedHandbookAndRequiresSuccessfulDrop()
        {
            _scope.Dispose();
            _scope = new MainSceneStorageScope(() => _storage, freshGuidance: true);
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            var root = Object.FindObjectOfType<MainGameRoot>();
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            MainApproachDriver.Click(Object.FindObjectOfType<CityMapView>(), "Primary");
            MainApproachDriver.Complete(); yield return null;
            var siege = Object.FindObjectOfType<SiegeTrialView>();
            var overlay = root.Presenter.Overlays;
            var battle = ((SiegeRescueActivity)root.Presenter.Session.Activity).Battle;
            Assert.That(overlay.Top.ToString(), Is.EqualTo("Objectives"));
            Assert.That(overlay.Guide.IsActive, Is.True);
            Button Named(string name) => siege.GetComponentsInChildren<Button>().Single(button => button.name == name);
            Named("OpenHandbook").onClick.Invoke();
            Assert.That(overlay.Top.ToString(), Is.EqualTo("Handbook"));
            var scroll = siege.GetComponentsInChildren<ScrollRect>().Single();
            Canvas.ForceUpdateCanvases(); yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(scroll.content.GetComponentsInChildren<SiegePropIcon>().Length,
                Is.EqualTo(battle.Catalog.Units.Count));
            int deployed = battle.Snapshot.Stats.Deployed;
            Named("HandbookDefense").onClick.Invoke();
            Assert.That(scroll.content.GetComponentsInChildren<Text>().Any(label => label.text.Contains("动物的防备")), Is.True);
            overlay.Back();
            Assert.That(overlay.Top.ToString(), Is.EqualTo("Objectives"));
            Assert.That(battle.Snapshot.Stats.Deployed, Is.EqualTo(deployed));
            overlay.Back(); yield return null;
            var card = siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First(cell => cell.IsClickable);
            var input = new PointerEventData(EventSystem.current) { pointerId = 15, button = PointerEventData.InputButton.Left };
            var field = siege.Scene.Field;
            var corners = new Vector3[4];
            field.rectTransform.GetWorldCorners(corners);
            var canvas = field.canvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            card.OnBeginDrag(input);
            Assert.That(overlay.Guide.StepIndex, Is.EqualTo(1));
            input.position = min - Vector2.one * 200;
            card.OnEndDrag(input);
            Assert.That(overlay.Guide.IsComplete, Is.False);
            Assert.That(battle.Snapshot.Stats.Deployed, Is.EqualTo(deployed));
            Vector2? drop = null;
            for (int y = 1; y < 24 && !drop.HasValue; y++)
                for (int x = 1; x < 24 && !drop.HasValue; x++)
                {
                    var point = new Vector2(Mathf.Lerp(min.x, max.x, x / 24f), Mathf.Lerp(min.y, max.y, y / 24f));
                    if (field.PickDeployment(point, camera, out _)) drop = point;
                }
            Assert.That(drop.HasValue, Is.True);
            input.position = drop.Value;
            card.OnBeginDrag(input); card.OnEndDrag(input);
            Assert.That(overlay.Guide.IsComplete, Is.True);
            yield return null;
            Assert.That(root.Guidance.HasCompleted(overlay.Guide.MechanicId), Is.True);
            Assert.That(battle.Snapshot.Stats.Deployed, Is.EqualTo(deployed + 1));
        }

        [UnityTest]
        public IEnumerator DeploymentDoesNotSaveInsideInput_ExplicitCheckpointAndReloadPreserveState()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            var root = Object.FindObjectOfType<MainGameRoot>();
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            MainApproachDriver.Click(Object.FindObjectOfType<CityMapView>(), "Primary");
            MainApproachDriver.Complete(); yield return null;
            var activity = (SiegeRescueActivity)root.Presenter.Session.Activity;
            var battle = activity.Battle;
            var animal = activity.Individual.Current.AnimalId;
            var state = battle.Snapshot;
            var card = state.Hand.First(item => battle.Catalog.Unit(item.DefinitionId).Cost <= state.Energy);
            var position = battle.Catalog.Lanes[0].PointAt(0);
            position = new SiegePoint(position.X + 2, position.Y - 2);
            int writes = _storage.Writes;
            var timer = Stopwatch.StartNew();
            Assert.That(battle.TryOrder(new SiegeOrder
            {
                SessionId = state.SessionId, Turn = state.Turn, DeployCardId = card.Id, DeployPoint = position
            }, true, out var reason, out _), Is.True, reason);
            timer.Stop();
            Assert.That(_storage.Writes, Is.EqualTo(writes), "Input must not synchronously serialize/write/replay.");
            TestContext.WriteLine("Actual Main deployment call: " + timer.Elapsed.TotalMilliseconds + "ms");
            battle.Advance(1000);
            battle.SetPaused(true); // Keep asynchronous scene unloading from advancing the checkpoint.
            timer.Restart(); root.Presenter.FlushFieldProgress(); timer.Stop();
            TestContext.WriteLine("Actual Main checkpoint: " + timer.Elapsed.TotalMilliseconds + "ms");
            Assert.That(root.Presenter.Progress.CanContinue, Is.True);
            Assert.That(_storage.Writes, Is.GreaterThan(writes));
            state = battle.Snapshot;
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            root = Object.FindObjectOfType<MainGameRoot>();
            Assert.That(root.Presenter.Progress.CanContinue, Is.True,
                root.Presenter.Progress.PersistenceError?.ToString());
            activity = (SiegeRescueActivity)root.Presenter.Session.Activity;
            Assert.That(activity, Is.Not.Null);
            battle = activity.Battle;
            Assert.That(battle.Realtime, Is.True);
            Assert.That(activity.Individual.Current.AnimalId, Is.EqualTo(animal));
            Assert.That(battle.Snapshot.Energy, Is.EqualTo(state.Energy));
            Assert.That(battle.Snapshot.Stats.Deployed, Is.EqualTo(state.Stats.Deployed));
            Assert.That(battle.Snapshot.Units.Select(unit => unit.Id), Is.EqualTo(state.Units.Select(unit => unit.Id)));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
