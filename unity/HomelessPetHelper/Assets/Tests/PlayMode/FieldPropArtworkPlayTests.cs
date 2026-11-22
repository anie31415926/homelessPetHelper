#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class FieldPropArtworkPlayTests
    {
        private MainSceneStorageScope _storage;
        private int _seed;
        [SetUp] public void SetUp()
        {
            _storage = new MainSceneStorageScope();
            SceneManager.sceneLoaded += Configure;
        }
        [TearDown] public void TearDown()
        {
            SceneManager.sceneLoaded -= Configure;
            _storage.Dispose();
        }
        private void Configure(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Main") scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MainGameRoot>(true)).Single()
                .ConfigureOutingSeedSource(() => _seed);
        }

        [UnityTest]
        public IEnumerator RealCleanupPuzzleAndRescueRenderSharedArtWithoutChangingSceneState()
        {
            foreach (int seed in new[] { 0, 1 })
            {
                _seed = seed;
                yield return SceneManager.LoadSceneAsync("Main");
                yield return null;
                MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
                var map = Object.FindObjectOfType<CityMapView>();
                MainApproachDriver.Click(map, "Yard");
                MainApproachDriver.Click(map, "Primary");
                var opening = Object.FindObjectOfType<ApproachPanelView>();
                yield return Capture(opening, "opening-" + seed);
                MainApproachDriver.Solve(opening);
                var finished = opening.FieldScene.Snapshot;
                yield return Capture(opening, "cleared-" + seed);
                MainApproachDriver.Click(opening, "Continue");
                yield return null;
                var siege = Object.FindObjectOfType<SiegeTrialView>();
                var field = siege.GetComponent<OutingFieldView>();
                Assert.That(field.Snapshot.Boxes, Is.EqualTo(finished.Boxes));
                Assert.That(field.Snapshot.Remaining, Is.EqualTo(finished.Remaining));
                Assert.That(field.Snapshot.Bridge, Is.EqualTo(finished.Bridge));
                siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First()
                    .GetComponent<Button>().onClick.Invoke();
                MainApproachDriver.Click(siege, "Mid");
                yield return Capture(siege, "planned-" + seed);
                MainApproachDriver.Click(siege, "Submit");
                yield return Capture(siege, "battle-" + seed);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator Capture(Component view, string name)
        {
            var canvas = view.GetComponent<Canvas>();
            var camera = new GameObject("FieldArtCapture", typeof(Camera)).GetComponent<Camera>();
            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.cullingMask = 1 << canvas.gameObject.layer;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                var sizes = new[] { new Vector2Int(390, 844), new Vector2Int(540, 960),
                    new Vector2Int(768, 1024), new Vector2Int(432, 960), new Vector2Int(540, 1080),
                    new Vector2Int(390, 844) };
                string folder = Path.GetFullPath("FieldPropEvidence");
                Directory.CreateDirectory(folder);
                for (int index = 0; index < sizes.Length; index++)
                    yield return CaptureSize(view, camera, sizes[index], index == sizes.Length - 1, folder, name);
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                Object.Destroy(camera.gameObject);
            }
            yield return null;
        }

        private static IEnumerator CaptureSize(Component view, Camera camera, Vector2Int size,
            bool cutout, string folder, string name)
        {
            var target = new RenderTexture(size.x, size.y, 24);
            var safeViews = view.GetComponentsInChildren<SafeAreaView>();
            var anchors = safeViews.Select(safe =>
                (rect: (RectTransform)safe.transform, min: ((RectTransform)safe.transform).anchorMin,
                    max: ((RectTransform)safe.transform).anchorMax, enabled: safe.enabled)).ToArray();
            try
            {
                camera.targetTexture = target;
                yield return null;
                yield return null;
                if (cutout)
                    foreach (var safe in safeViews)
                    {
                        safe.enabled = false;
                        var rect = (RectTransform)safe.transform;
                        rect.anchorMin = new Vector2(0, .045f);
                        rect.anchorMax = new Vector2(1, .91f);
                    }
                Canvas.ForceUpdateCanvases();
                var terrain = view.GetComponentInChildren<OutingFieldGraphic>();
                Assert.That(terrain.mainTexture.name, Is.EqualTo("T_Outing_GardenProps"));
                var mesh = terrain.canvasRenderer.GetMesh();
                Assert.That(mesh.uv.Any(uv => uv.x > .01f && uv.y > .01f), Is.True,
                    "The actual scene must sample its sprite texture, rather than white procedural geometry.");
                SaveCapture(camera, target, Path.Combine(folder, name + "-" + size.x + "x" + size.y +
                    (cutout ? "-cutout" : "") + ".png"));
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                for (int index = 0; index < safeViews.Length; index++)
                {
                    anchors[index].rect.anchorMin = anchors[index].min;
                    anchors[index].rect.anchorMax = anchors[index].max;
                    safeViews[index].enabled = anchors[index].enabled;
                }
            }
        }

        private static void SaveCapture(Camera camera, RenderTexture target, string path)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(image);
            }
        }
    }
}
#endif
