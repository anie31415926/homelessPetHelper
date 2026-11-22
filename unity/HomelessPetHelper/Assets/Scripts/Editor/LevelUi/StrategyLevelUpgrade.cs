using System;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Siege.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.LevelUi
{
    /// <summary>Incremental Prefab Mode upgrade in a disposable saved-project copy.</summary>
    public static class StrategyLevelUpgrade
    {
        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a disposable batch copy.");
            Edit("Assets/Prefabs/Widgets/Widget_LevelOverlays.prefab", Settings);
            Edit("Assets/Prefabs/Panels/Panel_SiegeTrial.prefab", ScenePaths);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var serialized = new SerializedObject(content);
            var fields = serialized.FindProperty("_fieldConfigs");
            var paths = new[] { "YardFieldConfig", "ParkingFieldConfig", "YardFormalFieldConfig", "PorchFieldConfig" };
            fields.arraySize = paths.Length;
            for (int index = 0; index < paths.Length; index++)
                fields.GetArrayElementAtIndex(index).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Settings/Game/" + paths[index] + ".json");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            content.Validate();
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
        }

        private static void Settings(GameObject root)
        {
            var view = root.GetComponent<LevelOverlayView>();
            if (Ref<Toggle>(view, "_pauseAtDecision") != null)
                throw new InvalidOperationException("Already upgraded; edit the saved prefab directly.");
            var metrics = AssetDatabase.LoadAssetAtPath<UiLayoutMetrics>("Assets/Settings/UI/LayoutMetrics.asset");
            var page = Ref<GameObject>(view, "_settingsPage");
            var body = page.GetComponentInChildren<ScrollRect>(true).content;
            var row = Rect("DecisionCadenceRow", body);
            row.SetSiblingIndex(3);
            var size = row.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = metrics.touchSize;
            var source = page.GetComponentsInChildren<Toggle>(true).First();
            var toggle = UnityEngine.Object.Instantiate(source, row, false);
            toggle.name = "PauseAtDecision";
            Place((RectTransform)toggle.transform, 0, 0, 1, 1);
            Place(toggle.graphic.rectTransform, .025f, .32f, .085f, .68f);
            var caption = toggle.GetComponentInChildren<Text>(true);
            Place(caption.rectTransform, .12f, .05f, .97f, .95f);
            caption.fontSize = metrics.labelSize;
            caption.alignment = TextAnchor.MiddleLeft;
            caption.resizeTextForBestFit = false;
            Bind(view, "_pauseAtDecision", toggle);
            var note = Text("DecisionCadenceNote", body, 0, 0, 1, 1, metrics.auxiliarySize);
            note.resizeTextForBestFit = false; note.alignment = TextAnchor.MiddleLeft;
            note.transform.SetSiblingIndex(4);
            var noteSize = note.gameObject.AddComponent<LayoutElement>();
            noteSize.minHeight = metrics.captionHeight; noteSize.preferredHeight = -1;
            var data = new SerializedObject(view);
            Append(data.FindProperty("_localizedLabels"), caption, note);
            var keys = data.FindProperty("_labelKeys");
            int old = keys.arraySize; keys.arraySize += 2;
            keys.GetArrayElementAtIndex(old).stringValue = "settings.pauseAtDecision";
            keys.GetArrayElementAtIndex(old + 1).stringValue = "settings.pauseNote";
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void UpdateRouteViewport()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a disposable batch copy.");
            Edit("Assets/Prefabs/Panels/Panel_SiegeTrial.prefab", ScenePaths);
            AssetDatabase.SaveAssets();
        }

        private static void ScenePaths(GameObject root)
        {
            var view = root.GetComponentInChildren<SiegeFieldView>(true);
            var field = Ref<SiegeBattlefield>(view, "_field");
            Bind(field, "_routeViewport", field.transform.parent.parent);
            // Saved route anchors remain for guidance and compatibility callbacks. All physical
            // pointer input goes through the battlefield geometry, with no paper route buttons.
            foreach (string binding in new[] { "_top", "_mid", "_bottom" })
            {
                var button = Ref<Button>(view, binding);
                var feedback = button.GetComponent<UiMotionFeedback>();
                if (feedback != null) UnityEngine.Object.DestroyImmediate(feedback);
                foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
                { graphic.raycastTarget = false; graphic.enabled = false; }
            }
        }

        private static void Append(SerializedProperty property, params UnityEngine.Object[] values)
        {
            int old = property.arraySize; property.arraySize += values.Length;
            for (int index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(old + index).objectReferenceValue = values[index];
        }

        private static void Edit(string path, Action<GameObject> action)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
