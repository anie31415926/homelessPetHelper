using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Station;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.Habitat;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.UI.Affection;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class DoudouAffectionPlayTests
    {
        [UnityTest]
        public IEnumerator ParkRescueKeepsDoudouIdentityAndActivatesOnlyItsOwnTouchView()
        {
            var saves = new MemorySaveStorage();
            using var storage = new MainSceneStorageScope(() => saves);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            var root = Object.FindObjectOfType<MainGameRoot>();
            var station = Object.FindObjectOfType<StationPanelView>(true);
            var map = Object.FindObjectOfType<CityMapView>(true);
            var field = Object.FindObjectOfType<ApproachPanelView>(true);
            var siege = Object.FindObjectOfType<SiegeTrialView>(true);
            var session = root.Presenter.Session;
            var content = Resources.FindObjectsOfTypeAll<GameContent>().Single(item => item.name == "GameContent");
            var sprite = content.Artwork.Get(StationArtwork.AffectionDefinitionId, true, out _);
            Assert.That(session.Requests.Requests.Any(item => item.TemplateId == "ordinary"), Is.True);
            Assert.That(session.Requests.Requests.Any(item => item.TemplateId == "ordinary-dog"), Is.True);
            MainApproachDriver.Click(station, "Restart");
            MainApproachDriver.Click(map, "Park");
            MainApproachDriver.Click(map, "Primary");
            yield return WaitFor(field);
            Assert.That(field.FieldScene.Animal.sprite, Is.SameAs(sprite));
            MainApproachDriver.Complete();
            yield return WaitFor(siege);
            var activity = (SiegeRescueActivity)session.Activity;
            Assert.That(activity.Individual.Current.DefinitionId, Is.EqualTo(StationArtwork.AffectionDefinitionId));
            yield return Win(activity);
            MainApproachDriver.Click(siege, "Confirm");
            yield return WaitFor(station);
            Assert.That(session.Station.Animals.Single().AnimalId, Is.EqualTo(activity.Individual.Current.AnimalId));
            MainApproachDriver.Click(station, "Enter");
            yield return null;
            var resident = station.Scene.Cells[0];
            var roomView = resident.GetComponentInChildren<DoudouAffectionView>();
            Assert.That(roomView, Is.Not.Null);
            Assert.That(roomView.raycastTarget, Is.False);
            Assert.That(roomView.CurrentFace, Is.EqualTo(AffectionFace.Calm));
            yield return InPortraitViewport(roomView.canvas.rootCanvas,
                camera => TapResident(roomView, resident.SelectButton, camera));
            var view = station.GetComponentInChildren<DoudouAffectionView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(station.GetComponentsInChildren<Text>().Any(text => text.text == "\u8c46\u8c46"), Is.True);
            Assert.That(view.raycastTarget, Is.True);
            Assert.That(MainApproachDriver.Button(station, "Pet").gameObject.activeInHierarchy, Is.False);
            var check = MainApproachDriver.Button(station, "Check");
            var wash = MainApproachDriver.Button(station, "Clean");
            var groom = MainApproachDriver.Button(station, "Groom");
            Assert.That(check.transform.parent, Is.SameAs(wash.transform.parent));
            Assert.That(wash.transform.parent, Is.SameAs(groom.transform.parent));
            Assert.That(check.transform.GetSiblingIndex(), Is.LessThan(wash.transform.GetSiblingIndex()));
            yield return null;
            yield return Rub(view, station);
            MainApproachDriver.Click(station, "Check");
            yield return Rub(view, station, waitForCalm: false);
            MainApproachDriver.Click(station, "Back");
            Assert.That(view.gameObject.activeInHierarchy, Is.False);
            Assert.That(roomView.CurrentFace, Is.EqualTo(AffectionFace.Smile));
            Assert.That(resident.Observing, Is.False);
            for (int step = 0; step < 2400 && resident.Motion.Pose != HabitatAction.Walk; step++)
                resident.Motion.Tick(.05);
            Assert.That(resident.Motion.Pose, Is.EqualTo(HabitatAction.Walk));
            yield return null;
            var first = roomView.rectTransform.localRotation;
            resident.Motion.Tick(.10);
            yield return null;
            Assert.That(Quaternion.Angle(roomView.rectTransform.localRotation, first), Is.GreaterThan(.1f));
            var animalId = activity.Individual.Current.AnimalId;
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            root = Object.FindObjectOfType<MainGameRoot>();
            Assert.That(root.Presenter.Progress.CanContinue, Is.True,
                root.Presenter.Progress.PersistenceError?.ToString());
            Assert.That(root.Presenter.Session.Station.Animals.Single().AnimalId, Is.EqualTo(animalId));
            Assert.That(root.Presenter.Session.Station.Animals.Single().AnimalDefinitionId,
                Is.EqualTo(StationArtwork.AffectionDefinitionId));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator WaitFor(MonoBehaviour panel)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (!panel.isActiveAndEnabled && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(panel.isActiveAndEnabled, Is.True, panel.name);
        }

        private static IEnumerator Win(SiegeRescueActivity activity)
        {
            for (int i = 0; i < 700 && activity.Battle.Phase != SiegeState.BattlePhase.Won; i++)
            {
                MainApproachDriver.AcknowledgeEnemyIntroduction();
                var state = activity.Battle.Snapshot;
                var card = state.Hand.FirstOrDefault(item =>
                    activity.Battle.Catalog.Unit(item.DefinitionId).Cost <= state.Energy);
                if (card != null)
                {
                    var lane = activity.Battle.Catalog.Lanes[state.Stats.Deployed % activity.Battle.Catalog.Lanes.Count];
                    var point = lane.PointAt(lane.Length * .2);
                    if (!SiegeDeployment.CanDeploy(activity.Battle.Catalog, point)) point = lane.PointAt(0);
                    activity.Battle.TryOrder(new SiegeOrder
                    {
                        SessionId = state.SessionId, Turn = state.Turn, DeployCardId = card.Id, DeployPoint = point
                    }, true, out _, out _);
                }
                activity.Battle.Advance(1000);
                yield return null;
            }
            Assert.That(activity.Battle.Phase, Is.EqualTo(SiegeState.BattlePhase.Won));
            yield return null;
        }

        private static void TapResident(DoudouAffectionView view, Button select, Camera camera)
        {
            var world = view.rectTransform.TransformPoint(view.GetPixelAdjustedRect().center);
            var data = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, world),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits, Is.Not.Empty, $"No hit at {data.position} in the portrait viewport.");
            data.pointerPressRaycast = hits[0];
            var target = hits[0].gameObject;
            Assert.That(ExecuteEvents.GetEventHandler<IPointerDownHandler>(target), Is.SameAs(select.gameObject),
                $"Expected a press on the resident, but hit {target.name}.");
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(target), Is.SameAs(select.gameObject));
            ExecuteEvents.Execute(select.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(select.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(select.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator Rub(DoudouAffectionView view, StationPanelView station, bool waitForCalm = true)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = view.canvas.rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var data = new PointerEventData(EventSystem.current)
            {
                pointerId = 5, button = PointerEventData.InputButton.Left
            };
            Vector2 At(float x, float y)
            {
                var rect = view.GetPixelAdjustedRect();
                float side = Mathf.Min(rect.width, rect.height);
                Assert.That(side, Is.GreaterThan(0));
                var world = view.rectTransform.TransformPoint(rect.center + new Vector2(x - .5f, .5f - y) * side);
                return RectTransformUtility.WorldToScreenPoint(camera, world);
            }
            data.position = At(161f / 512, 331f / 512);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject, Is.SameAs(view.gameObject),
                string.Join(", ", hits.Select(hit => hit.gameObject.name)));
            data.pointerPressRaycast = hits[0];
            var toast = station.GetComponentInChildren<ToastView>(true);
            string toastBefore = toast.CurrentLine;
            ExecuteEvents.Execute(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
            data.position = At(161f / 512, 243f / 512);
            ExecuteEvents.Execute(view.gameObject, data, ExecuteEvents.dragHandler);
            yield return null;
            Assert.That(view.HasRaisedFur, Is.True);
            bool sawSmile = false;
            for (int i = 0; i < 30 && view.CurrentFace != AffectionFace.Laugh; i++)
            {
                yield return new WaitForSecondsRealtime(.035f);
                data.position = At(i % 2 == 0 ? .49f : .30f, i < 8 ? .30f : .58f);
                ExecuteEvents.Execute(view.gameObject, data, ExecuteEvents.dragHandler);
                sawSmile |= view.CurrentFace == AffectionFace.Smile;
            }
            Assert.That(sawSmile, Is.True, "Continuous rubbing should first produce a smile.");
            Assert.That(view.CurrentFace, Is.EqualTo(AffectionFace.Laugh));
            if (waitForCalm) yield return Capture(station);
            ExecuteEvents.Execute(view.gameObject, data, ExecuteEvents.pointerUpHandler);
            Assert.That(view.CurrentFace, Is.EqualTo(AffectionFace.Smile));
            Assert.That(toast.CurrentLine == toastBefore || string.IsNullOrEmpty(toast.CurrentLine), Is.True);
            if (!waitForCalm) yield break;
            float deadline = Time.realtimeSinceStartup + 4;
            while (view.CurrentFace != AffectionFace.Calm && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.CurrentFace, Is.EqualTo(AffectionFace.Calm));
            Assert.That(view.HasRaisedFur, Is.False);
        }

        private static IEnumerator Capture(StationPanelView station) =>
            InPortraitViewport(station.GetComponent<Canvas>(), CapturePortrait);

        private static IEnumerator InPortraitViewport(Canvas canvas, System.Action<Camera> run)
        {
            var camera = new GameObject("DoudouCapture", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.cullingMask = 1 << canvas.gameObject.layer;
            var target = new RenderTexture(390, 844, 24);
            var mode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            float distance = canvas.planeDistance;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            camera.targetTexture = target;
            try
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                run(camera);
            }
            finally
            {
                canvas.renderMode = mode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = distance;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(camera.gameObject);
            }
        }

        private static void CapturePortrait(Camera camera)
        {
            var target = camera.targetTexture;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("DoudouEvidence");
                File.WriteAllBytes("DoudouEvidence/station-doudou-touch.png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(pixels);
            }
        }
    }
}
