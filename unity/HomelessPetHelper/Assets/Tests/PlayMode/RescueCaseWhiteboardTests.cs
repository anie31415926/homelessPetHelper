#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class RescueCaseWhiteboardTests
    {
        [Serializable]
        private sealed class TextTable
        {
            public Entry[] entries = Array.Empty<Entry>();
        }

        [Serializable]
        private sealed class Entry
        {
            public string key = string.Empty;
            public string value = string.Empty;
        }

        private const string ScenePath = "Assets/EditorScenes/SC_RescueCase.unity";
        private RescueCaseView _view;
        private TextTable _text;

        [UnityTest]
        public IEnumerator FullRescueUsesActualSavedControlsAndSummaryBeforeInteraction()
        {
            yield return Load();
            string initialTurn = Label("Turn").text;
            Button("Handbook").onClick.Invoke();
            Assert.That(Label("Details").text, Is.EqualTo(Text("handbook.body")));
            Assert.That(Label("Turn").text, Is.EqualTo(initialTurn));
            Button("Handbook").onClick.Invoke();
            Assert.That(Label("Details", true).gameObject.activeSelf, Is.False);
            Toggle pause = _view.GetComponentsInChildren<Toggle>().Single(item => item.name == "Pause");
            pause.isOn = true;
            Assert.That(Button("Assistance").interactable, Is.False);
            pause.isOn = false;
            Use("give-space");
            Use("food");
            Use("wait");
            Use(Cards().Any(card => card.GetComponentsInChildren<Text>()
                .Any(label => label.name == "Title" && label.text == Text("toy"))) ? "toy" : "wait");
            Use("wait");
            Assert.That(Button("Advance").GetComponentInChildren<Text>().text,
                Is.EqualTo(Text("advance.Approachable")));
            Button("Advance").onClick.Invoke();
            Assert.That(Label("Feedback").text, Is.EqualTo(Text("response.InspectingPlacement")));
            Assert.That(Button("Advance").GetComponentInChildren<Text>().text,
                Is.EqualTo(Text("advance.Responding")));
            Button("Advance").onClick.Invoke();
            Use("wait");
            Assert.That(Button("Advance").GetComponentInChildren<Text>().text,
                Is.EqualTo(Text("advance.ReadyForRescue")));
            Assert.That(_view.Options.GetComponentsInChildren<OptionCardView>(), Is.Empty);
            Button("Advance").onClick.Invoke();
            Assert.That(Label("Details").text, Is.EqualTo(Text("details.Summary")));
            Assert.That(Button("Advance").GetComponentInChildren<Text>().text,
                Is.EqualTo(Text("advance.Summary")));
            Button("Advance").onClick.Invoke();
            Assert.That(Button("Advance").GetComponentInChildren<Text>().text,
                Is.EqualTo(Text("advance.CheckAtStation")));
            Assert.That(Label("Details").text, Is.EqualTo(Text("station.unchecked")));
            Button("Advance").onClick.Invoke();
            Assert.That(Label("Feedback").text, Is.EqualTo(Text("response.CheckedAtStation")));
            Assert.That(Label("Details").text, Is.EqualTo(Text("station.ready")));
            pause.isOn = true;
            Assert.That(Button("Advance").interactable, Is.False);
            Assert.That(Label("Details").text, Is.EqualTo(Text("station.ready")));
            pause.isOn = false;
            Assert.That(Label("Details").text, Is.EqualTo(Text("station.ready")));
            Assert.That(Button("Advance").GetComponentInChildren<Text>().text,
                Is.EqualTo(Text("advance.Station")));
            Button("Advance").onClick.Invoke();
            Assert.That(Button("Advance").interactable, Is.False);
            Assert.That(Label("Feedback").text, Is.EqualTo(Text("response.Settled")));
            Button("Restart").onClick.Invoke();
            Assert.That(_view.Options.GetComponentsInChildren<OptionCardView>().Length, Is.EqualTo(3));
            Assert.That(_view.Options.SelectedIds, Is.Empty);
            Assert.That(Button("Assistance").interactable, Is.True);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator AssistanceEndsTrialAndRestartDoesNotDuplicateListeners()
        {
            yield return Load();
            for (int index = 0; index < 3; index++)
            {
                Button("Assistance").onClick.Invoke();
                Assert.That(Label("Feedback").text, Is.EqualTo(Text("response.AssistanceRequested")));
                Assert.That(Button("Assistance").interactable, Is.False);
                Assert.That(_view.Options.GetComponentsInChildren<OptionCardView>(), Is.Empty);
                Button("Restart").onClick.Invoke();
                Assert.That(_view.Options.GetComponentsInChildren<OptionCardView>().Length, Is.EqualTo(3));
            }

            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator StressPresetRecoversWithoutRefeedingAndMenuRestartsOnlyWhenRequested()
        {
            yield return Load();
            Assert.That(EditorApplication.ExecuteMenuItem(
                "Homeless Pet Helper/Preview/Open Stress Recovery Case"), Is.True);
            Assert.That(Label("Animal").text, Does.Contain(Text("fed")));
            Assert.That(Label("Animal").text, Does.Contain(Text("stress.Acute")));
            Button("Handbook").onClick.Invoke();
            Assert.That(Label("Details").text, Is.EqualTo(Text("handbook.body")));
            Button("Handbook").onClick.Invoke();
            Use("wait");
            Assert.That(Label("Animal").text, Does.Contain(Text("stress.Acute")));
            Use("give-space");
            Use("wait");
            Assert.That(Label("Animal").text, Does.Contain(Text("fed")));
            Assert.That(Label("Animal").text, Does.Contain(Text("stress.None")));
            Assert.That(Label("Turn").text, Is.EqualTo(Text("turn", 4, 2)));
            Use("wait");
            Use("wait");
            Assert.That(Label("Section").text, Does.Contain(Text("stage.Approachable")));
            Button("Advance").onClick.Invoke();
            Assert.That(Label("Feedback").text, Is.EqualTo(Text("response.InspectingPlacement")));
            Button("Advance").onClick.Invoke();
            Use("give-space");
            Button("Advance").onClick.Invoke();
            Button("Advance").onClick.Invoke();
            Assert.That(Label("Animal").text, Does.Contain(Text("fed")));
            Assert.That(Label("Animal").text, Does.Contain(Text("stress.None")));
            Button("Restart").onClick.Invoke();
            Assert.That(Label("Animal").text, Does.Contain(Text("stress.Acute")));
            Assert.That(EditorApplication.ExecuteMenuItem("Homeless Pet Helper/Preview/Open Rescue Case"), Is.True);
            Assert.That(Label("Animal").text, Does.Contain(Text("hungry")));
            Assert.That(Label("Animal").text, Does.Contain(Text("stress.None")));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator UnavailableLockedActionCannotBeSelectedButCanBeUnlockedWithoutReroll()
        {
            yield return Load();
            ReachInspectionWithLockedApproach();
            OptionCardView card = Card("approach");
            Assert.That(CardToggle(card, "Selection").interactable, Is.False);
            Assert.That(CardToggle(card, "Retention").isOn, Is.True);
            Assert.That(CardToggle(card, "Retention").interactable, Is.True);
            Assert.That(card.GetComponentsInChildren<Text>().Single(label => label.name == "Identity").text,
                Is.EqualTo(Text("unavailable.AlreadyWithinReach")));
            CardToggle(card, "Selection").isOn = true;
            Assert.That(_view.Options.SelectedIds, Is.Empty);
            Assert.That(Button("Use").interactable, Is.False);
            string turn = Label("Turn").text;
            var pause = _view.GetComponentsInChildren<Toggle>().Single(toggle => toggle.name == "Pause");
            pause.isOn = true;
            Assert.That(CardToggle(Card("approach"), "Retention").interactable, Is.False);
            pause.isOn = false;
            CardToggle(Card("approach"), "Retention").isOn = false;
            Assert.That(CardToggle(Card("approach"), "Retention").isOn, Is.False);
            Assert.That(Label("Turn").text, Is.EqualTo(turn));
            Use("toy");
            Assert.That(Cards().Any(item => item.GetComponentsInChildren<Text>()
                .Any(label => label.name == "Title" && label.text == Text("approach"))), Is.False);
            Assert.That(Label("Animal").text, Does.Contain(Text("behavior.Playing")));
            Assert.That(Button("Advance", true).gameObject.activeSelf, Is.False);
            Use("wait");
            Assert.That(Label("Feedback").text, Is.EqualTo(Text("response.AcceptedPreparation")));
            Assert.That(Label("Animal").text, Does.Contain(Text("behavior.SettlingInPlacement")));
            Assert.That(Button("Advance").GetComponentInChildren<Text>().text,
                Is.EqualTo(Text("advance.ReadyForRescue")));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator TrialRendersAtPortraitSizesWithVisibleCommandsAndReadableHandbook()
        {
            yield return Load();
            Canvas canvas = _view.GetComponent<Canvas>();
            var cameraObject = new GameObject("CaseCaptureCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.orthographic = true;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var sizes = new[]
            {
                new Vector2Int(540, 960), new Vector2Int(390, 844), new Vector2Int(768, 1024),
                new Vector2Int(540, 1080), new Vector2Int(540, 1170), new Vector2Int(540, 1200)
            };
            string folder = Path.GetFullPath("../../rescue-case-screenshots");
            Directory.CreateDirectory(folder);
            foreach (Vector2Int size in sizes)
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertControlsInBounds(canvas);
                Capture(camera, target, Path.Combine(folder, $"case-{size.x}x{size.y}.png"));
                Find("phone.reply");
                Canvas.ForceUpdateCanvases();
                AssertControlsInBounds(canvas);
                AssertLabelsFit();
                Capture(camera, target, Path.Combine(folder, $"flavor-card-{size.x}x{size.y}.png"));
                Use("phone.reply");
                Canvas.ForceUpdateCanvases();
                AssertLabelsFit();
                Capture(camera, target, Path.Combine(folder, $"flavor-feedback-{size.x}x{size.y}.png"));
                Use("give-space");
                Use("give-space");
                Assert.That(Label("Feedback").text, Is.EqualTo(Text("response.SpaceMaintained")));
                Canvas.ForceUpdateCanvases();
                AssertControlsInBounds(canvas);
                AssertLabelsFit();
                Capture(camera, target, Path.Combine(folder, $"maintain-space-{size.x}x{size.y}.png"));
                Button("Handbook").onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                AssertControlsInBounds(canvas);
                Capture(camera, target, Path.Combine(folder, $"handbook-{size.x}x{size.y}.png"));
                Button("Handbook").onClick.Invoke();
                Assert.That(EditorApplication.ExecuteMenuItem(
                    "Homeless Pet Helper/Preview/Open Stress Recovery Case"), Is.True);
                Canvas.ForceUpdateCanvases();
                AssertControlsInBounds(canvas);
                Capture(camera, target, Path.Combine(folder, $"stress-{size.x}x{size.y}.png"));
                ReachInspectionWithLockedApproach();
                Canvas.ForceUpdateCanvases();
                AssertControlsInBounds(canvas);
                AssertLabelsFit();
                Capture(camera, target, Path.Combine(folder, $"inspection-{size.x}x{size.y}.png"));
                Use("wait");
                Button("Advance").onClick.Invoke();
                Button("Advance").onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                AssertLabelsFit();
                Capture(camera, target, Path.Combine(folder, $"station-unchecked-{size.x}x{size.y}.png"));
                Button("Advance").onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                AssertControlsInBounds(canvas);
                AssertLabelsFit();
                Assert.That(Label("Details").text, Is.EqualTo(Text("station.ready")));
                Capture(camera, target, Path.Combine(folder, $"station-checked-{size.x}x{size.y}.png"));
                Assert.That(EditorApplication.ExecuteMenuItem("Homeless Pet Helper/Preview/Open Rescue Case"), Is.True);
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }

            var stageTarget = new RenderTexture(540, 960, 24);
            camera.targetTexture = stageTarget;
            Use("give-space");
            Use("food");
            Use("wait");
            Use(Cards().Any(card => card.GetComponentsInChildren<Text>()
                .Any(label => label.name == "Title" && label.text == Text("toy"))) ? "toy" : "wait");
            Use("wait");
            Canvas.ForceUpdateCanvases();
            Capture(camera, stageTarget, Path.Combine(folder, "case-approachable.png"));
            Button("Advance").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Capture(camera, stageTarget, Path.Combine(folder, "case-inspecting.png"));
            Button("Advance").onClick.Invoke();
            Use("wait");
            Canvas.ForceUpdateCanvases();
            Capture(camera, stageTarget, Path.Combine(folder, "case-accepted.png"));
            Button("Advance").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            AssertControlsInBounds(canvas);
            Capture(camera, stageTarget, Path.Combine(folder, "case-summary.png"));
            Button("Advance").onClick.Invoke();
            Button("Advance").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            AssertControlsInBounds(canvas);
            Capture(camera, stageTarget, Path.Combine(folder, "case-station.png"));
            camera.targetTexture = null;
            stageTarget.Release();
            UnityEngine.Object.Destroy(stageTarget);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            UnityEngine.Object.Destroy(cameraObject);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        private IEnumerator Load()
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/EditorScenes/RescueCaseText.json");
            _text = JsonUtility.FromJson<TextTable>(source.text);
            var options = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Rescue/RescueOptionText.zh-CN.json");
            _text.entries = _text.entries.Concat(JsonUtility.FromJson<TextTable>(options.text).entries).ToArray();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters());
            yield return null;
            _view = UnityEngine.Object.FindObjectOfType<RescueCaseView>();
            Assert.That(EditorApplication.ExecuteMenuItem("Homeless Pet Helper/Preview/Open Rescue Case"), Is.True);
            _view.GetComponentsInChildren<Toggle>().Single(item => item.name == "Pause").isOn = false;
        }

        [UnityTest]
        public IEnumerator FlavorUsesSavedControlsAndFeedbackSurvivesRedrawWithoutRepeatingTheAction()
        {
            yield return Load();
            foreach (string action in new[] { "phone.reply", "phone.photo" })
            {
                Button("Restart").onClick.Invoke();
                Find(action);
                string animal = Label("Animal").text;
                string turn = Label("Turn").text;
                OptionCardView card = Card(action);
                CardToggle(card, "Retention").isOn = true;
                Button("Handbook").onClick.Invoke();
                Button("Handbook").onClick.Invoke();
                Assert.That(CardToggle(Card(action), "Retention").isOn, Is.True);
                Assert.That(Label("Turn").text, Is.EqualTo(turn));
                CardToggle(Card(action), "Selection").isOn = true;
                Button("Use").onClick.Invoke();
                string feedback = Text("option." + action + ".feedback");
                Assert.That(Label("Feedback").text, Is.EqualTo(feedback));
                Assert.That(Label("Animal").text, Is.EqualTo(animal));
                Assert.That(Button("Use", true).interactable, Is.False);
                var pause = _view.GetComponentsInChildren<Toggle>().Single(item => item.name == "Pause");
                pause.isOn = true;
                pause.isOn = false;
                Button("Handbook").onClick.Invoke();
                Button("Handbook").onClick.Invoke();
                Assert.That(Label("Feedback").text, Is.EqualTo(feedback));
                Assert.That(Label("Animal").text, Is.EqualTo(animal));
                Button("Advance").onClick.Invoke();
                Assert.That(Label("Feedback").text, Is.EqualTo(feedback));
                for (int index = 0; index < 12; index++)
                {
                    Assert.That(Cards().Any(item => item.GetComponentsInChildren<Text>().Any(label =>
                        label.name == "Title" && MatchesAction(label.text, action))), Is.False);
                    Use("give-space");
                }
            }

            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator ExistingSpaceChangesActionTitleAndResponseWithoutClaimingAnotherRetreat()
        {
            yield return Load();
            Use("give-space");
            Assert.That(Label("Feedback").text, Is.EqualTo(Text("response.SpaceGiven")));
            Find("give-space");
            Assert.That(Card("give-space").GetComponentsInChildren<Text>()
                .Single(label => label.name == "Title").text, Is.EqualTo(Text("maintain-space")));
            Use("give-space");
            Assert.That(Label("Feedback").text, Is.EqualTo(Text("response.SpaceMaintained")));
            Canvas.ForceUpdateCanvases();
            AssertLabelsFit();
            Use("food");
            Use("wait");
            Use("toy");
            Use("approach");
            Assert.That(Label("Animal").text, Does.Contain(Text("behavior.PermittingApproach")));
            Assert.That(Label("Animal").text, Does.Not.Contain(Text("behavior.Approaching")));
            Assert.That(Label("Feedback").text, Is.EqualTo(Text("response.Accepted")));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        private void Use(string action)
        {
            Find(action);
            var card = Card(action);
            card.GetComponentsInChildren<Toggle>().Single(toggle => toggle.name == "Selection").isOn = true;
            Assert.That(Button("Use").interactable, Is.True);
            Button("Use").onClick.Invoke();
            Assert.That(Button("Use", true).interactable, Is.False);
            if (Button("Advance").GetComponentInChildren<Text>().text == Text("advance.Responding"))
            {
                Button("Advance").onClick.Invoke();
            }
        }

        private void ReachInspectionWithLockedApproach()
        {
            Assert.That(EditorApplication.ExecuteMenuItem(
                "Homeless Pet Helper/Preview/Open Stress Recovery Case"), Is.True);
            Find("approach");
            CardToggle(Card("approach"), "Retention").isOn = true;
            Use("give-space");
            for (int index = 0; index < 6 && !Label("Section").text.Contains(Text("stage.Approachable")); index++)
            {
                Use("wait");
            }

            Assert.That(Label("Section").text, Does.Contain(Text("stage.Approachable")));
            Button("Advance").onClick.Invoke();
            Assert.That(Label("Feedback").text, Is.EqualTo(Text("response.InspectingPlacement")));
            Button("Advance").onClick.Invoke();
            Assert.That(CardToggle(Card("approach"), "Selection").interactable, Is.False);
        }

        private OptionCardView Card(string action) => Cards().Single(item => item.GetComponentsInChildren<Text>()
            .Any(label => label.name == "Title" && MatchesAction(label.text, action)));

        private bool MatchesAction(string title, string action) => title == Text(
            action.StartsWith("phone.", StringComparison.Ordinal) ? "option." + action + ".title" : action) ||
            (action == "give-space" && title == Text("maintain-space"));

        private static Toggle CardToggle(OptionCardView card, string name) =>
            card.GetComponentsInChildren<Toggle>().Single(toggle => toggle.name == name);

        private void Find(string action)
        {
            for (int index = 0; index < 100; index++)
            {
                if (Cards().Any(card => card.GetComponentsInChildren<Text>()
                    .Any(label => label.name == "Title" && MatchesAction(label.text, action))))
                {
                    return;
                }

                bool inspecting = Label("Animal").text.Contains(Text("behavior.InspectingPlacement"));
                string[] fillers = inspecting ?
                    new[] { "toy", "phone.reply", "phone.photo", "give-space", "wait" } :
                    new[] { "give-space", "toy", "phone.reply", "phone.photo", "approach", "wait" };
                var available = fillers
                    .Select(id => Cards().FirstOrDefault(card => card.GetComponentsInChildren<Text>()
                        .Any(label => label.name == "Title" && MatchesAction(label.text, id))))
                    .First(card => card != null && CardToggle(card, "Selection").interactable);
                available.GetComponentsInChildren<Toggle>().Single(toggle => toggle.name == "Selection").isOn = true;
                Button("Use").onClick.Invoke();
                Button("Advance").onClick.Invoke();
            }

            Assert.Fail("Missing offered action: " + action);
        }

        private void AssertControlsInBounds(Canvas canvas)
        {
            Rect bounds = ((RectTransform)canvas.transform).rect;
            var corners = new Vector3[4];
            foreach (Button button in _view.GetComponentsInChildren<Button>())
            {
                var rect = (RectTransform)button.transform;
                rect.GetWorldCorners(corners);
                Assert.That(rect.rect.height, Is.GreaterThan(40), button.name);
                Assert.That(rect.rect.height, Is.LessThan(160), button.name);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = canvas.transform.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(bounds.xMin - 1, bounds.xMax + 1), button.name);
                    Assert.That(local.y, Is.InRange(bounds.yMin - 1, bounds.yMax + 1), button.name);
                }
            }
        }

        private static void Capture(Camera camera, RenderTexture target, string path)
        {
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            int dark = image.GetPixels32().Count(pixel => pixel.r < 150 && pixel.g < 150 && pixel.b < 150);
            Assert.That(dark, Is.GreaterThan(target.width * target.height / 1000), "Blank trial render.");
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
        }

        private void AssertLabelsFit()
        {
            foreach (Text label in _view.GetComponentsInChildren<Text>())
            {
                if (string.IsNullOrEmpty(label.text))
                {
                    continue;
                }

                var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                settings.fontSize = label.cachedTextGenerator.fontSizeUsedForBestFit;
                settings.resizeTextForBestFit = false;
                var generator = new TextGenerator();
                float height = generator.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
                Assert.That(height, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), label.name);
            }
        }

        private string Text(string key, params object[] args) =>
            string.Format(_text.entries.Single(item => item.key == key).value, args);
        private OptionCardView[] Cards() => _view.Options.GetComponentsInChildren<OptionCardView>();
        private Button Button(string name, bool inactive = false) => _view.GetComponentsInChildren<Button>(inactive)
            .Single(button => button.name == name);
        private Text Label(string name, bool inactive = false) => _view.GetComponentsInChildren<Text>(inactive)
            .Single(label => label.name == name);
    }
}
#endif
