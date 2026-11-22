#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue;
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
    public sealed class ForestEdgeIntegrationTests
    {
        private MainSceneStorageScope _storage;
        [SetUp] public void IsolateStorage() => _storage = new MainSceneStorageScope();
        [TearDown] public void ReleaseStorage() => _storage.Dispose();

        [UnityTest]
        public IEnumerator NewSitesDoNotCreateRequestsAndConsecutiveOutingsKeepTheirLocationArtwork()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            var root = Object.FindObjectOfType<MainGameRoot>();
            var session = root.Presenter.Session;
            var station = Object.FindObjectOfType<StationPanelView>(true);
            var map = Object.FindObjectOfType<CityMapView>(true);
            var approach = Object.FindObjectOfType<ApproachPanelView>(true);
            var siege = Object.FindObjectOfType<SiegeTrialView>(true);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var copy = new OptionPreviewText(content.MapText);
            MainApproachDriver.Click(station, "Restart");
            Assert.That(session.Requests.Requests.Count, Is.EqualTo(3));
            foreach (string site in new[] { "Park", "Market", "Riverside" })
            {
                MainApproachDriver.Click(map, site);
                Assert.That(MainApproachDriver.Button(map, "Primary").IsInteractable(), Is.False, site);
                Assert.That(session.Clock.Current.ElapsedMinutes, Is.Zero);
                Assert.That(session.Requests.Requests.All(request => request.State == RequestState.Available), Is.True);
                Assert.That(map.GetComponentsInChildren<Text>().Single(label => label.name == "Heading").text,
                    Is.EqualTo(copy.Get(site.ToLowerInvariant())));
            }
            yield return Capture(map, "main-map");
            foreach (string site in new[] { "Parking", "Yard" })
            {
                MainApproachDriver.Click(map, site);
                MainApproachDriver.Click(map, "Primary");
                var picture = content.GetOutingBackground(site.ToLowerInvariant());
                AssertBackground(approach, picture);
                approach.SendMessage("OnApplicationFocus", false);
                approach.SendMessage("OnApplicationFocus", true);
                Assert.That(approach.Session.IsSuspended, Is.True);
                MainApproachDriver.Click(approach, "Pause");
                AssertBackground(approach, picture);
                yield return Capture(approach, site + "-approach");
                MainApproachDriver.Complete();
                AssertBackground(siege, picture);
                yield return Capture(siege, site + "-siege");
                MainApproachDriver.Click(siege, "Retry");
                Assert.That(station.gameObject.activeInHierarchy, Is.True);
                Assert.That(session.Station.Animals, Is.Empty);
                MainApproachDriver.Click(station, "Restart");
            }
            MainApproachDriver.Click(map, "Cancel");
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertBackground(Component view, Sprite expected)
        {
            Assert.That(view.gameObject.activeInHierarchy, Is.True);
            var image = view.GetComponentsInChildren<Image>().Single(item => item.name == "LocationBackground");
            Assert.That(image.sprite, Is.SameAs(expected));
            Assert.That(image.raycastTarget, Is.False, "Artwork must not intercept puzzle or battlefield input.");
        }

        private static IEnumerator Capture(Component view, string name)
        {
            var canvas = view.GetComponent<Canvas>();
            var camera = new GameObject("ForestEdgeCapture", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.cullingMask = 1 << canvas.gameObject.layer;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var target = new RenderTexture(540, 960, 24);
            camera.targetTexture = target;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(540, 960, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            string folder = Path.GetFullPath("../../forest-edge-screenshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            canvas.worldCamera = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(image);
            Object.Destroy(camera.gameObject);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }
    }
}
#endif
