#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class LevelVisibilityPlayTests
    {
        [UnityTest]
        public IEnumerator SelectingCrateKeepsItsArtVisibleAndItsRuleStateUnchanged()
        {
            using var storage = new MainSceneStorageScope();
            yield return OpenYard();
            var view = Object.FindObjectOfType<ApproachPanelView>();
            // The test seed chooses the puzzle, so selection cannot be mistaken for debris completion.
            int box = view.Session.Template.Boxes.First();
            var before = view.FieldScene.Snapshot;
            var pointer = MainApproachDriver.Pointer(view, box);
            view.Board.OnPointerDown(pointer); view.Board.OnPointerUp(pointer);
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(view.Session.HasBox(box), Is.True);
            Assert.That(view.FieldScene.Snapshot.Boxes, Is.EqualTo(before.Boxes));
            var surface = view.Board.Cell(box).GetComponent<Image>();
            Assert.That(surface.color.a, Is.LessThanOrEqualTo(.25f),
                "Selected-cell overlay must leave the crate underneath visible.");
            yield return new UiLayoutCompliancePlayTests().CaptureAndAssert(view, "selected-crate");
            int destination = view.Session.Template.Id.Contains("staging") ? 38 : 44;
            MainApproachDriver.Drag(view, box, destination);
            Assert.That(view.Session.HasBox(destination), Is.True);
            MainApproachDriver.Click(view, "Undo");
            Assert.That(view.Session.HasBox(box), Is.True);
        }

        [UnityTest]
        public IEnumerator RescueReservesMostOfPhoneForSceneIncludingRouteSelection()
        {
            using var storage = new MainSceneStorageScope();
            yield return OpenYard();
            var opening = Object.FindObjectOfType<ApproachPanelView>();
            MainApproachDriver.Solve(opening); MainApproachDriver.Click(opening, "Continue");
            yield return null;
            var view = Object.FindObjectOfType<SiegeTrialView>();
            var scene = view.GetComponent<OutingFieldView>();
            var safe = (RectTransform)view.Scene.transform;
            var canvas = view.GetComponent<Canvas>();
            yield return new UiLayoutCompliancePlayTests().CaptureAndAssert(view, "rescue-expanded");
            yield return Capture(view, "rescue-expanded", (activeCanvas, size) =>
            {
                float ratio = ((RectTransform)scene.Frame.parent).rect.height / safe.rect.height;
                float minimum = size.x > size.y * .65f ? .40f : .58f;
                Assert.That(ratio, Is.GreaterThanOrEqualTo(minimum),
                    "Scene viewport must dominate the safe area at " + size);
                if (view.Scene.Plan.SelectedCard.HasValue) MainApproachDriver.Click(view, "Clear");
                Canvas.ForceUpdateCanvases();
                var oldBounds = Bounds(scene.Frame, activeCanvas);
                view.Scene.GetComponentsInChildren<SiegeChoiceCell>().First().GetComponent<Button>().onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                var newBounds = Bounds(scene.Frame, activeCanvas);
                Assert.That(Vector2.Distance(newBounds.size, oldBounds.size), Is.LessThan(1),
                    "Choosing a route cannot shrink the board away from the player's finger.");
                Assert.That(Vector2.Distance(newBounds.center, oldBounds.center), Is.LessThan(1));
            });
            yield return new UiLayoutCompliancePlayTests().CaptureAndAssert(view, "rescue-expanded-routes");
        }

        [UnityTest]
        public IEnumerator GuideCaptionAvoidsCurrentTargetsAndTracksTheirMovement()
        {
            using var storage = new MainSceneStorageScope();
            yield return OpenYard();
            var opening = Object.FindObjectOfType<ApproachPanelView>();
            var overlay = opening.Overlays;
            var serialized = new SerializedObject(overlay);
            var caption = (GameObject)serialized.FindProperty("_guideCaption").objectReferenceValue;
            var canvas = opening.GetComponent<Canvas>();
            var camera = new GameObject("GuideMovementCamera", typeof(Camera)).GetComponent<Camera>();
            var renderTarget = new RenderTexture(390, 844, 24);
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            float oldDistance = canvas.planeDistance;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            camera.targetTexture = renderTarget;
            try
            {
                // The Editor's default landscape viewport is outside the supported portrait layout.
                for (int frame = 0; frame < 5; frame++) { yield return null; Canvas.ForceUpdateCanvases(); }
                var target = (RectTransform)opening.Board.Cell(43).transform;
                var targets = new List<RectTransform> { target, opening.PauseTarget, opening.ObjectivesTarget };
                foreach (int cell in new[] { 43, 5, 50, 36 })
                {
                    targets[0] = (RectTransform)opening.Board.Cell(cell).transform;
                    overlay.ShowGuide("1/3 选择木箱，再选择一个可以移动的位置。", targets);
                    yield return null; Canvas.ForceUpdateCanvases(); yield return null;
                    var bounds = Bounds((RectTransform)caption.transform, canvas);
                    foreach (var item in targets)
                        Assert.That(bounds.Overlaps(Bounds(item, canvas)), Is.False,
                            "Guide caption obscures permitted target " + item.name);
                }
                // Place a target at the old bubble, then keep the guide open while it moves.
                Vector3 original = target.position;
                target.position = caption.transform.position;
                overlay.ShowGuide("1/3 点击高亮位置。", new[] { target });
                yield return null; Canvas.ForceUpdateCanvases(); yield return null;
                Assert.That(Bounds((RectTransform)caption.transform, canvas).Overlaps(
                    Bounds(target, canvas)), Is.False);
                target.position = original;
                overlay.HideGuide();
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
                camera.targetTexture = null; renderTarget.Release();
                Object.Destroy(renderTarget); Object.Destroy(camera.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator FirstGuidanceStaysVisibleAndAvoidsCardsRoutesAndSubmitAcrossViewports()
        {
            using var storage = new MainSceneStorageScope(freshGuidance: true);
            yield return OpenYard();
            var opening = Object.FindObjectOfType<ApproachPanelView>();
            yield return Capture(opening, "guide-crate", (canvas, _) =>
                AssertGuide(opening.Overlays, canvas, action =>
                {
                    var targets = new List<RectTransform>();
                    opening.ResolveGuideTarget(action, targets); return targets;
                }));
            MainApproachDriver.Solve(opening); MainApproachDriver.Click(opening, "Continue"); yield return null;
            var siege = Object.FindObjectOfType<SiegeTrialView>();
            var overlay = siege.GetComponentInChildren<LevelOverlayView>(true);
            foreach (string step in new[] { "cards", "routes", "submit" })
            {
                yield return Capture(siege, "guide-" + step, (canvas, _) =>
                    AssertGuide(overlay, canvas, action =>
                    {
                        var targets = new List<RectTransform>(); siege.Scene.ResolveGuideTarget(action, targets);
                        return targets;
                    }));
                if (step == "cards") siege.Scene.GetComponentsInChildren<SiegeChoiceCell>()
                    .First().GetComponent<Button>().onClick.Invoke();
                else if (step == "routes") MainApproachDriver.Click(siege, "Top");
                yield return null;
            }
            MainApproachDriver.Click(siege, "Submit");
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertGuide(LevelOverlayView overlay, Canvas canvas,
            Func<string, List<RectTransform>> resolve)
        {
            var guide = Object.FindObjectOfType<MainGameRoot>().Presenter.Overlays.Guide;
            Assert.That(guide.IsActive, Is.True);
            var data = new SerializedObject(overlay);
            var caption = (GameObject)data.FindProperty("_guideCaption").objectReferenceValue;
            var text = (Text)data.FindProperty("_guideText").objectReferenceValue;
            Assert.That(caption.activeInHierarchy && text.enabled, Is.True, "Every step needs visible instructions.");
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1));
            Rect bounds = Bounds((RectTransform)caption.transform, canvas);
            foreach (string action in guide.Current.AllowedActions)
                foreach (var target in resolve(action))
                    Assert.That(bounds.Overlaps(Bounds(target, canvas)), Is.False,
                        "Guide covers " + action);
            foreach (var button in canvas.GetComponentsInChildren<Button>())
                Assert.That(bounds.Overlaps(Bounds(
                    (RectTransform)button.transform, canvas)), Is.False, "Guide covers " + button.name);
        }

        private static IEnumerator OpenYard()
        {
            void SetSeed(Scene scene, LoadSceneMode mode)
            {
                if (scene.name == "Main") scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<MainGameRoot>(true)).Single().ConfigureOutingSeedSource(() => 1);
            }
            SceneManager.sceneLoaded += SetSeed;
            try { yield return SceneManager.LoadSceneAsync("Main"); yield return null; }
            finally { SceneManager.sceneLoaded -= SetSeed; }
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            var map = Object.FindObjectOfType<CityMapView>();
            MainApproachDriver.Click(map, "Yard"); MainApproachDriver.Click(map, "Primary");
            yield return null; Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator Capture(Component owner, string name, Action<Canvas, Vector2Int> inspect)
        {
            var canvas = owner.GetComponent<Canvas>();
            var camera = new GameObject("VisibilityCamera", typeof(Camera)).GetComponent<Camera>();
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            float oldDistance = canvas.planeDistance;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            try
            {
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(360, 640),
                    new Vector2Int(390, 844), new Vector2Int(432, 960), new Vector2Int(540, 1080),
                    new Vector2Int(768, 1024) })
                {
                    var target = new RenderTexture(size.x, size.y, 24);
                    var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = target;
                        for (int frame = 0; frame < 5; frame++) { yield return null; Canvas.ForceUpdateCanvases(); }
                        inspect(canvas, size);
                        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                        RenderTexture.active = previous;
                        Directory.CreateDirectory("LevelVisibilityEvidence");
                        File.WriteAllBytes("LevelVisibilityEvidence/" + name + "-" + size.x + "x" + size.y + ".png",
                            image.EncodeToPNG());
                    }
                    finally
                    {
                        camera.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(image);
                    }
                }
            }
            finally
            {
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
                Object.Destroy(camera.gameObject);
            }
        }

        private static Rect Bounds(RectTransform rect, Canvas canvas)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = canvas.transform.InverseTransformPoint(corners[0]);
            Vector2 max = canvas.transform.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
#endif
