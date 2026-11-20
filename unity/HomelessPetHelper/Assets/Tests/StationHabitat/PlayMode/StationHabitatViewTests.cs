#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Station.Habitat;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.StationHabitat.PlayMode
{
    public sealed class StationHabitatViewTests
    {
        private StationHabitatView _view;
        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/EditorScenes/StationHabitat/SC_StationHabitat.unity", new LoadSceneParameters());
            yield return null;
            yield return null;
            yield return null;
            _view = UnityEngine.Object.FindObjectOfType<StationHabitatView>();
        }

        [UnityTest]
        public IEnumerator ClickPermissionsPauseHideAndReentryKeepTheSameAnimal()
        {
            yield return Load();
            var id = _view.AnimalId;
            var motion = _view.Motion;
            var button = _view.GetComponentInChildren<Button>();
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Any(hit => hit.gameObject == button.gameObject), Is.True,
                "The moving cat must remain reachable through the saved Canvas raycasters.");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(motion.Response, Is.EqualTo(HabitatResponse.NeedsSpace));
            var pause = _view.GetComponentInParent<Canvas>().GetComponentInChildren<Toggle>();
            pause.isOn = true;
            double seconds = motion.ActionSeconds;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(motion.ActionSeconds, Is.EqualTo(seconds));
            Assert.That(_view.GetComponentInChildren<Button>().IsInteractable(), Is.False);
            _view.gameObject.SetActive(false);
            Assert.That(_view.Image.texture, Is.Null);
            yield return null;
            Assert.That(UnityEngine.Object.FindObjectsOfType<StationHabitatRenderer>(), Is.Empty);
            _view.gameObject.SetActive(true);
            _view.ShowResident(id, "Sample", true);
            Assert.That(_view.Motion, Is.SameAs(motion));
            Assert.That(_view.Motion.ActionSeconds, Is.EqualTo(seconds));
            pause.isOn = false;
            yield return null;
            yield return null;
            Assert.That(_view.Image.texture, Is.Not.Null);
            _view.SendMessage("OnApplicationFocus", false);
            _view.SendMessage("OnApplicationPause", true);
            _view.SendMessage("OnApplicationFocus", true);
            seconds = motion.ActionSeconds;
            yield return null;
            Assert.That(_view.Suspended, Is.True);
            Assert.That(motion.ActionSeconds, Is.EqualTo(seconds));
            _view.SendMessage("OnApplicationPause", false);
            yield return null;
            Assert.That(motion.ActionSeconds, Is.GreaterThan(seconds));
            _view.ClearResidents();
            yield return null;
            Assert.That(_view.AnimalId, Is.EqualTo(Guid.Empty));
            Assert.That(_view.Image.texture, Is.Null);
        }

        [UnityTest]
        public IEnumerator OffscreenSamplingCostIsRecordedAndTargetStaysBounded()
        {
            yield return Load();
            var renderer = UnityEngine.Object.FindObjectOfType<StationHabitatRenderer>();
            for (int i = 0; i < 10; i++) renderer.Render(HabitatAction.Walk, i * .01f, true);
            var timer = new System.Diagnostics.Stopwatch();
            long before = GC.GetAllocatedBytesForCurrentThread();
            timer.Start();
            for (int i = 0; i < 100; i++) renderer.Render(HabitatAction.Walk, i * .01f, true);
            timer.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Debug.Log("Habitat 100 rig samples + offscreen renders: managedBytes=" + allocated +
                ", milliseconds=" + timer.Elapsed.TotalMilliseconds + "; Editor CPU only, not device frame time.");
            Assert.That(_view.Image.texture.width, Is.EqualTo(512));
            Assert.That(_view.Image.texture.height, Is.EqualTo(512));
            Assert.That(UnityEngine.Object.FindObjectsOfType<StationHabitatRenderer>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RealCatMovesAndEveryPoseRendersAcrossPortraitSizes()
        {
            yield return Load();
            _view.SetSuspended(true);
            var canvas = _view.GetComponentInParent<Canvas>();
            var camera = new GameObject("HabitatCapture", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.cullingMask = 1 << canvas.gameObject.layer;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            canvas.GetComponentInChildren<SafeAreaView>().enabled = false;
            string folder = Path.GetFullPath("StationHabitatEvidence");
            Directory.CreateDirectory(folder);
            foreach (var size in new[] { new Vector2Int(375, 667), new Vector2Int(390, 844),
                new Vector2Int(540, 1200), new Vector2Int(768, 1024) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                camera.aspect = (float)size.x / size.y;
                foreach (var action in new[] { HabitatAction.Observe, HabitatAction.Walk,
                    HabitatAction.Sit, HabitatAction.Lie })
                {
                    for (int i = 0; i < 3000 && _view.Motion.Action != action; i++) _view.Motion.Tick(.05);
                    Assert.That(_view.Motion.Action, Is.EqualTo(action));
                    yield return null;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    Assert.That(_view.Image.uvRect.width, Is.EqualTo(_view.Motion.FacingLeft ? 1 : -1));
                    camera.Render();
                    var texture = Read(target);
                    File.WriteAllBytes(Path.Combine(folder, size.x + "x" + size.y + "-" + action + ".png"),
                        texture.EncodeToPNG());
                    var raw = Read((RenderTexture)_view.Image.texture);
                    Color32[] rawPixels = raw.GetPixels32();
                    int bottom = Array.FindIndex(rawPixels, pixel => pixel.a > 180) / raw.width;
                    Assert.That(bottom, Is.InRange(35, 75), "Pose must remain grounded: " + action);
                    File.WriteAllBytes(Path.Combine(folder, "raw-" + size.x + "-" + action + ".png"),
                        raw.EncodeToPNG());
                    UnityEngine.Object.Destroy(raw);
                    Assert.That(texture.GetPixels32().Count(IsCatPixel), Is.GreaterThan(150), "Cat absent: " + action);
                    UnityEngine.Object.Destroy(texture);
                    var corners = new Vector3[4];
                    ((RectTransform)_view.transform).GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = camera.WorldToViewportPoint(corner);
                        Assert.That(point.x, Is.InRange(-.005f, 1.005f));
                        Assert.That(point.y, Is.InRange(-.005f, 1.005f));
                    }
                }
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            while (_view.Motion.Action != HabitatAction.Walk) _view.Motion.Tick(.05);
            yield return null;
            var first = Read((RenderTexture)_view.Image.texture);
            var position = _view.Motion.Position;
            for (int i = 0; i < 7; i++) _view.Motion.Tick(.05);
            yield return null;
            var second = Read((RenderTexture)_view.Image.texture);
            var pixels = first.GetPixels32();
            Assert.That(second.GetPixels32().Where((pixel, i) => !pixel.Equals(pixels[i])).Count(),
                Is.GreaterThan(100));
            Assert.That(_view.Motion.Position.DistanceTo(position), Is.GreaterThan(.02));
            UnityEngine.Object.Destroy(first);
            UnityEngine.Object.Destroy(second);
            canvas.worldCamera = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.Object.Destroy(camera.gameObject);
        }

        private static bool IsCatPixel(Color32 pixel) => pixel.r > pixel.g * 1.12f && pixel.g > pixel.b * 1.1f;
        private static Texture2D Read(RenderTexture target)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }
    }
}
#endif
