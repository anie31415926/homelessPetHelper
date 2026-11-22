#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Station.UI;
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
    public sealed class OverlayMotionClippingPlayTests
    {
        [UnityTest]
        public IEnumerator PopupButtonMotionFitsMasksAtBothScrollEndsAndKeepsHitAreasFixed()
        {
            using var storage = new MainSceneStorageScope();
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            var map = Object.FindObjectOfType<CityMapView>();
            MainApproachDriver.Click(map, "Yard"); MainApproachDriver.Click(map, "Primary");
            var view = Object.FindObjectOfType<ApproachPanelView>();
            MainApproachDriver.Click(view, "Pause");
            var canvas = view.GetComponent<Canvas>();
            var camera = new GameObject("MaskMotionEvidence", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844),
                new Vector2Int(768, 1024) })
            {
                var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                for (int frame = 0; frame < 5; frame++) { yield return null; Canvas.ForceUpdateCanvases(); }
                yield return InspectPage(view, camera, target, "pause");
                MainApproachDriver.Click(view, "OverlaySettings");
                for (int frame = 0; frame < 5; frame++) { yield return null; Canvas.ForceUpdateCanvases(); }
                yield return InspectPage(view, camera, target, "settings");
                MainApproachDriver.Click(view, "OverlayCancelSettings");
                camera.targetTexture = null; target.Release(); Object.Destroy(target);
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            Object.Destroy(camera.gameObject); yield return null; LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator InspectPage(Component view, Camera camera, RenderTexture target, string page)
        {
            int checkedStates = 0;
            var pointer = new PointerEventData(EventSystem.current) { pointerId = -1 };
            foreach (var feedback in view.GetComponentsInChildren<UiMotionFeedback>()
                .Where(item => item.GetComponent<Button>()?.IsInteractable() == true &&
                    item.GetComponentsInParent<RectMask2D>().Length > 0))
            {
                var data = new SerializedObject(feedback);
                var visual = (RectTransform)data.FindProperty("_visual").objectReferenceValue;
                var scope = (MotionScope)data.FindProperty("_scope").objectReferenceValue;
                var hit = (RectTransform)feedback.transform;
                var scroll = feedback.GetComponentInParent<ScrollRect>();
                foreach (float end in new[] { 1f, 0f })
                {
                    if (scroll.vertical) scroll.verticalNormalizedPosition = end;
                    Canvas.ForceUpdateCanvases();
                    // Check fully visible items at each real scroll endpoint; partial edge items are intentional.
                    var mask = feedback.GetComponentInParent<RectMask2D>();
                    if (!Contains(mask, hit)) continue;
                    checkedStates++;
                    var before = new Vector3[4]; hit.GetWorldCorners(before);
                    feedback.OnPointerEnter(pointer);
                    yield return Advance(scope, MotionCue.Hover, visual, feedback);
                    Assert.That(visual.localScale.x, Is.GreaterThan(1), "Exercise the actual hover animation.");
                    Capture(camera, target, page + "-" + feedback.name + "-" + end);
                    feedback.OnPointerDown(pointer);
                    yield return Advance(scope, MotionCue.Press, visual, feedback);
                    feedback.OnPointerUp(pointer); feedback.SetSelected(true);
                    yield return Advance(scope, MotionCue.Selected, visual, feedback);
                    feedback.SetSelected(false); feedback.OnPointerExit(pointer);
                    yield return Advance(scope, MotionCue.Hover, visual, feedback);
                    var after = new Vector3[4]; hit.GetWorldCorners(after);
                    Assert.That(after, Is.EqualTo(before), "Animation must preserve the stable hit root.");
                    foreach (var graphic in visual.GetComponentsInChildren<Graphic>())
                        Assert.That(graphic.canvasRenderer.cull, Is.False, graphic.name + " unexpectedly culled");
                }
            }
            Assert.That(checkedStates, Is.GreaterThan(0), "The page must exercise masked button motion.");
        }

        private static IEnumerator Advance(MotionScope scope, MotionCue cue, RectTransform visual, Component owner)
        {
            float finish = scope.UiSeconds + scope.Library.Duration(cue) + .04f;
            do
            {
                yield return null; Canvas.ForceUpdateCanvases();
                foreach (var mask in owner.GetComponentsInParent<RectMask2D>())
                    Assert.That(Contains(mask, visual), Is.True, owner.name + " motion crosses " + mask.name);
            } while (scope.UiSeconds < finish);
        }

        private static bool Contains(RectMask2D mask, RectTransform visual)
        {
            var corners = new Vector3[4]; visual.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = mask.rectTransform.InverseTransformPoint(corner);
                if (point.x < mask.rectTransform.rect.xMin - .1f || point.x > mask.rectTransform.rect.xMax + .1f ||
                    point.y < mask.rectTransform.rect.yMin - .1f || point.y > mask.rectTransform.rect.yMax + .1f)
                    return false;
            }
            return true;
        }

        private static void Capture(Camera camera, RenderTexture target, string name)
        {
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            RenderTexture.active = previous; Directory.CreateDirectory("MaskMotionEvidence");
            File.WriteAllBytes("MaskMotionEvidence/" + name + "-" + target.width + "x" + target.height + ".png",
                image.EncodeToPNG());
            Object.Destroy(image);
        }
    }
}
#endif
