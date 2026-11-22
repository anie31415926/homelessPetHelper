#if UNITY_EDITOR
using HomelessPetHelper.Tests.Support;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
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
            for (int i = 0; i < 700 && activity.Battle.Phase != SiegeState.BattlePhase.Won; i++)
            {
                if (activity.Battle.Phase == SiegeState.BattlePhase.Decision)
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
            Assert.That(activity.IsRescueComplete, Is.True);
            Click(siege, "Retry");
            Click(station, "Enter");
            Assert.That(station.Scene.Cells[0].Observing, Is.True, "Unchecked is not healthy.");
            Click(station, "Animal0");
            Click(station, "Check");
            Click(station, "Back");
            yield return null;
            yield return null;
            yield return null;
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

        private static void Click(Component view, string name)
        {
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

        private static IEnumerator Capture(StationPanelView station)
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
                File.WriteAllBytes(Path.Combine(folder, "main-" + size.x + "x" + size.y + ".png"),
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
