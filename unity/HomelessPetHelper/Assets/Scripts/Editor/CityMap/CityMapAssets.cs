using UnityEditor;
using UnityEditor.SceneManagement;

namespace HomelessPetHelper.Editor.CityMap
{
    /// <summary>Saved assets are authoritative; edit the Prefab instead of recreating the map.</summary>
    public static class CityMapAssets
    {
        public const string ScenePath = "Assets/EditorScenes/SC_CityMap.unity";
        public const string PanelPath = "Assets/Prefabs/Panels/Panel_CityMap.prefab";
        public const string TextPath = "Assets/EditorScenes/CityMapText.json";
        public const string ConfigPath = "Assets/EditorScenes/CityMapConfig.json";

        [MenuItem("Homeless Pet Helper/Preview/Open City Map")]
        public static void OpenScene()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
