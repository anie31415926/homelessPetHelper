using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.Station;
using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using HomelessPetHelper.Editor.CityMap;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Modules.Outing.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class CityMapExpansionTests
    {
        [Serializable] private sealed class Config
        {
            public int schemaVersion = 1;
            public string backgroundId = "neighborhood";
            public string[] regionIds = { "area" };
            public string initialPointId = "site-0";
            public int initialCapacity = 8;
            public int minutesPerDay = 600;
            public int seed = 19;
            public Timing timePolicy = new Timing();
            public Point[] points = Array.Empty<Point>();
        }
        [Serializable] private sealed class Timing
        {
            public long explorationMinutes = 7;
            public long openingMinutes = 2;
            public long negotiationMinutes = 3;
            public long handoverMinutes = 4;
        }
        [Serializable] private sealed class Point
        {
            public string id = string.Empty;
            public string nameKey = string.Empty;
            public string detailKey = "yard.detail";
            public string regionId = "area";
            public bool openByDefault = true;
            public string[] sceneTemplateIds = { "scene" };
        }
        [Serializable] private sealed class TextTable { public Entry[] entries = Array.Empty<Entry>(); }
        [Serializable] private sealed class Entry
        {
            public string key = string.Empty;
            public string value = string.Empty;
        }

        [UnityTest]
        public IEnumerator VariableCountsReorderedBindingsRenamedNodesAndDisabledPointsUseStableIds()
        {
            yield return new EnterPlayMode();
            foreach (int count in new[] { 1, 4, 7 })
            {
                using (var fixture = new Fixture(count))
                {
                    int events = 0;
                    string lastId = null;
                    fixture.View.PlaceSelected += id => { events++; lastId = id; };
                    foreach (int index in Enumerable.Range(0, count))
                    {
                        Button button = fixture.PointButtons[index];
                        Assert.That(button.GetComponentInChildren<Text>().text, Is.EqualTo("Place " + index));
                        if (count > 1 && index == count - 1)
                        {
                            Assert.That(button.interactable, Is.False);
                            int before = events;
                            button.onClick.Invoke();
                            Assert.That(events, Is.EqualTo(before));
                            continue;
                        }
                        button.onClick.Invoke();
                        Assert.That(lastId, Is.EqualTo("site-" + index));
                        Assert.That(fixture.Label("Heading").text, Is.EqualTo("Place " + index));
                    }
                    int afterSelection = events;
                    fixture.Root.SetActive(false);
                    fixture.PointButtons[0].onClick.Invoke();
                    Assert.That(events, Is.EqualTo(afterSelection));
                    for (int repeat = 0; repeat < 3; repeat++)
                    {
                        fixture.Root.SetActive(true);
                        fixture.Root.SetActive(false);
                    }
                    fixture.Root.SetActive(true);
                    fixture.PointButtons[0].onClick.Invoke();
                    Assert.That(events, Is.EqualTo(afterSelection + 1), "Listeners must not multiply on reopen.");
                    fixture.Button("Primary").onClick.Invoke();
                    Assert.That(fixture.Label("Heading").text, Does.Contain("Place 0"));
                    Assert.That(fixture.PointButtons.All(button => !button.interactable), Is.True);
                    fixture.Button("Primary").onClick.Invoke();
                    Assert.That(fixture.Label("Status").text, Does.Contain("7"));
                    Assert.That(fixture.Label("Details").text, Does.Contain("7"));
                    fixture.Button("Primary").onClick.Invoke();
                    Assert.That(fixture.Label("Heading").text, Is.EqualTo("Place 0"));
                }
            }
        }

        [UnityTest]
        public IEnumerator ConfigMismatchAndCorruptBindingsFailBeforeListenerRegistration()
        {
            yield return new EnterPlayMode();
            using (var fixture = new Fixture(4, attachPresenter: false))
            {
                Assert.Throws<InvalidOperationException>(() => fixture.View.ValidateDefinition(
                    "wrong", Enumerable.Range(0, 4).Select(index => "site-" + index)));
                Assert.Throws<InvalidOperationException>(() => fixture.View.ValidateDefinition(
                    "neighborhood", new[] { "site-0" }));
                var wrong = new TextAsset(JsonUtility.ToJson(CreateConfig(1)));
                try
                {
                    Assert.Throws<InvalidOperationException>(() => new CityMapPresenter(
                        fixture.View, fixture.Text, new CityMapPreviewConfig(wrong)));
                }
                finally { UnityEngine.Object.DestroyImmediate(wrong); }
            }
            foreach (string error in new[] { "duplicate-id", "duplicate-button", "missing-label" })
            {
                string message = error == "missing-label" ? "Incomplete city map point binding" :
                    "Duplicate city map point ID or button";
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: " + message));
                using (var fixture = new Fixture(4, attachPresenter: false, corrupt: error))
                {
                    int count = 0;
                    fixture.View.PlaceSelected += _ => count++;
                    foreach (Button button in fixture.PointButtons) button.onClick.Invoke();
                    Assert.That(count, Is.Zero, "Failed initialization must not leave partial listeners.");
                }
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SavedMapUsesConfiguredCapacityTimingAndRequestTextInsteadOfPointNameConventions()
        {
            yield return new EnterPlayMode();
            var staging = new GameObject("SavedMapConfigTestStaging");
            staging.SetActive(false);
            GameObject root = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(CityMapAssets.PanelPath), staging.transform);
            var source = new TextAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(CityMapAssets.ConfigPath).text
                .Replace("\"initialCapacity\": 2", "\"initialCapacity\": 0")
                .Replace("\"explorationMinutes\": 5", "\"explorationMinutes\": 9")
                .Replace("\"textKey\": \"parking.detail\"", "\"textKey\": \"yard.detail\""));
            TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(CityMapAssets.TextPath);
            var copy = new OptionPreviewText(text);
            staging.SetActive(true);
            try
            {
                using (var presenter = new CityMapPresenter(root.GetComponent<CityMapView>(), text,
                    new CityMapPreviewConfig(source)))
                {
                    Text details = root.GetComponentsInChildren<Text>(true).Single(item => item.name == "Details");
                    Text feedback = root.GetComponentsInChildren<Text>(true).Single(item => item.name == "Feedback");
                    Text status = root.GetComponentsInChildren<Text>(true).Single(item => item.name == "Status");
                    Button primary = root.GetComponentsInChildren<Button>(true).Single(item => item.name == "Primary");
                    Assert.That(details.text, Is.EqualTo(copy.Get("yard.detail")),
                        "Request template owns its text key.");
                    primary.onClick.Invoke();
                    Assert.That(feedback.text, Is.EqualTo(copy.Get("full")));
                    Assert.That(status.text, Is.EqualTo(copy.Get("status", 0, 0)));
                    root.GetComponentsInChildren<Button>(true).Single(item => item.name == "Porch").onClick.Invoke();
                    primary.onClick.Invoke();
                    primary.onClick.Invoke();
                    Assert.That(status.text, Is.EqualTo(copy.Get("status", 0, 9)));
                    primary.onClick.Invoke();
                    Assert.That(status.text, Is.EqualTo(copy.Get("status", 0, 9)));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(staging);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static Config CreateConfig(int count)
        {
            return new Config
            {
                points = Enumerable.Range(0, count).Select(index => new Point
                {
                    id = "site-" + index, nameKey = "fixture.name." + index,
                    openByDefault = count == 1 || index != count - 1
                }).Reverse().ToArray()
            };
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject _staging;
            private readonly TextAsset _configSource;
            private readonly CityMapPresenter _presenter;
            public GameObject Root { get; }
            public CityMapView View { get; }
            public Button[] PointButtons { get; }
            public TextAsset Text { get; }

            public Fixture(int count, bool attachPresenter = true, string corrupt = null)
            {
                // Test-only staging of cloned saved controls; production never builds this tree at runtime.
                _staging = new GameObject("MapExpansionTestStaging");
                _staging.SetActive(false);
                Root = UnityEngine.Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>(CityMapAssets.PanelPath), _staging.transform);
                View = Root.GetComponent<CityMapView>();
                Button[] original = Root.GetComponentsInChildren<Button>(true)
                    .Where(button => button.transform.parent.name == "MapArt").ToArray();
                PointButtons = new Button[count];
                var serialized = new SerializedObject(View);
                SerializedProperty bindings = serialized.FindProperty("_pointBindings");
                bindings.arraySize = count;
                for (int index = 0; index < count; index++)
                {
                    GameObject node = UnityEngine.Object.Instantiate(
                        original[0].gameObject, original[0].transform.parent);
                    node.name = "UnrelatedNodeName" + index;
                    Button button = node.GetComponent<Button>();
                    PointButtons[index] = button;
                    SerializedProperty binding = bindings.GetArrayElementAtIndex(count - 1 - index);
                    binding.FindPropertyRelative("_pointId").stringValue = "site-" + index;
                    binding.FindPropertyRelative("_button").objectReferenceValue = button;
                    binding.FindPropertyRelative("_highlight").objectReferenceValue =
                        node.GetComponentsInChildren<Image>()
                        .Single(image => image != button.image);
                    binding.FindPropertyRelative("_label").objectReferenceValue = node.GetComponentInChildren<Text>();
                }
                CorruptBinding(bindings, corrupt);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (Button button in original) UnityEngine.Object.DestroyImmediate(button.gameObject);
                var table = JsonUtility.FromJson<TextTable>(
                    AssetDatabase.LoadAssetAtPath<TextAsset>(CityMapAssets.TextPath).text);
                table.entries = table.entries.Concat(Enumerable.Range(0, count).Select(index =>
                    new Entry { key = "fixture.name." + index, value = "Place " + index })).ToArray();
                Text = new TextAsset(JsonUtility.ToJson(table));
                _configSource = new TextAsset(JsonUtility.ToJson(CreateConfig(count)));
                _staging.SetActive(true);
                if (attachPresenter)
                    _presenter = new CityMapPresenter(View, Text, new CityMapPreviewConfig(_configSource));
            }

            public Text Label(string name) => Root.GetComponentsInChildren<Text>(true)
                .Single(text => text.name == name);
            public Button Button(string name) => Root.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == name);

            public void Dispose()
            {
                _presenter?.Dispose();
                UnityEngine.Object.DestroyImmediate(_staging);
                UnityEngine.Object.DestroyImmediate(Text);
                UnityEngine.Object.DestroyImmediate(_configSource);
            }

            private static void CorruptBinding(SerializedProperty bindings, string corrupt)
            {
                if (corrupt == null) return;
                SerializedProperty first = bindings.GetArrayElementAtIndex(0);
                SerializedProperty second = bindings.GetArrayElementAtIndex(1);
                if (corrupt == "duplicate-id")
                    second.FindPropertyRelative("_pointId").stringValue =
                        first.FindPropertyRelative("_pointId").stringValue;
                else if (corrupt == "duplicate-button")
                    foreach (string field in new[] { "_button", "_highlight", "_label" })
                        second.FindPropertyRelative(field).objectReferenceValue =
                            first.FindPropertyRelative(field).objectReferenceValue;
                else second.FindPropertyRelative("_label").objectReferenceValue = null;
            }
        }
    }
}
