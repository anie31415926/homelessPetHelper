using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.Station;
using System;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.RescueCase
{
    public static class RescueCaseAssets
    {
        public const string ScenePath = "Assets/EditorScenes/SC_RescueCase.unity";
        public const string PanelPath = "Assets/Prefabs/Panels/Panel_RescueCase.prefab";
        public const string TextPath = "Assets/EditorScenes/RescueCaseText.json";
        public const string OptionsPath = "Assets/Settings/Rescue/RescueOptions.json";
        public const string OptionTextPath = "Assets/Settings/Rescue/RescueOptionText.zh-CN.json";

        [MenuItem("Homeless Pet Helper/Preview/Open Rescue Case")]
        public static void OpenScene()
        {
            OpenScenario(CaseStart.Normal);
        }

        [MenuItem("Homeless Pet Helper/Preview/Open Stress Recovery Case")]
        public static void OpenStressRecovery()
        {
            OpenScenario(CaseStart.StressRecovery);
        }

        [MenuItem("Homeless Pet Helper/Preview/Open Rescue Case", true)]
        [MenuItem("Homeless Pet Helper/Preview/Open Stress Recovery Case", true)]
        private static bool CanOpenScenario()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode ||
                (Application.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == ScenePath);
        }

        private static void OpenScenario(CaseStart start)
        {
            if (Application.isPlaying)
            {
                RescueCaseHost.SetStart(start);
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                RescueCaseHost.SetStart(start);
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        public static void CreateAssets()
        {
            if (!Application.isBatchMode || AssetDatabase.LoadMainAssetAtPath(PanelPath) != null ||
                AssetDatabase.LoadMainAssetAtPath(ScenePath) != null)
            {
                throw new InvalidOperationException("Author once in an isolated batch project; do not overwrite.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(OptionPreviewAssets.PanelPath);
            try
            {
                AuthorPanel(root);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath));
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        public static void NormalizeLayout()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                foreach (HorizontalLayoutGroup row in root.GetComponentsInChildren<HorizontalLayoutGroup>(true))
                {
                    row.childControlWidth = true;
                    row.childControlHeight = true;
                }

                var panel = new SerializedObject(root.GetComponent<OptionPanelView>());
                var feedback = (Text)panel.FindProperty("_feedback").objectReferenceValue;
                feedback.resizeTextForBestFit = false;
                feedback.fontSize = 34;
                feedback.horizontalOverflow = HorizontalWrapMode.Wrap;
                feedback.verticalOverflow = VerticalWrapMode.Truncate;
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AuthorPanel(GameObject root)
        {
            root.SetActive(false);
            root.name = "Panel_RescueCase";
            var text = new OptionPreviewText(AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath));
            var panel = root.GetComponent<OptionPanelView>();
            var binding = new SerializedObject(panel);
            var list = (ScrollRect)binding.FindProperty("_scroll").objectReferenceValue;
            Transform parent = list.transform.parent;
            var layout = parent.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 24, 24);
            layout.spacing = 16;
            parent.Find("Title").GetComponent<Text>().text = text.Get("title");
            SetHeight(parent.Find("Title"), 64);
            var intent = parent.Find("Section").GetComponent<Text>();
            SetHeight(intent.transform, 108);
            Text animal = Label("Animal", parent, 100);
            animal.transform.SetSiblingIndex(1);
            Text details = Label("Details", parent, 240);
            details.transform.SetSiblingIndex(list.transform.GetSiblingIndex() + 1);
            var feedback = (Text)binding.FindProperty("_feedback").objectReferenceValue;
            SetHeight(feedback.transform, 144);
            feedback.resizeTextForBestFit = false;
            feedback.fontSize = 34;
            var use = (Button)binding.FindProperty("_use").objectReferenceValue;
            use.GetComponentInChildren<Text>().text = text.Get("use");
            GameObject commands = new GameObject("Help", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            commands.transform.SetParent(parent, false);
            var row = commands.GetComponent<HorizontalLayoutGroup>();
            row.spacing = 16;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = false;
            SetHeight(commands.transform, 88);
            Button handbook = Command("Handbook", commands.transform, use, text.Get("handbook"));
            Button assistance = Command("Assistance", commands.transform, use, text.Get("assistance"));
            Button advance = Command("Advance", parent, use, text.Get("advance.Responding"));
            var view = root.AddComponent<RescueCaseView>();
            Bind(view, "_options", panel);
            Bind(view, "_animal", animal);
            Bind(view, "_intent", intent);
            Bind(view, "_details", details);
            Bind(view, "_advance", advance);
            Bind(view, "_advanceLabel", advance.GetComponentInChildren<Text>());
            Bind(view, "_handbook", handbook);
            Bind(view, "_assistance", assistance);
            Bind(view, "_choices", list.gameObject);
            Bind(view, "_use", use.gameObject);
            root.SetActive(true);
        }

        private static Text Label(string name, Transform parent, float height)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Text));
            root.transform.SetParent(parent, false);
            SetHeight(root.transform, height);
            var label = root.GetComponent<Text>();
            label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 36;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 24;
            label.resizeTextMaxSize = 36;
            label.color = new Color32(32, 36, 40, 255);
            label.raycastTarget = false;
            return label;
        }

        private static Button Command(string name, Transform parent, Button source, string label)
        {
            Button result = UnityEngine.Object.Instantiate(source, parent);
            result.name = name;
            result.GetComponentInChildren<Text>().text = label;
            SetHeight(result.transform, 88);
            return result;
        }

        private static void SetHeight(Transform target, float height)
        {
            var layout = target.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = target.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredHeight = height;
            layout.flexibleHeight = 0;
            layout.flexibleWidth = 1;
        }

        private static void Bind(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
