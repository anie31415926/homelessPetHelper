#if UNITY_EDITOR
using System.Collections;
using System;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.Options;
using HomelessPetHelper.Modules.Rescue.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class OptionWhiteboardTests
    {
        private const string ScenePath = "Assets/EditorScenes/SC_OptionWhiteboard.unity";

        [UnityTest]
        public IEnumerator ChoosingRetainingUsingPausingAndRestartingWorkThroughSavedUI()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters());
            yield return null;
            OptionPanelView panel = UnityEngine.Object.FindObjectOfType<OptionPanelView>();
            Toggle pause = panel.GetComponentsInChildren<Toggle>().Single(toggle => toggle.name == "Pause");
            pause.isOn = false;
            Button use = ButtonNamed(panel, "Use");
            Assert.That(use.interactable, Is.False);
            OptionCardView[] cards = Cards(panel);
            Assert.That(cards.Length, Is.EqualTo(3));
            string keptIdentity = cards[0].GetComponentsInChildren<Text>().Single(text => text.name == "Identity").text;
            ToggleNamed(cards[0], "Selection").isOn = true;
            ToggleNamed(cards[0], "Retention").isOn = true;
            Assert.That(panel.SelectedIds.Count, Is.EqualTo(1));
            cards = Cards(panel);
            ToggleNamed(cards[1], "Selection").isOn = true;
            Assert.That(panel.SelectedIds.Count, Is.EqualTo(1));
            use.onClick.Invoke();
            Assert.That(panel.SelectedIds, Is.Empty);
            Assert.That(use.interactable, Is.False);
            Assert.That(Cards(panel).Any(card => card.GetComponentsInChildren<Text>()
                .Any(text => text.name == "Identity" && text.text == keptIdentity)), Is.True);

            cards = Cards(panel);
            ToggleNamed(cards[1], "Retention").isOn = true;
            cards = Cards(panel);
            Assert.That(ToggleNamed(cards[2], "Retention").interactable, Is.False);
            pause.isOn = true;
            Assert.That(Cards(panel).All(card => !ToggleNamed(card, "Selection").interactable), Is.True);
            pause.isOn = false;
            Assert.That(Cards(panel).All(card => ToggleNamed(card, "Selection").interactable), Is.True);
            ButtonNamed(panel, "Restart").onClick.Invoke();
            Assert.That(Cards(panel).All(card => !ToggleNamed(card, "Retention").isOn), Is.True);
            Assert.That(panel.SelectedIds, Is.Empty);
            Assert.That(Cards(panel).Length, Is.EqualTo(3));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator SavedPanelRendersAtPortraitAspectRatiosAndHasNoMissingBindings()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters());
            yield return null;
            var panel = UnityEngine.Object.FindObjectOfType<OptionPanelView>();
            Canvas canvas = panel.GetComponent<Canvas>();
            var cameraObject = new GameObject("PreviewCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var sizes = new[]
            {
                new Vector2Int(540, 960), new Vector2Int(390, 844), new Vector2Int(768, 1024),
                new Vector2Int(540, 1080), new Vector2Int(540, 1170), new Vector2Int(540, 1200)
            };
            foreach (Vector2Int size in sizes)
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (Button button in panel.GetComponentsInChildren<Button>())
                {
                    Assert.That(((RectTransform)button.transform).rect.height, Is.LessThan(160),
                        "A layout group expanded a command into unused screen space.");
                }

                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                int darkPixels = image.GetPixels32().Count(pixel => pixel.r < 150 && pixel.g < 150 && pixel.b < 150);
                Assert.That(darkPixels, Is.GreaterThan(size.x * size.y / 1000), "Blank UI render.");
                string folder = Path.GetFullPath("../../whiteboard-screenshots");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, $"whiteboard-{size.x}x{size.y}.png"), image.EncodeToPNG());
                UnityEngine.Object.Destroy(image);
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            UnityEngine.Object.Destroy(cameraObject);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator LargerOfferReusesCellsAndReopeningResetsSelectionAndListeners()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Panels/Panel_OptionWhiteboard.prefab");
            GameObject root = UnityEngine.Object.Instantiate(prefab);
            var panel = root.GetComponent<OptionPanelView>();
            var hand = new RescueOptionHand(Guid.NewGuid(), new RescueOptionRules(6, 1, 4));
            var definitions = Enumerable.Range(0, 6).Select(index =>
                new RescueActionDefinition("test." + index)).ToArray();
            Assert.That(hand.TryRefresh(new OptionTurnId(hand.CurrentTurn.SessionId, 1), definitions,
                new[] { "test.0" }, out _), Is.True);
            Font font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            string[] titles = definitions.Select(definition => definition.Id).ToArray();
            int uses = 0;
            panel.UseRequested += () => uses++;
            panel.Open(1, 4);
            for (int index = 0; index < 5; index++)
            {
                panel.Refresh(hand.Options, titles, font);
                panel.SetStatus("Turn 1", "0/4", "Ready", false);
            }

            Assert.That(root.GetComponentsInChildren<OptionCardView>(true).Length, Is.EqualTo(6));
            ToggleNamed(Cards(panel)[0], "Selection").isOn = true;
            panel.Close();
            Assert.That(root.activeSelf, Is.False);
            panel.Open(1, 4);
            panel.Refresh(hand.Options, titles, font);
            panel.SetStatus("Turn 1", "0/4", "Ready", false);
            Assert.That(panel.SelectedIds, Is.Empty);
            Assert.That(ButtonNamed(panel, "Use").interactable, Is.False);
            ToggleNamed(Cards(panel)[0], "Selection").isOn = true;
            ButtonNamed(panel, "Use").onClick.Invoke();
            Assert.That(uses, Is.EqualTo(1));
            panel.Close();
            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        private static Button ButtonNamed(OptionPanelView panel, string name)
        {
            return panel.GetComponentsInChildren<Button>().Single(button => button.name == name);
        }

        private static Toggle ToggleNamed(OptionCardView card, string name)
        {
            return card.GetComponentsInChildren<Toggle>().Single(toggle => toggle.name == name);
        }

        private static OptionCardView[] Cards(OptionPanelView panel)
        {
            return panel.GetComponentsInChildren<OptionCardView>();
        }
    }
}
#endif
