#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Outing.UI;
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

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class PresentationMotionPlayTests
    {
        private MainSceneStorageScope _storage;
        [SetUp] public void SetUp() => _storage = new MainSceneStorageScope();
        [TearDown] public void TearDown() => _storage.Dispose();

        private static IEnumerator Depart()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            var map = Object.FindObjectOfType<CityMapView>();
            MainApproachDriver.Click(map, "Yard"); MainApproachDriver.Click(map, "Primary");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SharedPresetInstancesInterruptResetAndKeepTouchDragAndHitRectsIndependent()
        {
            yield return Depart();
            var scope = Object.FindObjectOfType<ApproachPanelView>().GetComponent<MotionScope>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Widgets/Widget_SiegeChoice.prefab");
            var first = Object.Instantiate(prefab).GetComponent<SiegeChoiceCell>();
            var second = Object.Instantiate(prefab).GetComponent<SiegeChoiceCell>();
            try
            {
                first.BindMotion(scope); second.BindMotion(scope);
                first.Bind(1, "a", false, true, _ => { }); second.Bind(2, "b", false, true, _ => { });
                var motion = first.GetComponent<UiMotionFeedback>();
                var visual = first.GetComponent<Button>().targetGraphic.rectTransform;
                var other = second.GetComponent<Button>().targetGraphic.rectTransform;
                var hit = (RectTransform)first.transform;
                var before = hit.rect;
                var mouse = new PointerEventData(EventSystem.current) { pointerId = -1 };
                motion.OnPointerEnter(mouse);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(visual.localScale.x, Is.EqualTo(1.025f).Within(.001f));
                Assert.That(other.localScale, Is.EqualTo(Vector3.one));
                Assert.That(hit.rect, Is.EqualTo(before));
                Assert.That(hit.localScale, Is.EqualTo(Vector3.one));
                first.GetComponent<Button>().enabled = false;
                yield return null;
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
                first.GetComponent<Button>().enabled = true;
                motion.OnPointerDown(mouse);
                first.Bind(3, "c", false, false, _ => { });
                yield return null;
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
                first.Bind(3, "c", false, true, _ => { });
                var touch = new PointerEventData(EventSystem.current) { pointerId = 7 };
                motion.OnPointerEnter(touch);
                yield return new WaitForSecondsRealtime(.18f);
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one), "Touch must never stick in hover.");
                motion.OnPointerDown(touch);
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(visual.localScale.x, Is.EqualTo(.955f).Within(.001f));
                motion.OnBeginDrag(touch);
                yield return new WaitForSecondsRealtime(.18f);
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
                motion.OnEndDrag(touch);
                first.Bind(4, "d", true, true, _ => { });
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(visual.localScale.x, Is.EqualTo(1.02f).Within(.001f));
                motion.SendMessage("OnApplicationFocus", false);
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
                first.gameObject.SetActive(false); first.gameObject.SetActive(true);
                first.BindMotion(scope); first.Bind(5, "e", false, true, _ => { });
                yield return new WaitForSecondsRealtime(.18f);
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            }
            finally { Object.Destroy(first.gameObject); Object.Destroy(second.gameObject); }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PauseFreezesSceneMeshWhileMenuFeedbackRunsAndBackgroundFreezesBothClocks()
        {
            yield return Depart();
            var opening = Object.FindObjectOfType<ApproachPanelView>();
            var scope = opening.GetComponent<MotionScope>();
            var terrain = opening.GetComponentInChildren<OutingFieldGraphic>();
            yield return new WaitForSecondsRealtime(.1f);
            MainApproachDriver.Click(opening, "Pause");
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(scope.ScenePaused, Is.True);
            float scene = scope.SceneSeconds;
            var vertices = terrain.canvasRenderer.GetMesh().vertices;
            var resume = opening.GetComponentsInChildren<Button>().Single(button => button.name == "OverlayResume");
            var feedback = resume.GetComponent<UiMotionFeedback>();
            feedback.OnPointerEnter(new PointerEventData(EventSystem.current) { pointerId = -1 });
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(scope.SceneSeconds, Is.EqualTo(scene));
            Assert.That(terrain.canvasRenderer.GetMesh().vertices, Is.EqualTo(vertices));
            Assert.That(resume.targetGraphic.rectTransform.localScale.x, Is.GreaterThan(1));
            scope.SendMessage("OnApplicationPause", true);
            float ui = scope.UiSeconds;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(scope.UiSeconds, Is.EqualTo(ui));
            scope.SendMessage("OnApplicationPause", false);
            scope.SendMessage("OnApplicationFocus", false);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(scope.UiSeconds, Is.EqualTo(ui));
            scope.SendMessage("OnApplicationFocus", true);
            MainApproachDriver.Click(opening, "OverlayResume");
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(scope.SceneSeconds, Is.GreaterThan(scene));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RealOpeningAndRescueKeepArtAndProduceRuntimeUiAndMotionEvidence()
        {
            yield return Depart();
            var opening = Object.FindObjectOfType<ApproachPanelView>();
            MeasureFieldMesh(opening.GetComponentInChildren<OutingFieldGraphic>());
            yield return Capture(opening, "opening", 390, 844);
            MainApproachDriver.Solve(opening);
            var cleared = opening.FieldScene.Snapshot;
            yield return Capture(opening, "cleared", 390, 844);
            MainApproachDriver.Click(opening, "Continue"); yield return null;
            var siege = Object.FindObjectOfType<SiegeTrialView>();
            Assert.That(siege.GetComponent<OutingFieldView>().Snapshot.Remaining, Is.EqualTo(cleared.Remaining));
            siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First().GetComponent<Button>().onClick.Invoke();
            MainApproachDriver.Click(siege, "Mid");
            foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(540, 960),
                new Vector2Int(768, 1024), new Vector2Int(432, 960), new Vector2Int(540, 1080) })
                yield return Capture(siege, "planned", size.x, size.y);
            var card = siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First();
            var motion = card.GetComponent<UiMotionFeedback>();
            var mouse = new PointerEventData(EventSystem.current) { pointerId = -1 };
            for (int index = 0; index < 24; index++)
            {
                if (index == 1) motion.OnPointerEnter(mouse);
                if (index == 6) motion.OnPointerDown(mouse);
                if (index == 10) motion.OnPointerUp(mouse);
                if (index == 15) motion.OnPointerExit(mouse);
                yield return Capture(siege, "motion-" + index.ToString("D2"), 390, 844);
                yield return new WaitForSecondsRealtime(.04f);
            }
            MainApproachDriver.Click(siege, "Submit");
            yield return Capture(siege, "battle", 390, 844);
            LogAssert.NoUnexpectedReceived();
        }

        private static void MeasureFieldMesh(OutingFieldGraphic terrain)
        {
            var method = typeof(OutingFieldGraphic).GetMethod("OnPopulateMesh",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null);
            var populate = (System.Action<VertexHelper>)System.Delegate.CreateDelegate(
                typeof(System.Action<VertexHelper>), terrain, method);
            using (var mesh = new VertexHelper())
            {
                for (int index = 0; index < 10; index++) populate(mesh);
                var timer = new System.Diagnostics.Stopwatch();
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                timer.Start();
                for (int index = 0; index < 300; index++) populate(mesh);
                timer.Stop();
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
                TestContext.WriteLine("Motion 300 warm field mesh samples: managedBytes=" + bytes +
                    ", milliseconds=" + timer.Elapsed.TotalMilliseconds +
                    "; Editor CPU only, excludes Canvas/GPU and device performance.");
                Assert.That(mesh.currentVertCount, Is.GreaterThan(0));
                Assert.That(bytes, Is.Zero);
            }
        }

        private static IEnumerator Capture(Component owner, string name, int width, int height)
        {
            var canvas = owner.GetComponent<Canvas>();
            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            float distance = canvas.planeDistance;
            var camera = new GameObject("PresentationCapture", typeof(Camera)).GetComponent<Camera>();
            var target = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousTarget = RenderTexture.active;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                camera.cullingMask = 1 << canvas.gameObject.layer; camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                yield return null; yield return null; Canvas.ForceUpdateCanvases();
                foreach (var cell in owner.GetComponentsInChildren<SiegeChoiceCell>())
                {
                    var label = cell.GetComponentInChildren<Text>();
                    var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                    float heightNeeded = new TextGenerator().GetPreferredHeight(label.text, settings) /
                        label.pixelsPerUnit;
                    Assert.That(heightNeeded, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2),
                        "Hand card name and cost must both fit: " + label.text);
                }
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                string folder = Path.GetFullPath("PresentationEvidence"); Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + "-" + width + "x" + height + ".png"),
                    image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = distance;
                RenderTexture.active = previousTarget; camera.targetTexture = null; target.Release();
                Object.Destroy(image); Object.Destroy(target); Object.Destroy(camera.gameObject);
            }
            yield return null;
        }
    }
}
#endif
