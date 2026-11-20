using System;
using System.Linq;
using HomelessPetHelper.Framework.Audio.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Editor.StationHabitat
{
    public static class StationSceneAuthor
    {
        public const string ScenePath = "Assets/EditorScenes/StationHabitat/SC_StationScene.unity";
        private const string PanelPath = "Assets/Prefabs/Panels/Panel_StationLoop.prefab";
        private const string ResidentPath = "Assets/Prefabs/Widgets/Widget_StationHabitatResident.prefab";
        private static readonly Color Ink = new Color32(32, 36, 40, 255);
        [Serializable] private sealed class Copy { public string stationTitle, sceneEmpty, observation; }

        [MenuItem("Homeless Pet Helper/Preview/Open Station Scene")]
        public static void Open()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated scene migration.");
            var copy = JsonUtility.FromJson<Copy>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                StationHabitatAuthor.TextPath).text);
            CreateResident(copy);
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var panel = root.GetComponent<StationPanelView>();
                if (Read<StationSceneView>(panel, "_scene") != null)
                    throw new InvalidOperationException(
                        "Scene layout already exists. Edit the saved prefab in Prefab Mode.");
                Upgrade(panel, copy);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath));
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        public static void Polish()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated prefab edit.");
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var panel = root.GetComponent<StationPanelView>();
                var name = Read<Text>(panel, "_name");
                var heading = name.transform.parent.gameObject;
                SetHeight(heading, 88);
                heading.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                heading.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                SetHeight(Read<Button>(panel, "_back").gameObject, 88);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static void Upgrade(StationPanelView panel, Copy copy)
        {
            var safe = panel.GetComponentInChildren<SafeAreaView>(true).transform;
            Remove<VerticalLayoutGroup>(safe.gameObject);
            var legacy = Rect("LegacyBindings", panel.transform);
            var body = Read<CanvasGroup>(panel, "_actions").transform;
            var overview = Read<GameObject>(panel, "_overview");
            var detail = Read<GameObject>(panel, "_detail");
            var summary = Read<GameObject>(panel, "_summary");
            var chrome = panel.gameObject.AddComponent<StationSceneChrome>();
            Bind(panel, "_chrome", chrome);
            Bind(chrome, "_owner", panel);
            body.SetParent(safe, false);
            Place((RectTransform)body, Vector2.zero, new Vector2(1, .91f));
            Remove<VerticalLayoutGroup>(overview);
            foreach (Transform child in overview.transform.Cast<Transform>().ToArray())
                child.SetParent(legacy.transform, false);
            foreach (Transform child in safe.Cast<Transform>().ToArray())
                if (child != body) child.SetParent(legacy.transform, false);
            Place((RectTransform)overview.transform, new Vector2(0, .075f), Vector2.one);
            Place((RectTransform)summary.transform, Vector2.zero, Vector2.one);
            if (summary.GetComponent<Image>() == null) summary.AddComponent<Image>().color = Color.white;
            var sceneView = CreateRoom(overview.transform, copy);
            Bind(panel, "_scene", sceneView);
            var backdrop = Command("DismissAnimal", body, string.Empty, new Color(0, 0, 0, .08f));
            Place((RectTransform)backdrop.transform, Vector2.zero, Vector2.one);
            Bind(chrome, "_backdrop", backdrop);
            detail.transform.SetAsLastSibling();
            summary.transform.SetAsLastSibling();
            UpgradeDetail(panel, chrome, detail);
            CreateHeader(panel, chrome, safe, copy);
            CreateFooter(panel, chrome, safe);
            CreateToast(panel, chrome, safe);
            Read<GameObject>(chrome, "_menu").transform.SetAsLastSibling();
            legacy.SetActive(false);
            summary.SetActive(false);
            detail.SetActive(false);
            overview.SetActive(true);
            backdrop.gameObject.SetActive(false);
        }

        private static StationSceneView CreateRoom(Transform parent, Copy copy)
        {
            var root = Rect("StationScene", parent);
            Place((RectTransform)root.transform, Vector2.zero, Vector2.one);
            root.AddComponent<Image>().color = new Color32(239, 244, 241, 255);
            root.AddComponent<RectMask2D>();
            var scene = root.AddComponent<StationSceneView>();
            Block("Wall", root.transform, new Vector2(0, .70f), Vector2.one, new Color32(224, 235, 228, 255));
            Block("Skirting", root.transform, new Vector2(0, .69f), new Vector2(1, .70f),
                new Color32(183, 201, 191, 255));
            Block("WindowFrame", root.transform, new Vector2(.08f, .78f), new Vector2(.43f, .94f), Color.white);
            Block("Window", root.transform, new Vector2(.095f, .79f), new Vector2(.415f, .93f),
                new Color32(195, 221, 225, 255));
            Block("WindowCrossV", root.transform, new Vector2(.25f, .79f), new Vector2(.26f, .93f), Color.white);
            Block("WindowCrossH", root.transform, new Vector2(.095f, .855f), new Vector2(.415f, .865f), Color.white);
            for (int i = 1; i <= 6; i++)
                Block("FloorJoint" + i, root.transform, new Vector2(0, i * .1f),
                    new Vector2(1, i * .1f + .0015f), new Color32(219, 228, 223, 255));
            Block("RestMat", root.transform, new Vector2(.10f, .34f), new Vector2(.40f, .43f),
                new Color32(201, 215, 223, 255));
            Block("SecondMat", root.transform, new Vector2(.12f, .51f), new Vector2(.40f, .59f),
                new Color32(225, 209, 215, 255));
            var empty = Label("EmptyStation", root.transform, copy.sceneEmpty, 36);
            Place((RectTransform)empty.transform, new Vector2(.08f, .40f), new Vector2(.92f, .49f));
            Bind(scene, "_empty", empty);
            var cells = new StationResidentView[4];
            for (int i = 0; i < cells.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(ResidentPath), root.transform);
                instance.name = "SceneResident" + i;
                Place((RectTransform)instance.transform, Vector2.zero, Vector2.one);
                cells[i] = instance.GetComponent<StationResidentView>();
                Read<Button>(cells[i], "_select").gameObject.name = "SceneAnimal" + i;
                instance.SetActive(false);
            }
            var fields = new SerializedObject(scene);
            var array = fields.FindProperty("_cells");
            array.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
            for (int i = cells.Length - 1; i >= 0; i--) cells[i].transform.SetAsLastSibling();
            return scene;
        }

        private static void UpgradeDetail(StationPanelView panel, StationSceneChrome chrome, GameObject detail)
        {
            if (detail.GetComponent<Image>() == null) detail.AddComponent<Image>().color = Color.white;
            var group = detail.GetComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(28, 28, 20, 24);
            group.spacing = 12;
            var header = Rect("AnimalHeading", detail.transform);
            header.transform.SetAsFirstSibling();
            header.AddComponent<LayoutElement>();
            SetHeight(header, 88);
            var row = header.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 12;
            row.childControlHeight = row.childControlWidth = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var name = Read<Text>(panel, "_name");
            name.transform.SetParent(header.transform, false);
            name.alignment = TextAnchor.MiddleLeft;
            SetHeight(name.gameObject, 64);
            var close = Read<Button>(panel, "_back");
            close.transform.SetParent(header.transform, false);
            SetHeight(close.gameObject, 88);
            close.GetComponent<LayoutElement>().preferredWidth = 88;
            close.GetComponent<LayoutElement>().flexibleWidth = 0;
            close.GetComponentInChildren<Text>().text = "X";
            var condition = Read<Text>(panel, "_condition");
            SetHeight(condition.gameObject, 100);
            condition.fontSize = condition.resizeTextMaxSize = 32;
            var progress = Read<Text>(panel, "_progress");
            SetHeight(progress.gameObject, 44);
            var stage = Read<DirtSurfaceView>(panel, "_surface").transform.parent.gameObject;
            var commands = Read<Button>(panel, "_check").transform.parent.gameObject;
            var tools = Read<Button>(panel, "_clean").transform.parent.gameObject;
            SetHeight(commands, 88);
            SetHeight(tools, 88);
            Bind(chrome, "_detail", detail.transform);
            Bind(chrome, "_careStage", stage);
            Bind(chrome, "_careCommands", commands);
            Bind(chrome, "_progress", progress.gameObject);
            stage.SetActive(false);
            progress.gameObject.SetActive(false);
        }

        private static void CreateHeader(StationPanelView panel, StationSceneChrome chrome, Transform safe, Copy copy)
        {
            var header = Rect("SceneHeader", safe);
            Place((RectTransform)header.transform, new Vector2(0, .91f), Vector2.one);
            var title = Label("StationTitle", header.transform, copy.stationTitle, 40);
            title.alignment = TextAnchor.MiddleLeft;
            Place((RectTransform)title.transform, new Vector2(.04f, .48f), new Vector2(.80f, .95f));
            var clock = Read<Text>(panel, "_clock");
            clock.transform.SetParent(header.transform, false);
            clock.fontSize = clock.resizeTextMaxSize = 30;
            Place((RectTransform)clock.transform, new Vector2(.04f, .05f), new Vector2(.52f, .45f));
            var capacity = Read<Text>(panel, "_capacity");
            capacity.transform.SetParent(header.transform, false);
            capacity.fontSize = capacity.resizeTextMaxSize = 28;
            Place((RectTransform)capacity.transform, new Vector2(.53f, .05f), new Vector2(.85f, .45f));
            var button = Command("StationMenu", header.transform, "...", new Color32(242, 243, 244, 255));
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
            rect.pivot = new Vector2(1, .5f);
            rect.sizeDelta = new Vector2(88, 88);
            rect.anchoredPosition = new Vector2(-24, 0);
            Bind(chrome, "_menuButton", button);
            CreateMenu(panel, chrome, safe);
        }

        private static void CreateMenu(StationPanelView panel, StationSceneChrome chrome, Transform safe)
        {
            var menu = Rect("StationMenuPopup", safe);
            Place((RectTransform)menu.transform, Vector2.zero, Vector2.one);
            var backdrop = Command("CloseStationMenu", menu.transform, string.Empty, new Color(0, 0, 0, .06f));
            Place((RectTransform)backdrop.transform, Vector2.zero, Vector2.one);
            var body = Rect("MenuItems", menu.transform);
            var rect = (RectTransform)body.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, .90f);
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(380, 232);
            rect.anchoredPosition = new Vector2(-24, 0);
            body.AddComponent<Image>().color = Color.white;
            var group = body.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(16, 16, 16, 16);
            group.spacing = 12;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            var history = Read<Button>(panel, "_history");
            history.transform.SetParent(body.transform, false);
            SetHeight(history.gameObject, 88);
            var pause = Read<Toggle>(panel, "_pause");
            pause.transform.SetParent(body.transform, false);
            SetHeight(pause.gameObject, 88);
            Bind(chrome, "_history", history);
            Bind(chrome, "_menu", menu);
            Bind(chrome, "_menuBackdrop", backdrop);
            menu.SetActive(false);
        }

        private static void CreateFooter(StationPanelView panel, StationSceneChrome chrome, Transform safe)
        {
            var footer = Rect("SceneFunctions", safe);
            Place((RectTransform)footer.transform, new Vector2(0, .005f), new Vector2(1, .075f));
            var outing = Read<Button>(panel, "_restart");
            outing.transform.SetParent(footer.transform, false);
            Place((RectTransform)outing.transform, new Vector2(.64f, .12f), new Vector2(.97f, .92f));
            Bind(chrome, "_footer", footer);
        }

        private static void CreateToast(StationPanelView panel, StationSceneChrome chrome, Transform safe)
        {
            var root = Rect("StationFeedback", safe);
            Place((RectTransform)root.transform, new Vector2(.06f, .81f), new Vector2(.94f, .90f));
            var canvas = root.AddComponent<CanvasGroup>();
            canvas.alpha = 0;
            canvas.blocksRaycasts = canvas.interactable = false;
            var label = Read<Text>(panel, "_feedback");
            label.transform.SetParent(root.transform, false);
            Place((RectTransform)label.transform, Vector2.zero, Vector2.one);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = label.resizeTextMaxSize = 30;
            Bind(chrome, "_toast", canvas);
        }

        private static void CreateResident(Copy copy)
        {
            if (AssetDatabase.LoadMainAssetAtPath(ResidentPath) != null)
                throw new InvalidOperationException("Resident prefab already exists; do not overwrite its bindings.");
            var root = Rect("Widget_StationHabitatResident", null);
            var view = root.AddComponent<StationResidentView>();
            root.AddComponent<Canvas>();
            root.AddComponent<GraphicRaycaster>();
            var position = Rect("AnimalPosition", root.transform);
            var rect = (RectTransform)position.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, .1f);
            rect.sizeDelta = new Vector2(360, 360);
            var still = Block("StillAnimal", position.transform, Vector2.zero, Vector2.one, Color.white)
                .GetComponent<Image>();
            still.preserveAspect = true;
            still.raycastTarget = true;
            var imageRoot = Rect("AnimatedAnimal", position.transform);
            Place((RectTransform)imageRoot.transform, Vector2.zero, Vector2.one);
            var image = imageRoot.AddComponent<RawImage>();
            var button = position.AddComponent<Button>();
            position.AddComponent<ButtonAudio>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.disabledColor = colors.highlightedColor = Color.white;
            button.colors = colors;
            var cage = Rect("ObservationCage", position.transform);
            Place((RectTransform)cage.transform, new Vector2(.01f, .07f), new Vector2(.99f, .92f));
            Frame(cage.transform, new Color32(118, 145, 137, 255), .018f);
            for (int i = 1; i < 5; i++)
                Block("CageBar" + i, cage.transform, new Vector2(i * .2f, .02f),
                    new Vector2(i * .2f + .012f, .98f), new Color(.46f, .57f, .54f, .50f));
            var observation = Label("ObservationLabel", cage.transform, copy.observation, 26);
            Place((RectTransform)observation.transform, new Vector2(.1f, .86f), new Vector2(.9f, .99f));
            var selected = Rect("Selection", position.transform);
            Place((RectTransform)selected.transform, new Vector2(.06f, .06f), new Vector2(.94f, .95f));
            Frame(selected.transform, new Color32(80, 133, 108, 255), .012f);
            var name = Label("ResidentName", position.transform, string.Empty, 28);
            Place((RectTransform)name.transform, new Vector2(-.1f, -.13f), new Vector2(1.1f, .02f));
            Bind(view, "_rendererPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(StationHabitatAuthor.ActorPath)
                .GetComponent<StationHabitatRenderer>());
            Bind(view, "_motionImage", image);
            Bind(view, "_stillImage", still);
            Bind(view, "_position", rect);
            Bind(view, "_name", name);
            Bind(view, "_cage", cage);
            Bind(view, "_selected", selected);
            Bind(view, "_select", button);
            selected.SetActive(false);
            cage.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, ResidentPath);
            Object.DestroyImmediate(root);
        }

        private static void Frame(Transform parent, Color color, float thickness)
        {
            Block("Left", parent, Vector2.zero, new Vector2(thickness, 1), color);
            Block("Right", parent, new Vector2(1 - thickness, 0), Vector2.one, color);
            Block("Bottom", parent, Vector2.zero, new Vector2(1, thickness), color);
            Block("Top", parent, new Vector2(0, 1 - thickness), Vector2.one, color);
        }

        private static void SetHeight(GameObject node, float height)
        {
            var layout = node.GetComponent<LayoutElement>();
            layout.minHeight = 0;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;
        }

        private static T Read<T>(Object target, string field) where T : Object =>
            (T)new SerializedObject(target).FindProperty(field).objectReferenceValue;
        private static void Bind(Object target, string field, Object value) =>
            MainSceneAuthor.Bind(target, field, value);
        private static void Remove<T>(GameObject node) where T : Component
        {
            var component = node.GetComponent<T>();
            if (component != null) Object.DestroyImmediate(component);
        }
        private static GameObject Rect(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            return node;
        }
        private static void Place(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max;
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
        private static Text Label(string name, Transform parent, string value, int size)
        {
            var node = Rect(name, parent);
            var text = node.AddComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>(MainSceneAuthor.FontPath);
            text.text = value; text.fontSize = size;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 24; text.resizeTextMaxSize = size;
            text.color = Ink; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }
        private static Button Command(string name, Transform parent, string value, Color color)
        {
            var node = Rect(name, parent);
            var image = node.AddComponent<Image>();
            image.color = color;
            var button = node.AddComponent<Button>();
            node.AddComponent<ButtonAudio>();
            button.targetGraphic = image;
            var label = Label("Label", node.transform, value, 36);
            Place((RectTransform)label.transform, Vector2.zero, Vector2.one);
            return button;
        }
    }
}
