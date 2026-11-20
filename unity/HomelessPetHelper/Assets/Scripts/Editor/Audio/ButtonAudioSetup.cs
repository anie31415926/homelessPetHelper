using System;
using HomelessPetHelper.Framework.Audio.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.Audio
{
    public static class ButtonAudioSetup
    {
        [MenuItem("Homeless Pet Helper/Audio/Add Button Audio To Selected Hierarchy")]
        public static void AddToSelectedHierarchy()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            GameObject selected = Selection.activeGameObject;
            if (stage == null || selected == null || !selected.scene.Equals(stage.scene))
                throw new InvalidOperationException("Select a saved panel or widget in Prefab Mode first.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before editing button feedback.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add button audio feedback");
            foreach (Button button in selected.GetComponentsInChildren<Button>(true))
                if (button.GetComponent<ButtonAudio>() == null) Undo.AddComponent<ButtonAudio>(button.gameObject);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(stage.scene);
        }

        [MenuItem("Homeless Pet Helper/Audio/Add Button Audio To Selected Hierarchy", true)]
        private static bool CanAddToSelectedHierarchy()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return !EditorApplication.isPlayingOrWillChangePlaymode && stage != null &&
                Selection.activeGameObject != null && Selection.activeGameObject.scene.Equals(stage.scene);
        }

        internal static AudioScope AddToRoot(GameObject root, bool addScope)
        {
            AudioScope scope = root.GetComponent<AudioScope>();
            if (addScope && scope == null) scope = root.AddComponent<AudioScope>();
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.GetComponent<ButtonAudio>() == null) button.gameObject.AddComponent<ButtonAudio>();
            return scope;
        }
    }
}
