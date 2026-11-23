#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Station;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Tests.StationHabitat.PlayMode
{
    public sealed class StationUiRefinementTests
    {
        private StationPanelView _view;
        private Camera _camera;
        private MainSceneStorageScope _scope;
        private MemorySaveStorage _progress, _audio;
        [TearDown] public void TearDown()
        {
            _scope?.Dispose(); _scope = null;
            if (_camera != null) Object.DestroyImmediate(_camera.gameObject);
        }
        private IEnumerator LoadMain()
        {
            _progress = new MemorySaveStorage(); _audio = new MemorySaveStorage();
            _scope = new MainSceneStorageScope(() => _progress, createAudioStorage: () => _audio);
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            _view = Object.FindObjectOfType<StationPanelView>();
        }
        private IEnumerator LoadPreview()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/EditorScenes/StationHabitat/SC_StationScene.unity", new LoadSceneParameters());
            yield return null; yield return null;
            _view = Object.FindObjectOfType<StationPanelView>();
        }
        private Button Button(string name) => _view.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        private void Click(string name)
        {
            var button = Button(name); Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }
        private static T Read<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static StationPreviewSession Preview()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a =>
                a.GetType("HomelessPetHelper.Editor.StationHabitat.StationScenePreview")).Single(t => t != null);
            return (StationPreviewSession)type.GetField("_presenter", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
        }

        [UnityTest] public IEnumerator DisabledPaperStaysLightAndRestoresSurfaceWithoutChangingHitbox()
        {
            yield return LoadPreview();
            var button = Button("StationMenu");
            var surface = (Image)button.targetGraphic;
            var original = surface.sprite;
            var rect = (RectTransform)button.transform;
            var size = rect.rect.size;
            button.interactable = false;
            yield return null;
            Assert.That(surface.sprite.name, Is.EqualTo("Paper"));
            Assert.That(surface.color.r, Is.GreaterThan(.9f));
            Assert.That(surface.canvasRenderer.GetColor().a, Is.EqualTo(1));
            Assert.That(rect.rect.size, Is.EqualTo(size));
            Assert.That(button.GetComponentInChildren<Text>().color.g, Is.LessThan(.3f));
            button.interactable = true;
            yield return null;
            Assert.That(surface.sprite, Is.SameAs(original));
            Assert.That(surface.color, Is.EqualTo(Color.white));
            var group = button.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            yield return null;
            Assert.That(button.IsInteractable(), Is.False);
            Assert.That(surface.sprite.name, Is.EqualTo("Paper"));
            group.interactable = true;
            yield return null;
            Assert.That(surface.sprite, Is.SameAs(original));
        }

        [UnityTest] public IEnumerator IndependentCareReasonsRemainAfterToastAndClearWhenPermissionsRecover()
        {
            yield return LoadPreview();
            var presenter = Preview();
            var station = Read<RescueStation>(presenter, "_station");
            var animal = (AnimalIndividual)typeof(StationAnimalIntake).GetProperty("Individual",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(station.Animals[0]);
            Assert.That(animal.TryApplyCarePermissions(animal.Current,
                new AnimalCarePermissions(true, false, true)), Is.True);
            _view.Scene.Cells[0].SelectButton.onClick.Invoke();
            _view.GetComponentInChildren<ToastView>().Clear();
            yield return null;
            var reason = Read<Text>(_view, "_careAvailability");
            Assert.That(Button("Clean").IsInteractable(), Is.False);
            Assert.That(Button("Groom").IsInteractable(), Is.True);
            Assert.That(reason.isActiveAndEnabled, Is.True);
            Assert.That(reason.text, Does.Contain("清洁护理：").And.Contain("不接受清洗"));
            Assert.That(((Image)Button("Clean").targetGraphic).sprite.name, Is.EqualTo("Paper"));
            yield return Capture("care-permissions");
            Click("Back");
            Assert.That(animal.TryApplyCarePermissions(animal.Current,
                new AnimalCarePermissions(true, true, true)), Is.True);
            _view.Scene.Cells[0].SelectButton.onClick.Invoke();
            Assert.That(Button("Clean").IsInteractable(), Is.True);
            Assert.That(reason.gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator StationSettingsReusesDraftConfirmationAndKeepsTheUsersPause()
        {
            yield return LoadMain();
            var main = Object.FindObjectOfType<MainGameRoot>();
            Click("StationMenu");
            var pause = Read<Toggle>(_view, "_pause");
            pause.isOn = true;
            Click("StationSettings");
            var overlays = main.Presenter.Overlays;
            Assert.That(overlays.Depth, Is.EqualTo(1));
            Assert.That(overlays.Top, Is.EqualTo(LevelPopupKind.Settings));
            Assert.That(Button("DismissAnimal").gameObject.activeSelf, Is.False, "One fullscreen backdrop at a time.");
            var music = _view.GetComponentsInChildren<Slider>().Single(s => s.name == "MusicVolume");
            music.value = .35f;
            Click("OverlayResetAudio");
            Assert.That(overlays.Depth, Is.EqualTo(2));
            yield return Capture("station-reset-confirm");
            Click("OverlayCancel");
            Assert.That(overlays.Top, Is.EqualTo(LevelPopupKind.Settings));
            Click("OverlayResetAudio"); Click("OverlayConfirm");
            Assert.That(main.Audio.Playback.Preferences.Music.Volume, Is.EqualTo(1));
            Click("OverlayCancelSettings");
            Assert.That(main.Audio.Playback.Preferences.Music.Volume, Is.EqualTo(1));
            Assert.That(_view.GetComponent<StationSceneChrome>().MenuIsOpen, Is.True);
            Assert.That(pause.isOn && main.Presenter.Session.Flow.IsSuspended, Is.True);
            Click("StationSettings");
            music.value = .35f;
            yield return Capture("station-settings");
            _audio.FailWrites = true;
            Click("OverlaySaveSettings");
            Assert.That(overlays.Top, Is.EqualTo(LevelPopupKind.Settings));
            Assert.That(Read<Text>(_view.GetComponentInChildren<LevelOverlayView>(), "_settingsStatus").text,
                Does.Contain("失败"));
            _audio.FailWrites = false;
            Click("OverlaySaveSettings");
            Assert.That(overlays.Depth, Is.Zero);
            Assert.That(_audio.Writes, Is.GreaterThan(0));
            Assert.That(pause.isOn && main.Presenter.Session.Flow.IsSuspended, Is.True);
            pause.isOn = false;
            Assert.That(main.Presenter.Session.Flow.IsSuspended, Is.False);
            Click("StationSettings");
            _view.SendMessage("OnApplicationFocus", false);
            _view.SendMessage("OnApplicationFocus", true);
            Assert.That(overlays.Top, Is.EqualTo(LevelPopupKind.Settings));
            Click("OverlayCancelSettings");
            Assert.That(main.Presenter.Session.Flow.IsSuspended, Is.False);
            Click("CloseStationMenu"); Click("Restart");
            Assert.That(main.Presenter.Overlays, Is.Null, "Leaving station releases its shared overlay owner.");
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator SaveFailureIsVisibleAndBlocksInputWhilePausedWithoutOverwriting()
        {
            yield return LoadMain();
            _progress.FailWrites = true;
            Click("Restart");
            yield return AssertBlockingError("保存失败");
            Assert.That(_progress.Writes, Is.Zero);
            Assert.That(_progress.Slots, Is.All.Null);
            yield return Capture("save-error");
        }

        [UnityTest] public IEnumerator LoadFailurePreservesOriginalAndShowsAReadablePersistentError()
        {
            _progress = new MemorySaveStorage { FailReads = true };
            _progress.Slots[0] = "preserved unreadable bytes";
            _scope = new MainSceneStorageScope(() => _progress);
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            _view = Object.FindObjectOfType<StationPanelView>();
            yield return AssertBlockingError("读取失败");
            Assert.That(_progress.Slots[0], Is.EqualTo("preserved unreadable bytes"));
            Assert.That(_progress.Writes, Is.Zero);
            yield return Capture("load-error");
        }

        private IEnumerator AssertBlockingError(string expected)
        {
            for (int i = 0; i < 4; i++) { yield return null; Canvas.ForceUpdateCanvases(); }
            var chrome = _view.GetComponent<StationSceneChrome>();
            var error = Read<GameObject>(chrome, "_blockingError");
            var message = Read<Text>(chrome, "_errorMessage");
            Assert.That(error.activeInHierarchy, Is.True);
            Assert.That(message.text, Does.Contain(expected).And.Contain("退出"));
            Assert.That(message.canvasRenderer.GetAlpha(), Is.EqualTo(1));
            Assert.That(message.GetComponentsInParent<CanvasGroup>().All(g => g.alpha > .9f), Is.True);
            Assert.That(message.rectTransform.rect.height, Is.GreaterThanOrEqualTo(message.preferredHeight - 1));
            Assert.That(Button("StationMenu").IsInteractable(), Is.False);
            Assert.That(Button("Restart").IsInteractable(), Is.False);
            Assert.That(Button("DismissAnimal").gameObject.activeInHierarchy, Is.True);
            Assert.That(Button("DismissAnimal").IsInteractable(), Is.False);
        }

        private IEnumerator Capture(string state)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            if (_camera == null) _camera = new GameObject("Station refinement evidence", typeof(Camera))
                .GetComponent<Camera>();
            var canvas = _view.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _camera; canvas.planeDistance = 10;
            foreach (var size in new[]
                { new Vector2Int(320, 568), new Vector2Int(390, 844), new Vector2Int(768, 1024) })
            {
                var target = new RenderTexture(size.x, size.y, 24); _camera.targetTexture = target;
                for (int i = 0; i < 4; i++) { yield return null; Canvas.ForceUpdateCanvases(); }
                _camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply(); RenderTexture.active = previous;
                Directory.CreateDirectory("StationRefinementEvidence");
                File.WriteAllBytes("StationRefinementEvidence/" + state + "-" + size.x + "x" + size.y + ".png",
                    image.EncodeToPNG());
                Object.Destroy(image); _camera.targetTexture = null; target.Release(); Object.Destroy(target);
            }
        }
    }
}
#endif
