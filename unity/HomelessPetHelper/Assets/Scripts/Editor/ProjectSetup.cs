using System;
using HomelessPetHelper.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomelessPetHelper.Editor
{
    public static class ProjectSetup
    {
        public const string MainScenePath = "Assets/Scenes/Main.unity";

        // Deliberately batch-only: opening the editor must never rewrite a designer's scene.
        public static void Initialize()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Use the repository initialization command in batch mode.");
            }

            ConfigurePlayer();
            EnsureMainScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        private static void ConfigurePlayer()
        {
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.productName = "Homeless Pet Helper";
            PlayerSettings.companyName = "HomelessPetHelper";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard);
        }

        private static void EnsureMainScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) != null)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(231, 242, 233, 255);
            new GameObject("Game Bootstrap", typeof(GameBootstrap));
            EditorSceneManager.SaveScene(scene, MainScenePath);
        }
    }
}
