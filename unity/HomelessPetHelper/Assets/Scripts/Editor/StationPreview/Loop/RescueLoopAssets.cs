using System;
using HomelessPetHelper.Editor.CityMap;
using HomelessPetHelper.Editor.RescueCase;
using HomelessPetHelper.Modules.Station.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.StationPreview.Loop
{
    public static class RescueLoopAssets
    {
        public const string ScenePath = "Assets/EditorScenes/SC_RescueLoop.unity";
        public const string ConfigPath = "Assets/EditorScenes/RescueLoopConfig.json";
        public const string StationPanelPath = "Assets/Prefabs/Panels/Panel_StationLoop.prefab";

        public static void CreateSavedScene()
        {
            if (!Application.isBatchMode || AssetDatabase.LoadMainAssetAtPath(ScenePath) != null ||
                AssetDatabase.LoadMainAssetAtPath(StationPanelPath) != null)
                throw new InvalidOperationException("Author the loop scene once in an isolated batch project.");
            GameObject stationRoot = PrefabUtility.LoadPrefabContents(StationPreviewAssets.PanelPath);
            try
            {
                AuthorRoster(stationRoot.GetComponent<StationPanelView>());
                stationRoot.name = "Panel_StationLoop";
                PrefabUtility.SaveAsPrefabAsset(stationRoot, StationPanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(stationRoot); }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (string path in new[] { StationPanelPath, CityMapAssets.PanelPath,
                RescueCaseAssets.PanelPath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                panel.SetActive(path == StationPanelPath);
            }
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void AuthorRoster(StationPanelView view)
        {
            StationAnimalView[] cells = view.Animals;
            Transform parent = cells[0].transform.parent;
            var viewport = new GameObject("Roster", typeof(RectTransform), typeof(LayoutElement),
                typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(parent, false);
            viewport.transform.SetSiblingIndex(cells[0].transform.GetSiblingIndex());
            viewport.GetComponent<LayoutElement>().flexibleHeight = 1;
            viewport.GetComponent<Image>().color = Color.white;
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            var rect = (RectTransform)content.transform;
            rect.SetParent(viewport.transform, false);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (StationAnimalView cell in cells)
            {
                cell.transform.SetParent(rect, false);
                cell.GetComponent<LayoutElement>().flexibleHeight = 0;
                cell.GetComponent<LayoutElement>().preferredHeight = 320;
            }
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = rect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var binding = new SerializedObject(view);
            binding.FindProperty("_rosterContent").objectReferenceValue = rect;
            binding.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
