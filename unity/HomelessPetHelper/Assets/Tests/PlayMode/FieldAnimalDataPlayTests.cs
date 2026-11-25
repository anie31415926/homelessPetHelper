#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Outing.UI;
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
    public sealed class FieldAnimalDataPlayTests
    {
        [UnityTest]
        public IEnumerator AuthoredBoardGrowsShrinksReusesAndHitsHighCellsAndGuideTargets()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Panels/Panel_Approach.prefab");
            var root = Object.Instantiate(prefab); root.SetActive(false);
            var view = root.GetComponent<ApproachPanelView>();
            var board = view.Board;
            string json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Settings/Game/YardFieldConfig.json").text;
            var source = new TextAsset(JsonUtility.ToJson(FieldTestData.Resize(json, 9, 10)));
            using var storage = new MainSceneStorageScope();
            var data = new OutingFieldLayout(source);
            using var session = data.Opening.Create(Guid.NewGuid(), 1);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            view.Bind(session, content.ApproachText, false, () => { }, () => { }, _ => { });
            root.SetActive(true); yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(board.CellCount, Is.EqualTo(90)); Assert.That(board.PooledCells, Is.EqualTo(90));
            var retained = board.Cell(64);
            var targets = new List<RectTransform>();
            Assert.That(view.ResolveGuideTarget("approach.move.64", targets), Is.True);
            Assert.That(targets.Single(), Is.SameAs(retained.transform));
            int pressed = -1;
            board.Pressed += cell => pressed = cell;
            var rect = (RectTransform)retained.transform;
            var events = new GameObject("BoardTestEvents", typeof(EventSystem));
            var pointer = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 47,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            board.OnPointerDown(pointer);
            Assert.That(pressed, Is.EqualTo(64)); Assert.That(board.IsHeld, Is.True);
            board.Configure(content.CreateApproachCatalog().Get(ApproachMode.Cleanup));
            Assert.That(board.IsHeld, Is.False); Assert.That(board.CellCount, Is.EqualTo(56));
            Assert.That(retained.gameObject.activeSelf, Is.False);
            board.Configure(session.Template);
            Assert.That(board.Cell(64), Is.SameAs(retained)); Assert.That(board.PooledCells, Is.EqualTo(90));
            view.Unbind(); Object.Destroy(root); Object.Destroy(source); Object.Destroy(events); yield return null;
        }

        [UnityTest]
        public IEnumerator MainKeepsInitialAnimalAndCoatFromOpeningThroughVictoryAndStation()
        {
            using var storage = new MainSceneStorageScope();
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            var root = Object.FindObjectOfType<MainGameRoot>();
            var station = Object.FindObjectOfType<StationPanelView>(true);
            var map = Object.FindObjectOfType<CityMapView>(true);
            var opening = Object.FindObjectOfType<ApproachPanelView>(true);
            var siege = Object.FindObjectOfType<SiegeTrialView>(true);
            MainApproachDriver.Click(station, "Restart"); MainApproachDriver.Click(map, "Yard");
            MainApproachDriver.Click(map, "Primary");
            var initial = root.Presenter.OutingAnimal;
            Assert.That(initial, Is.Not.Null); Assert.That(initial.Coat.Dirtiness, Is.EqualTo(65));
            var openingCoat = opening.FieldScene.Animal.GetComponent<AnimalCoatView>();
            Assert.That(openingCoat.Snapshot, Is.SameAs(initial));
            MainApproachDriver.Solve(opening); MainApproachDriver.Click(opening, "Continue"); yield return null;
            var activity = (SiegeRescueActivity)root.Presenter.Session.Activity;
            Assert.That(activity.Individual.Current, Is.SameAs(initial));
            Assert.That(openingCoat.HasResources, Is.False, "Closing releases derived presentation resources.");
            foreach (var coat in siege.GetComponentsInChildren<AnimalCoatView>(true))
                Assert.That(coat.Snapshot, Is.SameAs(initial));
            for (int i = 0; i < 1000 && activity.Battle.Phase != SiegeState.BattlePhase.Won &&
                activity.Battle.Phase != SiegeState.BattlePhase.Timeout; i++)
            {
                if (activity.Battle.Phase == SiegeState.BattlePhase.Decision)
                {
                    var cell = siege.GetComponentsInChildren<SiegeChoiceCell>().First();
                    cell.GetComponent<Button>().onClick.Invoke();
                    MainApproachDriver.Click(siege,
                        new[] { "Top", "Mid", "Bottom" }[activity.Battle.Snapshot.Turn % 3]);
                    MainApproachDriver.Click(siege, "Submit");
                }
                activity.Battle.Advance(1000); yield return null;
            }
            yield return null;
            Assert.That(activity.IsRescueComplete, Is.True);
            var rescued = activity.Individual.Current;
            Assert.That(rescued.AnimalId, Is.EqualTo(initial.AnimalId));
            Assert.That(rescued.Coat, Is.SameAs(initial.Coat));
            Assert.That(rescued.Disposition.Emotion, Is.EqualTo(AnimalEmotion.Relaxed));
            MainApproachDriver.Click(siege, "Retry"); yield return null;
            var summary = station.GetComponentsInChildren<AnimalCoatView>().Single();
            Assert.That(summary.Snapshot, Is.SameAs(rescued));
            Assert.That(root.Presenter.Session.Station.Animals.Single().AnimalAtArrival, Is.SameAs(rescued));
            MainApproachDriver.Click(station, "Enter"); yield return null;
            foreach (var coat in station.Scene.GetComponentsInChildren<AnimalCoatView>(true)
                .Where(coat => coat.Snapshot != null))
                Assert.That(coat.Snapshot, Is.SameAs(rescued));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CoatRendersVisibleDirtyCleanDifferenceAndReleasesOnReuse()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Panels/Panel_Approach.prefab"));
            root.SetActive(false);
            var view = root.GetComponent<ApproachPanelView>();
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var field = content.CreateFields().Get("yard");
            using var session = field.Opening.Create(Guid.NewGuid(), 0);
            var animal = content.CreateMapConfig().GetRescue("ordinary-dog").CreateIndividual();
            view.ConfigureField(field, content.GetOutingBackground("yard"), true, session);
            view.Bind(session, content.ApproachText, true, () => { }, () => { }, _ => { });
            view.FieldScene.ShowAnimal(animal.Current); root.SetActive(true); yield return null;
            var surface = view.FieldScene.Animal.GetComponent<FieldPetMotion>().Surface;
            var coat = surface.GetComponent<AnimalCoatView>();
            var canvas = root.GetComponent<Canvas>();
            var camera = new GameObject("CoatEvidenceCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            var target = new RenderTexture(540, 960, 24); camera.targetTexture = target;
            yield return null; Canvas.ForceUpdateCanvases();
            var dirty = Capture(camera, target, "dirty");
            var oldMaterial = surface.material;
            animal.TryApplyCoatCondition(animal.Current, new AnimalCoatCondition(0, 0));
            coat.Show(animal.Current); yield return null;
            Assert.That(surface.material, Is.SameAs(oldMaterial));
            var clean = Capture(camera, target, "clean");
            int changed = dirty.Zip(clean, (a, b) => a.Equals(b) ? 0 : 1).Sum();
            Assert.That(changed, Is.GreaterThan(50), "Dirty coat must visibly affect the actual rendered animal.");
            root.SetActive(false); Assert.That(coat.HasResources, Is.False);
            root.SetActive(true); yield return null; Assert.That(coat.HasResources, Is.True);
            Assert.That(surface.material, Is.Not.SameAs(oldMaterial));
            view.Unbind(); camera.targetTexture = null; target.Release(); Object.Destroy(target);
            Object.Destroy(camera.gameObject); Object.Destroy(root); yield return null;
        }

        private static Color32[] Capture(Camera camera, RenderTexture target, string name)
        {
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            RenderTexture.active = previous;
            string folder = Path.GetFullPath("AnimalCoatEvidence"); Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            var pixels = image.GetPixels32(); Object.Destroy(image); return pixels;
        }
    }
}
#endif
