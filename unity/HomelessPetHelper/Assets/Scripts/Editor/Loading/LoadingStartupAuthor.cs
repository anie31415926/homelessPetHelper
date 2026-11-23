using System;
using System.Linq;
using HomelessPetHelper.Game.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HomelessPetHelper.Editor.Loading
{
    public static class LoadingStartupAuthor
    {
        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated LoadingStartup.");
            var scene = EditorSceneManager.OpenScene(LoadingSceneRoot.ScenePath);
            var host = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<LoadingSceneRoot>(true)).Single();
            var data = new SerializedObject(host);
            data.FindProperty("_demoSeconds").floatValue = LoadingSceneRoot.DefaultDemoSeconds;
            data.FindProperty("_timeoutSeconds").floatValue = 60f;
            data.FindProperty("_previewOnly").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Configured existing Loading startup: demo preparation, then real Main scene load.");
        }
    }
}
