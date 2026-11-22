#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class UiLayoutCompliancePlayTests
    {
        private MainSceneStorageScope _storage;
        private readonly List<string> _issues = new List<string>();
        [SetUp] public void SetUp()
        {
            _issues.Clear();
            _storage = new MainSceneStorageScope();
        }
        [TearDown] public void TearDown() => _storage.Dispose();

        [UnityTest]
        public IEnumerator MainPagesKeepReadableTextAndUsableControlsAcrossPhoneAndTabletLayouts()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            var station = Object.FindObjectOfType<StationPanelView>();
            yield return Capture(station, "station");
            MainApproachDriver.Click(station, "Restart");
            var map = Object.FindObjectOfType<CityMapView>();
            yield return Capture(map, "map");
            MainApproachDriver.Click(map, "Yard"); MainApproachDriver.Click(map, "Primary");
            yield return null;
            var approach = Object.FindObjectOfType<ApproachPanelView>();
            yield return Capture(approach, "opening");
            MainApproachDriver.Solve(approach); MainApproachDriver.Click(approach, "Continue");
            yield return null;
            var siege = Object.FindObjectOfType<SiegeTrialView>();
            yield return Capture(siege, "rescue");
            MainApproachDriver.Click(siege, "Pause");
            yield return Capture(siege, "pause");
            MainApproachDriver.Click(siege, "OverlaySettings");
            yield return Capture(siege, "settings");
            LogAssert.NoUnexpectedReceived();
            Assert.That(_issues, Is.Empty, string.Join("\n", _issues));
        }

        [UnityTest]
        public IEnumerator AlternateSavedPreviewsKeepReadableInitialLayouts()
        {
            foreach (string scene in new[] { "SC_OptionWhiteboard", "SC_RescueCase", "SC_StationWhiteboard",
                "PetMotion/SC_PetMotion" })
            {
                yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    "Assets/EditorScenes/" + scene + ".unity", new LoadSceneParameters());
                yield return null;
                var canvas = Object.FindObjectsOfType<Canvas>().Single(item => item.isRootCanvas);
                yield return Capture(canvas, scene.Replace('/', '-'));
            }
            Assert.That(_issues, Is.Empty, string.Join("\n", _issues));
        }

        [UnityTest]
        public IEnumerator CardPricesRemainIndependentOfNamesAcrossDigitsAndReuse()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Widgets/Widget_SiegeChoice.prefab");
            var card = Object.Instantiate(prefab).GetComponent<SiegeChoiceCell>();
            try
            {
                foreach (int cost in new[] { 0, 1, 12, 999 })
                {
                    card.Bind(1, "很长的道具名称", true, true, _ => { });
                    card.SetCost(cost, false, "资源不足");
                    var labels = card.GetComponentsInChildren<Text>();
                    Assert.That(labels.Single(text => text.name == "Cost").text, Is.EqualTo(cost.ToString()));
                    Assert.That(labels.Single(text => text.name == "Label").text, Is.EqualTo("很长的道具名称"));
                    Assert.That(labels.Single(text => text.name == "Availability").text, Is.EqualTo("资源不足"));
                    Assert.That(labels.All(text => !text.resizeTextForBestFit), Is.True);
                    Assert.That(labels.All(text => text.fontSize >= 42), Is.True);
                    card.Bind(2, "next", false, true, _ => { });
                    Assert.That(card.GetComponentsInChildren<Text>().Any(text => text.name == "Cost"), Is.False,
                        "Reused roster cells cannot retain a previous card price.");
                }
            }
            finally { Object.Destroy(card.gameObject); }
        }

        private IEnumerator Capture(Component owner, string page)
        {
            var canvas = owner.GetComponent<Canvas>();
            var camera = new GameObject("LayoutEvidenceCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            camera.cullingMask = 1 << canvas.gameObject.layer;
            var previousMode = canvas.renderMode; var previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            try
            {
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(360, 640),
                    new Vector2Int(390, 844), new Vector2Int(432, 960), new Vector2Int(540, 1080),
                    new Vector2Int(768, 1024) })
                {
                    var target = new RenderTexture(size.x, size.y, 24);
                    var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    var previousTarget = RenderTexture.active;
                    try
                    {
                        camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera; canvas.planeDistance = 10;
                        for (int frame = 0; frame < 5; frame++) { yield return null; Canvas.ForceUpdateCanvases(); }
                        camera.Render(); RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                        Directory.CreateDirectory("UiLayoutEvidence");
                        File.WriteAllBytes("UiLayoutEvidence/" + page + "-" + size.x + "x" + size.y + ".png",
                            image.EncodeToPNG());
                        if (owner is SiegeTrialView siege && siege.Scene != null)
                            InspectSiegeHud(siege.Scene, canvas, page, size);
                        foreach (var label in owner.GetComponentsInChildren<Text>())
                        {
                            if (string.IsNullOrWhiteSpace(label.text)) continue;
                            if (label.fontSize < 36) _issues.Add(page + "/" + label.name + " font too small");
                            if (label.preferredHeight > label.rectTransform.rect.height + 3)
                                _issues.Add(page + "/" + label.name + " at " + size + ": " + label.text +
                                    " needs " + label.preferredHeight + " has " + label.rectTransform.rect.height);
                        }
                        foreach (var control in owner.GetComponentsInChildren<Selectable>())
                        {
                            if (!control.interactable) continue;
                            var rect = (RectTransform)control.transform;
                            if (rect.rect.width < 143 || rect.rect.height < 143)
                                _issues.Add(page + "/" + control.name + " at " + size +
                                    " small target " + rect.rect.size);
                            LevelUiPlayTests.ScrollIntoView(control);
                            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                            foreach (var corner in corners)
                            {
                                var local = canvas.transform.InverseTransformPoint(corner);
                                var bounds = ((RectTransform)canvas.transform).rect;
                                if (local.x < bounds.xMin - 2 || local.x > bounds.xMax + 2 ||
                                    local.y < bounds.yMin - 2 || local.y > bounds.yMax + 2)
                                {
                                    _issues.Add(page + "/" + control.name + " outside canvas at " + size);
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        RenderTexture.active = previousTarget; camera.targetTexture = null;
                        target.Release(); Object.Destroy(target); Object.Destroy(image);
                    }
                }
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance; Object.Destroy(camera.gameObject);
            }
            yield return null; Canvas.ForceUpdateCanvases(); yield return null;
        }

        private void InspectSiegeHud(SiegeFieldView field, Canvas canvas, string page, Vector2Int size)
        {
            var bindings = new UnityEditor.SerializedObject(field);
            var intent = (Text)bindings.FindProperty("_intent").objectReferenceValue;
            Rect intentBounds = Bounds(intent.rectTransform, canvas);
            foreach (string property in new[] { "_pause", "_objectives" })
            {
                var control = (Button)bindings.FindProperty(property).objectReferenceValue;
                if (control.gameObject.activeInHierarchy && intentBounds.Overlaps(
                    Bounds((RectTransform)control.transform, canvas)))
                    _issues.Add(page + "/intent overlaps " + control.name + " at " + size);
            }
        }

        private static Rect Bounds(RectTransform rect, Canvas canvas)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = canvas.transform.InverseTransformPoint(corners[0]);
            Vector2 max = canvas.transform.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        internal IEnumerator CaptureAndAssert(Component owner, string page)
        {
            _issues.Clear();
            yield return Capture(owner, page);
            Assert.That(_issues, Is.Empty, string.Join("\n", _issues));
        }
    }
}
#endif
