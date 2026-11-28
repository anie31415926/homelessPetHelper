#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.PetMotion;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege;
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
    public sealed class FieldPetMotionPlayTests
    {
        private GameObject _ownedPanel;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_ownedPanel != null) Object.Destroy(_ownedPanel);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpeningCatHasChangingPixelsFrozenClockCoatAndBoundedResourcesOnReuse()
        {
            using var storage = new MainSceneStorageScope();
            _ownedPanel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Panels/Panel_Approach.prefab"));
            _ownedPanel.SetActive(false);
            _ownedPanel.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var view = _ownedPanel.GetComponent<ApproachPanelView>();
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var field = content.CreateFields().Get("parking");
            using var opening = field.Opening.Create(Guid.NewGuid(), 0);
            var individual = content.CreateMapConfig().GetRescue("ordinary").CreateIndividual();
            view.ConfigureField(field, content.GetOutingBackground("parking"), false, opening);
            view.Bind(opening, content.ApproachText, false, () => { }, () => { }, opening.SetSuspended);
            view.FieldScene.ShowAnimal(individual.Current);
            _ownedPanel.SetActive(true); yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var motion = view.FieldScene.Animal.GetComponent<FieldPetMotion>();
            var clock = _ownedPanel.GetComponent<MotionScope>();
            clock.SendMessage("OnApplicationFocus", false);
            SampleAt(clock, motion, .1f);
            yield return null;
            var first = Pixels((RenderTexture)motion.Surface.texture, "opening-cat-pose-a");
            SampleAt(clock, motion, 7.5f);
            yield return null;
            var second = Pixels((RenderTexture)motion.Surface.texture, "opening-cat-pose-b");
            Assert.That(first.Zip(second, (a, b) => a.Equals(b) ? 0 : 1).Sum(), Is.GreaterThan(30));
            Assert.That(first.Count(pixel => pixel.a > 150), Is.GreaterThan(300));
            Assert.That(first.Count(pixel => pixel.a == 0), Is.GreaterThan(first.Length / 3),
                "The offscreen camera must leave a transparent scene background.");
            Assert.That(motion.Surface.texture.width, Is.LessThanOrEqualTo(256));
            Assert.That(motion.Surface.texture.height, Is.LessThanOrEqualTo(256));
            Assert.That(view.FieldScene.Animal.enabled, Is.False);
            Assert.That(motion.Surface.GetComponent<AnimalCoatView>().Snapshot, Is.SameAs(individual.Current));
            int revision = motion.Player.Revision;
            yield return null; yield return null;
            Assert.That(motion.Player.Revision, Is.EqualTo(revision), "Backgrounded cat must stop sampling.");
            clock.SendMessage("OnApplicationFocus", true);
            // Isolate the presentation suspension contract; Main below exercises the actual pause menu.
            view.FieldScene.SetPresentationPaused(true); yield return null;
            Assert.That(clock.ScenePaused, Is.True);
            revision = motion.Player.Revision;
            yield return null; yield return null;
            Assert.That(motion.Player.Revision, Is.EqualTo(revision), "Pause freezes the visible pose.");
            view.FieldScene.SetPresentationPaused(false);
            yield return null; yield return null;
            Assert.That(motion.Player.Revision, Is.GreaterThan(revision));
            clock.SendMessage("OnApplicationFocus", false);
            // This fixture has no overlay presenter. Remove its legacy menu from the pixel comparison.
            var legacyMenu = (GameObject)new SerializedObject(view).FindProperty("_sceneMenu")
                .objectReferenceValue;
            legacyMenu.SetActive(false);
            Color32[] dirty = null, clean = null;
            yield return CaptureCanvas(_ownedPanel.GetComponent<Canvas>(), "opening-cat-dirty",
                pixels => dirty = pixels);
            individual.TryApplyCoatCondition(individual.Current, new AnimalCoatCondition(0, 0));
            view.FieldScene.ShowAnimal(individual.Current);
            yield return CaptureCanvas(_ownedPanel.GetComponent<Canvas>(), "opening-cat-clean",
                pixels => clean = pixels);
            Assert.That(legacyMenu.activeSelf, Is.False);
            Assert.That(dirty.Zip(clean, (a, b) => a.Equals(b) ? 0 : 1).Sum(), Is.GreaterThan(30),
                "Coat presentation must affect animated pixels, not only the hidden still image.");
            var oldPlayer = motion.Player;
            _ownedPanel.SetActive(false);
            Assert.That(motion.Surface.texture, Is.Null);
            Assert.That(motion.Player, Is.Null);
            Assert.That(motion.Surface.GetComponent<AnimalCoatView>().HasResources, Is.False);
            yield return null;
            Assert.That(oldPlayer == null, Is.True);
            _ownedPanel.SetActive(true); yield return null; yield return null;
            Assert.That(motion.Surface.texture, Is.Not.Null);
            _ownedPanel.SetActive(false);
            view.ConfigureField(field, content.GetOutingBackground("parking"), true, opening);
            _ownedPanel.SetActive(true); yield return null;
            Assert.That(motion.Player, Is.Null);
            Assert.That(motion.Surface.texture, Is.TypeOf<Texture2D>());
            Assert.That(motion.Surface.texture.name, Is.EqualTo("T_Station_ShibaMotion"));
            Assert.That(view.FieldScene.Animal.enabled, Is.False);
            _ownedPanel.BroadcastMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
            view.FieldScene.SetPresentationPaused(false);
            yield return null; yield return null;
            var shibaUv = motion.Surface.uvRect;
            Assert.That(shibaUv.y, Is.EqualTo(.8f).Within(.001f), "Idle uses the authored standing row.");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(motion.Surface.uvRect, Is.Not.EqualTo(shibaUv));
            // Walking must leave the idle row; idle is the default standing wait.
            clock.SendMessage("OnApplicationFocus", false);
            motion.SetDogWalking(true);
            SampleAt(clock, motion, .2f);
            Assert.That(motion.Surface.uvRect.y, Is.LessThan(.8f));
            motion.SetDogWalking(false);
            SampleAt(clock, motion, .2f);
            Assert.That(motion.Surface.uvRect.y, Is.EqualTo(.8f).Within(.001f));
            clock.SendMessage("OnApplicationFocus", true);
            view.FieldScene.SetPresentationPaused(true);
            shibaUv = motion.Surface.uvRect;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(motion.Surface.uvRect, Is.EqualTo(shibaUv));
            _ownedPanel.SetActive(false);
            view.ConfigureField(field, content.GetOutingBackground("parking"), false, opening);
            _ownedPanel.SetActive(true); yield return null; yield return null;
            Assert.That(motion.Surface.texture, Is.TypeOf<RenderTexture>());
            Assert.That(view.FieldScene.Animal.enabled, Is.False);
            Assert.That(motion.Surface.GetComponent<AnimalCoatView>().Snapshot, Is.SameAs(individual.Current));
            view.FieldScene.Configure(field, content.GetOutingBackground("parking"), false, opening,
                showAnimal: false);
            yield return null;
            Assert.That(motion.Surface.gameObject.activeSelf, Is.False, "Exploration has no rescue animal.");
            Assert.That(view.FieldScene.Animal.enabled, Is.False);
            view.Unbind();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MainParkingKeepsAnimatedCatAndCoatThroughOpeningBattleAndArrival()
        {
            using var storage = new MainSceneStorageScope();
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            var root = Object.FindObjectOfType<MainGameRoot>();
            var station = Object.FindObjectOfType<StationPanelView>(true);
            var map = Object.FindObjectOfType<CityMapView>(true);
            var view = Object.FindObjectOfType<ApproachPanelView>(true);
            var siege = Object.FindObjectOfType<SiegeTrialView>(true);
            MainApproachDriver.Click(station, "Restart"); MainApproachDriver.Click(map, "Parking");
            MainApproachDriver.Click(map, "Primary"); yield return null; yield return null;
            var initial = root.Presenter.OutingAnimal;
            var openingMotion = view.FieldScene.Animal.GetComponent<FieldPetMotion>();
            Assert.That(openingMotion.Surface.texture, Is.TypeOf<RenderTexture>());
            MainApproachDriver.Complete(); yield return null; yield return null;
            Assert.That(openingMotion.Surface.texture, Is.Null);
            var field = siege.GetComponent<OutingFieldView>();
            var motion = field.Animal.GetComponent<FieldPetMotion>();
            Assert.That(motion.Surface.isActiveAndEnabled, Is.True);
            Assert.That(motion.Surface.transform.parent, Is.SameAs(field.Animal.transform));
            Assert.That(field.Animal.enabled, Is.False);
            var pixels = Pixels((RenderTexture)motion.Surface.texture, "siege-cat");
            Assert.That(pixels.Count(pixel => pixel.a > 150), Is.GreaterThan(300));
            var legacy = (PetMotionPlayer)new SerializedObject(siege.GetComponent<SiegePetMotion>())
                .FindProperty("_player").objectReferenceValue;
            Assert.That(legacy.gameObject.activeSelf, Is.False, "Hidden legacy rig must not keep animating.");
            foreach (var coat in siege.GetComponentsInChildren<AnimalCoatView>(true))
                Assert.That(coat.Snapshot, Is.SameAs(initial));
            yield return CaptureCanvas(siege.GetComponent<Canvas>(), "main-parking-siege");
            MeasureRendering(siege.GetComponent<MotionScope>(), motion);
            MainApproachDriver.AcknowledgeEnemyIntroduction();
            MainApproachDriver.Click(siege, "Pause"); yield return null;
            int revision = motion.Player.Revision;
            yield return null; yield return null;
            Assert.That(motion.Player.Revision, Is.EqualTo(revision));
            MainApproachDriver.Click(siege, "Resume");
            var activity = (SiegeRescueActivity)root.Presenter.Session.Activity;
            for (int i = 0; i < 1000 && activity.Battle.Phase != SiegeState.BattlePhase.Won &&
                activity.Battle.Phase != SiegeState.BattlePhase.Timeout; i++)
            {
                if (activity.Battle.Phase == SiegeState.BattlePhase.Decision)
                {
                    var cell = siege.GetComponentsInChildren<SiegeChoiceCell>().First();
                    cell.GetComponent<Button>().onClick.Invoke();
                    MainApproachDriver.Click(siege, new[] { "Top", "Mid", "Bottom" }[
                        activity.Battle.Snapshot.Turn % 3]);
                    MainApproachDriver.Click(siege, "Submit");
                }
                MainApproachDriver.AcknowledgeEnemyIntroduction();
                activity.Battle.Advance(1000); yield return null;
            }
            yield return null;
            Assert.That(activity.IsRescueComplete, Is.True);
            Assert.That(activity.Individual.Current.AnimalId, Is.EqualTo(initial.AnimalId));
            Assert.That(activity.Individual.Current.Coat, Is.SameAs(initial.Coat));
            MainApproachDriver.Click(siege, "Retry"); yield return null;
            Assert.That(motion.Surface.texture, Is.Null);
            Assert.That(motion.Player, Is.Null);
            Assert.That(root.Presenter.Session.Station.Animals.Single().AnimalAtArrival,
                Is.SameAs(activity.Individual.Current));
            MainApproachDriver.Click(station, "Enter"); yield return null; yield return null;
            Assert.That(station.Scene.GetComponentsInChildren<RawImage>().Any(raw =>
                raw.texture is RenderTexture), Is.True, "The same cat remains animated in the station.");
            LogAssert.NoUnexpectedReceived();
        }

        private static void SampleAt(MotionScope clock, FieldPetMotion motion, float seconds)
        {
            // Drive the shared presentation clock deterministically without adding a runtime debug API.
            typeof(MotionScope).GetProperty(nameof(MotionScope.SceneSeconds)).SetValue(clock, seconds);
            motion.SendMessage("LateUpdate");
        }

        private static void MeasureRendering(MotionScope clock, FieldPetMotion motion)
        {
            var setClock = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), clock,
                typeof(MotionScope).GetProperty(nameof(MotionScope.SceneSeconds)).GetSetMethod(true));
            var render = (Action)Delegate.CreateDelegate(typeof(Action), motion,
                typeof(FieldPetMotion).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic));
            for (int i = 0; i < 30; i++) { setClock(i * .05f); render(); }
            var timer = new System.Diagnostics.Stopwatch();
            long before = GC.GetAllocatedBytesForCurrentThread(); timer.Start();
            for (int i = 0; i < 120; i++) { setClock(i * .05f); render(); }
            timer.Stop(); long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            string folder = Path.GetFullPath("FieldPetMotionEvidence");
            File.WriteAllText(Path.Combine(folder, "render-cost.txt"),
                "Unity 2022.3.62f3 Windows Editor; warmed 30, measured 120 synchronous samples/renders; " +
                "managed bytes=" + allocated + "; CPU total ms=" + timer.Elapsed.TotalMilliseconds +
                "; RT=" + motion.Surface.texture.width + "x" + motion.Surface.texture.height +
                ". This is not full-frame or target-device profiling.");
        }

        private static IEnumerator CaptureCanvas(Canvas canvas, string name, Action<Color32[]> captured = null)
        {
            var camera = new GameObject("FieldMotionEvidence", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            var mode = canvas.renderMode; var previousCamera = canvas.worldCamera;
            var target = new RenderTexture(540, 960, 24);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = 10; camera.targetTexture = target;
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var pixels = Pixels(target, name);
            var animal = canvas.GetComponent<OutingFieldView>().Animal;
            var corners = new Vector3[4]; animal.rectTransform.GetWorldCorners(corners);
            if (captured != null)
            {
                var bottom = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var top = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                int left = Mathf.Clamp(Mathf.CeilToInt(bottom.x), 0, target.width - 1);
                int right = Mathf.Clamp(Mathf.FloorToInt(top.x), left + 1, target.width);
                int low = Mathf.Clamp(Mathf.CeilToInt(bottom.y), 0, target.height - 1);
                int high = Mathf.Clamp(Mathf.FloorToInt(top.y), low + 1, target.height);
                var crop = new Color32[(right - left) * (high - low)];
                for (int y = low; y < high; y++)
                    Array.Copy(pixels, y * target.width + left, crop, (y - low) * (right - left), right - left);
                captured(crop);
            }
            File.WriteAllLines(Path.Combine(Path.GetFullPath("FieldPetMotionEvidence"), name + "-position.txt"),
                corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point).ToString()));
            canvas.renderMode = mode; canvas.worldCamera = previousCamera;
            camera.targetTexture = null; target.Release(); Object.Destroy(target);
            Object.Destroy(camera.gameObject);
        }

        private static Color32[] Pixels(RenderTexture target, string name)
        {
            Assert.That(target, Is.Not.Null);
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            RenderTexture.active = previous;
            string folder = Path.GetFullPath("FieldPetMotionEvidence"); Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            var pixels = image.GetPixels32(); Object.Destroy(image); return pixels;
        }
    }
}
#endif
