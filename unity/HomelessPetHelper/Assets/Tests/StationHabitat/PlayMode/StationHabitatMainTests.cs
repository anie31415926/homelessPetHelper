#if UNITY_EDITOR
using HomelessPetHelper.Tests.Support;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.Habitat;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.StationHabitat.PlayMode
{
    public sealed class StationHabitatMainTests
    {
        [UnityTest]
        public IEnumerator CheckedCatUsesTheRealIdentityAndPresentationCannotAdvanceBusinessState()
        {
            using var storageScope = new MainSceneStorageScope();
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            var root = UnityEngine.Object.FindObjectOfType<MainGameRoot>();
            var station = UnityEngine.Object.FindObjectOfType<StationPanelView>();
            var map = UnityEngine.Object.FindObjectOfType<CityMapView>(true);
            var siege = UnityEngine.Object.FindObjectOfType<SiegeTrialView>(true);
            Assert.That(station.Scene.Count, Is.Zero);
            Click(station, "Restart");
            Click(map, "Primary"); MainApproachDriver.Complete();
            var activity = (SiegeRescueActivity)root.Presenter.Session.Activity;
            yield return Win(siege, activity);
            Click(siege, "Retry");
            Click(station, "Enter");
            Assert.That(station.Scene.Cells[0].Observing, Is.True, "Unchecked is not healthy.");
            Click(station, "Animal0");
            Click(station, "Check");
            Click(station, "Back");
            yield return null;
            yield return null;
            yield return null;
            var native = UnityEngine.Object.FindObjectOfType<StationCatPlayer>();
            Assert.That(native, Is.Not.Null, "Main must bind the accepted single-skeleton cat.");
            Assert.That(native.GetComponent<HomelessPetHelper.Modules.PetMotion.PetMotionPlayer>(), Is.Null);
            var session = root.Presenter.Session;
            var arrival = session.Station.Animals.Single();
            Assert.That(station.Scene.Cells[0].AnimalId, Is.EqualTo(arrival.AnimalId));
            Assert.That(station.Scene.Cells[0].Observing, Is.False);
            var motion = station.Scene.Cells[0].Motion;
            var before = activity.Individual.Current;
            long minutes = session.Clock.Current.ElapsedMinutes;
            for (int i = 0; i < 4000; i++) motion.Tick(.05);
            station.Scene.Cells[0].SelectButton.onClick.Invoke();
            yield return null;
            Assert.That(session.Station.TryGetCurrentAnimal(arrival.AnimalId, out var after), Is.True);
            Assert.That(after, Is.SameAs(before));
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.EqualTo(minutes));
            Click(station, "Back");
            yield return Capture(station);
            Click(station, "Animal0");
            Assert.That(station.Scene.gameObject.activeInHierarchy, Is.True, "Sheet keeps the room visible.");
            Click(station, "Back");
            Assert.That(station.Scene.Cells[0].Motion, Is.SameAs(motion));
            Click(station, "Restart");
            Click(map, "Cancel");
            Assert.That(station.Scene.Cells[0].Motion, Is.SameAs(motion));
            Assert.That(station.Scene.Cells[0].AnimalId, Is.EqualTo(arrival.AnimalId));
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.EqualTo(minutes));
        }

        [UnityTest]
        public IEnumerator RescuedShibaWalksAndRisesInTheRoomWithoutChangingItsCoatOrSave()
        {
            using var storageScope = new MainSceneStorageScope();
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            var root = UnityEngine.Object.FindObjectOfType<MainGameRoot>();
            var station = UnityEngine.Object.FindObjectOfType<StationPanelView>();
            var map = UnityEngine.Object.FindObjectOfType<CityMapView>(true);
            var siege = UnityEngine.Object.FindObjectOfType<SiegeTrialView>(true);
            Click(station, "Restart"); Click(map, "Yard");
            Click(map, "Primary"); MainApproachDriver.Complete();
            var session = root.Presenter.Session;
            var activity = (SiegeRescueActivity)session.Activity;
            Assert.That(activity.Individual.Current.DefinitionId, Is.EqualTo("dog"));
            yield return Win(siege, activity);
            Click(siege, "Retry"); Click(station, "Enter");
            var resident = station.Scene.Cells[0];
            Assert.That(resident.Observing, Is.True);
            var image = resident.MotionImage;
            Assert.That(image.isActiveAndEnabled, Is.True);
            Assert.That(image.texture.name, Is.EqualTo("T_Station_ShibaMotion"));
            Click(station, "Animal0"); Click(station, "Check"); Click(station, "Back");
            Assert.That(resident.Observing, Is.False);
            var animal = activity.Individual.Current;
            long minutes = session.Clock.Current.ElapsedMinutes;
            yield return VerifyShibaActions(station, resident);
            Assert.That(image.GetComponent<AnimalCoatView>().Snapshot, Is.SameAs(animal));
            Assert.That(session.Station.TryGetCurrentAnimal(animal.AnimalId, out var after), Is.True);
            Assert.That(after, Is.SameAs(animal));
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.EqualTo(minutes));
        }

        private static IEnumerator VerifyShibaActions(StationPanelView station, StationResidentView resident)
        {
            var motion = resident.Motion;
            var image = resident.MotionImage;
            AdvanceTo(motion, HabitatAction.Walk);
            motion.Tick(.1);
            yield return null;
            yield return null;
            var previous = image.uvRect;
            var start = motion.Position;
            Assert.That(previous.width < 0, Is.EqualTo(!motion.FacingLeft));
            motion.Tick(.1);
            yield return null;
            yield return null;
            Assert.That(image.uvRect, Is.Not.EqualTo(previous));
            Assert.That(motion.Position.DistanceTo(start), Is.GreaterThan(0));
            Assert.That(image.material.GetVector("_UvRect").z - image.material.GetVector("_UvRect").x,
                Is.EqualTo(Mathf.Abs(image.uvRect.width)).Within(.001f));
            yield return VerifyShibaSuspension(station, resident);
            AdvanceTo(motion, HabitatAction.Sit);
            for (int i = 0; i < 15; i++) motion.Tick(.1);
            yield return null;
            yield return null;
            Assert.That(image.uvRect.y, Is.EqualTo(.6f).Within(.001f));
            AdvanceTo(motion, HabitatAction.Walk);
            Assert.That(motion.RisingFromRest, Is.True);
            start = motion.Position;
            for (int i = 0; i < 8; i++) motion.Tick(.1);
            yield return null;
            yield return null;
            Assert.That(motion.Position.DistanceTo(start), Is.Zero);
            float x = Mathf.Min(image.uvRect.xMin, image.uvRect.xMax);
            int frame = Mathf.RoundToInt(x * 8) + Mathf.RoundToInt((.8f - image.uvRect.y) * 5) * 8;
            Assert.That(frame, Is.InRange(5, 10), "Rise must show an intermediate frame.");
            for (int i = 0; i < 13; i++) motion.Tick(.1);
            yield return Capture(station, "shiba-walk");
            Assert.That(motion.Position.DistanceTo(start), Is.GreaterThan(0));
        }

        private static IEnumerator VerifyShibaSuspension(StationPanelView station, StationResidentView resident)
        {
            var motion = resident.Motion;
            station.Scene.SetSuspended(true);
            double time = motion.PoseSeconds;
            yield return null; // Present any pose already sampled by Update before the suspension.
            var frame = resident.MotionImage.uvRect;
            yield return null;
            yield return null;
            Assert.That(motion.PoseSeconds, Is.EqualTo(time));
            Assert.That(resident.MotionImage.uvRect, Is.EqualTo(frame));
            station.Scene.SetSuspended(false);
            Click(station, "Animal0");
            yield return null;
            yield return null;
            Assert.That(motion.PoseSeconds, Is.EqualTo(time));
            Assert.That(resident.MotionImage.uvRect, Is.EqualTo(frame));
            Click(station, "Back");
        }

        private static void AdvanceTo(StationHabitatMotion motion, HabitatAction action)
        {
            for (int i = 0; i < 1200 && motion.Action != action; i++) motion.Tick(.1);
            Assert.That(motion.Action, Is.EqualTo(action));
        }

        private static IEnumerator Win(SiegeTrialView siege, SiegeRescueActivity activity)
        {
            for (int i = 0; i < 700 && activity.Battle.Phase != SiegeState.BattlePhase.Won &&
                activity.Battle.Phase != SiegeState.BattlePhase.Timeout; i++)
            {
                if (activity.Battle.Realtime)
                    DeployRealtime(activity.Battle);
                else if (activity.Battle.Phase == SiegeState.BattlePhase.Decision)
                {
                    var cell = siege.GetComponentsInChildren<SiegeChoiceCell>().FirstOrDefault();
                    if (cell != null) cell.GetComponent<Button>().onClick.Invoke();
                    Click(siege, activity.Battle.Snapshot.Turn % 3 == 0 ? "Top" :
                        activity.Battle.Snapshot.Turn % 3 == 1 ? "Mid" : "Bottom");
                    Click(siege, "Submit");
                }
                activity.Battle.Advance(1000);
                yield return null;
            }
            Assert.That(activity.IsRescueComplete, Is.True,
                activity.Battle.Snapshot.EncounterId + ": " + activity.Battle.Phase);
        }

        private static void DeployRealtime(SiegeSession battle)
        {
            var state = battle.Snapshot;
            var card = state.Hand.FirstOrDefault(item => battle.Catalog.Unit(item.DefinitionId).Cost <= state.Energy);
            if (card == null) return;
            var lane = battle.Catalog.Lanes[state.Stats.Deployed % 3];
            var point = lane.PointAt(lane.Length * .2);
            if (!SiegeDeployment.CanDeploy(battle.Catalog, point)) point = lane.PointAt(0);
            battle.TryOrder(new SiegeOrder
            {
                SessionId = state.SessionId, Turn = state.Turn, DeployCardId = card.Id, DeployPoint = point
            }, true, out _, out _);
        }

        private static void Click(Component view, string name)
        {
            if (view is CityMapView map)
            { MainApproachDriver.Click(map, name); return; }
            if (view is SiegeTrialView siege && siege.Scene != null)
            { MainApproachDriver.Click(view, name); return; }
            if (view is StationPanelView station && station.Scene != null && name.StartsWith("Animal",
                StringComparison.Ordinal))
            {
                station.Scene.Cells[int.Parse(name.Substring(6))].SelectButton.onClick.Invoke();
                return;
            }
            var button = view.GetComponentsInChildren<Button>(true).Single(x => x.name == name);
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }

        private static IEnumerator Capture(StationPanelView station, string label = "main")
        {
            var canvas = station.GetComponent<Canvas>();
            var camera = new GameObject("HabitatMainCapture", typeof(Camera)).GetComponent<Camera>();
            camera.cullingMask = 1 << canvas.gameObject.layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            string folder = Path.GetFullPath("StationHabitatEvidence");
            Directory.CreateDirectory(folder);
            foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(768, 1024) })
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
                File.WriteAllBytes(Path.Combine(folder, label + "-" + size.x + "x" + size.y + ".png"),
                    texture.EncodeToPNG());
                UnityEngine.Object.Destroy(texture);
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            canvas.worldCamera = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.Object.Destroy(camera.gameObject);
        }
    }
}
#endif
