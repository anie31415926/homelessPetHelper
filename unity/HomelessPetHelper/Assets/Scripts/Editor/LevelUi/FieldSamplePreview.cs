using System;
using System.Linq;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Game;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Station.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.LevelUi
{
    /// <summary>Editor-only composition for six samples. No asset edits and no player storage access.</summary>
    [InitializeOnLoad]
    public static class FieldSamplePreview
    {
        private const string Pending = "HomelessPetHelper.FieldSample";
        private static MainGameRoot _root;
        static FieldSamplePreview()
        {
            SceneManager.sceneLoaded += Configure;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                EditorApplication.update -= Depart;
                _root = null;
            };
        }

        [MenuItem("Homeless Pet Helper/Field Samples/Parking - Puzzle")]
        public static void ParkingPuzzle() => Launch("parking", 1);
        [MenuItem("Homeless Pet Helper/Field Samples/Parking - Cleanup")]
        public static void ParkingCleanup() => Launch("parking", 0);
        [MenuItem("Homeless Pet Helper/Field Samples/Yard - Puzzle")]
        public static void YardPuzzle() => Launch("yard", 1);
        [MenuItem("Homeless Pet Helper/Field Samples/Yard - Cleanup")]
        public static void YardCleanup() => Launch("yard", 0);
        [MenuItem("Homeless Pet Helper/Field Samples/Porch - Puzzle")]
        public static void PorchPuzzle() => Launch("porch", 1);
        [MenuItem("Homeless Pet Helper/Field Samples/Porch - Cleanup")]
        public static void PorchCleanup() => Launch("porch", 0);

        private static void Launch(string point, int seed)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MainSceneAuthor.MainPath);
            SessionState.SetString(Pending, point + ":" + seed);
            EditorApplication.EnterPlaymode();
        }

        private static void Configure(Scene scene, LoadSceneMode mode)
        {
            string pending = SessionState.GetString(Pending, string.Empty);
            if (!Application.isPlaying || scene.path != MainSceneAuthor.MainPath || pending.Length == 0) return;
            SessionState.EraseString(Pending);
            string[] choice = pending.Split(':');
            int seed = int.Parse(choice[1], System.Globalization.CultureInfo.InvariantCulture);
            _root = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainGameRoot>(true))
                .Single();
            var content = CreateContent(Ref<GameContent>(_root, "_content"), choice[0]);
            Bind(_root, "_content", content);
            _root.ConfigureProgressStorage(new PreviewStorage());
            _root.ConfigurePreferenceStorage(new PreviewStorage(), new PreviewStorage(), new PreviewStorage());
            _root.ConfigureOutingSeedSource(() => seed);
            EditorApplication.update += Depart;
            Debug.Log("Field sample uses temporary memory progress and preferences: " + pending);
        }

        public static GameContent CreateContent(GameContent original, string point)
        {
            var content = UnityEngine.Object.Instantiate(original);
            content.hideFlags = HideFlags.DontSave;
            var map = JObject.Parse(Ref<TextAsset>(content, "_mapConfig").text);
            string template = point == "parking" ? "ordinary" : "ordinary-dog";
            map["initialPointId"] = point;
            var definition = map["requestTemplates"].Single(item => (string)item["id"] == template);
            definition["allowedPointIds"] = new JArray(point);
            map["initialRequests"] = new JArray(new JObject
            { ["templateId"] = template, ["pointId"] = point, ["templateVersion"] = 1 });
            // The sample keeps a single authored request; keeping dispatch would reference points and
            // templates this derived config no longer allows, and validation rejects the binding.
            map.Remove("dispatch");
            var source = new TextAsset(map.ToString()) { hideFlags = HideFlags.DontSave };
            Bind(content, "_mapConfig", source);
            var serialized = new SerializedObject(content);
            var fields = serialized.FindProperty("_fieldConfigs");
            var selected = content.CreateFields().Layouts.Single(field => field.PointId == point &&
                !field.Introductory);
            var candidates = Enumerable.Range(0, fields.arraySize).Select(index =>
                fields.GetArrayElementAtIndex(index).objectReferenceValue).Cast<TextAsset>().ToArray();
            var fieldSource = candidates.Single(asset => new Game.Field.OutingFieldLayout(asset).LayoutId ==
                selected.LayoutId);
            fields.arraySize = 1; fields.GetArrayElementAtIndex(0).objectReferenceValue = fieldSource;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            content.Validate();
            return content;
        }

        private static void Depart()
        {
            if (_root == null || _root.Presenter == null) return;
            EditorApplication.update -= Depart;
            // Editor fixture drives saved controls once, then the player owns normal runtime input.
            Click(Ref<StationPanelView>(_root, "_station"), "Restart");
            Click(Ref<CityMapView>(_root, "_map"), "Primary");
        }
        private static void Click(Component owner, string name) => owner.GetComponentsInChildren<Button>()
            .Single(button => button.name == name).onClick.Invoke();

        private sealed class PreviewStorage : ISaveStorage
        {
            private readonly string[] _slots = new string[2];
            public string Read(int slot) => _slots[slot];
            public void Write(int slot, string content) => _slots[slot] = content;
            public void Archive(int slot) { }
        }
    }
}
