using System;
using HomelessPetHelper.Modules.PetMotion;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.StationHabitat
{
    public static class StationHabitatAuthor
    {
        public const string ScenePath = "Assets/EditorScenes/StationHabitat/SC_StationHabitat.unity";
        public const string ActorPath = "Assets/Prefabs/Widgets/Widget_StationHabitatActor.prefab";
        private const string PanelPath = "Assets/Prefabs/Panels/Panel_StationLoop.prefab";
        internal const string TextPath = "Assets/Settings/Game/StationHabitat.json";
        private static readonly Color Ink = new Color32(32, 36, 40, 255);
        private static Font Font => AssetDatabase.LoadAssetAtPath<Font>(MainSceneAuthor.FontPath);
        [Serializable] private sealed class Copy { public string title, rest, supplies, pause; }

        [MenuItem("Homeless Pet Helper/Preview/Open Station Habitat")]
        public static void Open()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Apply()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use the isolated habitat authoring tool.");
            CreateActor();
            var text = JsonUtility.FromJson<Copy>(
                AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath).text);
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var panel = root.GetComponent<StationPanelView>();
                var fields = new SerializedObject(panel);
                if (fields.FindProperty("_habitat").objectReferenceValue != null)
                    throw new InvalidOperationException(
                        "Habitat already exists; edit in Prefab Mode instead of recreating it.");
                var overview = (GameObject)fields.FindProperty("_overview").objectReferenceValue;
                var habitat = CreateHabitat(overview.transform, text);
                habitat.transform.SetSiblingIndex(1);
                var layout = habitat.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = 420;
                layout.preferredHeight = 650;
                layout.flexibleHeight = .65f;
                MainSceneAuthor.Bind(panel, "_habitat", habitat);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            CreatePreview(text);
            AssetDatabase.SaveAssets();
        }

        public static void Polish()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Polish only saved assets in an isolated project.");
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                PolishHabitat(root.GetComponentInChildren<StationHabitatView>(true));
                var panel = root.GetComponent<StationPanelView>();
                var fields = new SerializedObject(panel);
                var content = (RectTransform)fields.FindProperty("_rosterContent").objectReferenceValue;
                content.GetComponentInParent<ScrollRect>(true).GetComponent<LayoutElement>().minHeight = 320;
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                var view = sceneRoot.GetComponentInChildren<StationHabitatView>(true);
                if (view != null) PolishHabitat(view);
            }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void PolishHabitat(StationHabitatView view)
        {
            var fields = new SerializedObject(view);
            var actor = (RectTransform)fields.FindProperty("_actor").objectReferenceValue;
            var shadow = (RectTransform)fields.FindProperty("_shadow").objectReferenceValue;
            if (actor.parent.GetComponent<Canvas>() == null)
            {
                var moving = Rect("MovingAnimal", view.transform);
                Place((RectTransform)moving.transform, Vector2.zero, Vector2.one);
                moving.AddComponent<Canvas>();
                moving.AddComponent<GraphicRaycaster>();
                shadow.SetParent(moving.transform, false);
                actor.SetParent(moving.transform, false);
            }
            var attention = (Button)fields.FindProperty("_attention").objectReferenceValue;
            ColorBlock colors = attention.colors;
            colors.disabledColor = Color.white;
            attention.colors = colors;
        }

        private static void CreateActor()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ActorPath) != null)
                throw new InvalidOperationException("Actor already exists; do not overwrite the saved asset.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/PetMotion/Widget_PetMotion.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                root.name = "Widget_StationHabitatActor";
                // A different layer and distant origin isolate this render from the siege rig.
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 29;
                var camera = root.GetComponentInChildren<Camera>(true);
                camera.cullingMask = 1 << 29;
                camera.backgroundColor = Color.clear;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.enabled = false;
                var renderer = root.AddComponent<StationHabitatRenderer>();
                MainSceneAuthor.Bind(renderer, "_player", root.GetComponent<PetMotionPlayer>());
                MainSceneAuthor.Bind(renderer, "_camera", camera);
                var fields = new SerializedObject(renderer);
                var array = fields.FindProperty("_renderers");
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                array.arraySize = renderers.Length;
                for (int i = 0; i < renderers.Length; i++)
                    array.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                fields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ActorPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static StationHabitatView CreateHabitat(Transform parent, Copy text)
        {
            var root = Rect("StationHabitat", parent);
            root.AddComponent<Image>().color = new Color32(243, 246, 245, 255);
            root.AddComponent<RectMask2D>();
            var view = root.AddComponent<StationHabitatView>();
            // The rear fixtures sit outside the rectangular, cat-radius-inset walking area.
            Block("Wall", root.transform, new Vector2(0, .54f), Vector2.one, new Color32(230, 238, 233, 255));
            Block("Skirting", root.transform, new Vector2(0, .53f), new Vector2(1, .55f),
                new Color32(193, 209, 200, 255));
            Block("WindowFrame", root.transform, new Vector2(.1f, .68f), new Vector2(.43f, .94f), Color.white);
            Block("Window", root.transform, new Vector2(.115f, .7f), new Vector2(.415f, .925f),
                new Color32(201, 225, 228, 255));
            Block("WindowBarV", root.transform, new Vector2(.26f, .7f), new Vector2(.27f, .925f), Color.white);
            Block("WindowBarH", root.transform, new Vector2(.115f, .81f), new Vector2(.415f, .82f), Color.white);
            Block("Cupboard", root.transform, new Vector2(.69f, .56f), new Vector2(.92f, .86f),
                new Color32(212, 216, 218, 255));
            var supplies = Label("Supplies", root.transform, text.supplies, 27);
            Place((RectTransform)supplies.transform, new Vector2(.69f, .58f), new Vector2(.92f, .82f));
            for (int i = 1; i < 5; i++)
                Block("FloorJoint" + i, root.transform, new Vector2(0, i * .1f), new Vector2(1, i * .1f + .002f),
                    new Color32(224, 231, 227, 255));
            Block("RestMatEdge", root.transform, new Vector2(.09f, .24f), new Vector2(.4f, .43f),
                new Color32(189, 202, 210, 255));
            Block("RestMat", root.transform, new Vector2(.105f, .255f), new Vector2(.385f, .415f),
                new Color32(216, 224, 231, 255));
            var matLabel = Label("RestLabel", root.transform, text.rest, 25);
            Place((RectTransform)matLabel.transform, new Vector2(.09f, .245f), new Vector2(.4f, .30f));
            var shadow = Rect("GroundShadow", root.transform);
            var shadowRect = (RectTransform)shadow.transform;
            shadowRect.anchorMin = shadowRect.anchorMax = Vector2.zero;
            shadowRect.sizeDelta = new Vector2(160, 12);
            shadow.AddComponent<Image>().color = new Color(0.23f, .3f, .28f, .14f);
            var actor = Rect("Animal", root.transform);
            var actorRect = (RectTransform)actor.transform;
            actorRect.anchorMin = actorRect.anchorMax = Vector2.zero;
            actorRect.pivot = new Vector2(.5f, .1f);
            actorRect.sizeDelta = new Vector2(360, 360);
            var image = actor.AddComponent<RawImage>();
            image.raycastTarget = true;
            var attention = actor.AddComponent<Button>();
            attention.targetGraphic = image;
            var colors = attention.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(.92f, .96f, .94f);
            colors.disabledColor = new Color(1, 1, 1, .55f);
            attention.colors = colors;
            var status = Label("HabitatStatus", root.transform, string.Empty, 32);
            Place((RectTransform)status.transform, new Vector2(.03f, .015f), new Vector2(.97f, .11f));
            var empty = Label("HabitatEmpty", root.transform, string.Empty, 34);
            Place((RectTransform)empty.transform, new Vector2(.15f, .25f), new Vector2(.85f, .48f));
            MainSceneAuthor.Bind(view, "_text", AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath));
            MainSceneAuthor.Bind(view, "_actorPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath)
                .GetComponent<StationHabitatRenderer>());
            MainSceneAuthor.Bind(view, "_actor", actorRect);
            MainSceneAuthor.Bind(view, "_shadow", shadowRect);
            MainSceneAuthor.Bind(view, "_image", image);
            MainSceneAuthor.Bind(view, "_attention", attention);
            MainSceneAuthor.Bind(view, "_status", status);
            MainSceneAuthor.Bind(view, "_empty", empty);
            actor.SetActive(false);
            shadow.SetActive(false);
            return view;
        }

        private static void CreatePreview(Copy text)
        {
            const string directory = "Assets/EditorScenes/StationHabitat";
            if (!AssetDatabase.IsValidFolder(directory))
                AssetDatabase.CreateFolder("Assets/EditorScenes", "StationHabitat");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Rect("StationHabitatPreview", null);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.AddComponent<GraphicRaycaster>();
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            root.AddComponent<Image>().color = Color.white;
            var safe = Rect("SafeArea", root.transform);
            Place((RectTransform)safe.transform, Vector2.zero, Vector2.one);
            safe.AddComponent<SafeAreaView>();
            var title = Label("Title", safe.transform, text.title, 48);
            Place((RectTransform)title.transform, new Vector2(.05f, .9f), new Vector2(.95f, .96f));
            var slot = Rect("RoomSlot", safe.transform);
            Place((RectTransform)slot.transform, new Vector2(.04f, .21f), new Vector2(.96f, .87f));
            var view = CreateHabitat(slot.transform, text);
            var fit = view.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1.35f;
            var pauseRoot = Rect("Pause", safe.transform);
            Place((RectTransform)pauseRoot.transform, new Vector2(.1f, .09f), new Vector2(.9f, .15f));
            pauseRoot.AddComponent<Image>().color = new Color32(242, 243, 244, 255);
            var pause = pauseRoot.AddComponent<Toggle>();
            pause.targetGraphic = pauseRoot.GetComponent<Image>();
            var indicator = Block("Indicator", pauseRoot.transform, new Vector2(.04f, .3f), new Vector2(.1f, .7f), Ink);
            pause.graphic = indicator.GetComponent<Image>();
            var label = Label("Label", pauseRoot.transform, text.pause, 34);
            Place((RectTransform)label.transform, new Vector2(.16f, 0), new Vector2(.9f, 1));
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static GameObject Rect(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            return node;
        }

        private static void Place(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static GameObject Block(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var node = Rect(name, parent);
            Place((RectTransform)node.transform, min, max);
            var image = node.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return node;
        }

        private static Text Label(string name, Transform parent, string text, int size)
        {
            var node = Rect(name, parent);
            var label = node.AddComponent<Text>();
            label.font = Font;
            label.fontSize = size;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 24;
            label.resizeTextMaxSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Ink;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }
    }
}
