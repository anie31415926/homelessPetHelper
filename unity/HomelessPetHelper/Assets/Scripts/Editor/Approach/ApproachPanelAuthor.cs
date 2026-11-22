using System;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.Approach
{
    /// <summary>Creates the new static panel once in an isolated project. Existing panel assets are never
    /// rebuilt.</summary>
    public static class ApproachPanelAuthor
    {
        public const string PanelPath = "Assets/Prefabs/Panels/Panel_Approach.prefab";
        private const string ContentPath = "Assets/Settings/Game/GameContent.asset";

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated ApproachAssets authoring.");
            Font font = AssetDatabase.LoadAssetAtPath<Font>(MainSceneAuthor.FontPath);
            if (font == null) throw new InvalidOperationException("Missing bundled font.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath) == null) CreatePanel(font);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            MainSceneAuthor.Bind(content, "_approachConfig", AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Game/ApproachConfig.json"));
            MainSceneAuthor.Bind(content, "_approachText", AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Game/ApproachText.json"));
            content.Validate();
            var scene = EditorSceneManager.OpenScene(MainSceneAuthor.MainPath);
            var roots = scene.GetRootGameObjects();
            var host = roots.SelectMany(root => root.GetComponentsInChildren<MainGameRoot>(true)).Single();
            var panel = roots.SelectMany(root => root.GetComponentsInChildren<ApproachPanelView>(true))
                .SingleOrDefault();
            if (panel == null)
                panel = ((GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath))).GetComponent<ApproachPanelView>();
            panel.gameObject.SetActive(false);
            MainSceneAuthor.Bind(host, "_approach", panel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void CreatePanel(Font font)
        {
            var root = new GameObject("Panel_Approach", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = .5f;
                root.AddComponent<GraphicRaycaster>();
                var view = root.AddComponent<ApproachPanelView>();
                var background = Rect("Background", root.transform, Vector2.zero, Vector2.one);
                background.gameObject.AddComponent<Image>().color = new Color32(245, 246, 235, 255);
                var safe = Rect("SafeArea", root.transform, Vector2.zero, Vector2.one);
                safe.gameObject.AddComponent<SafeAreaView>();
                BindText(view, "_title", Text("Title", safe, font, .04f, .94f, .75f, .985f, 44));
                BindText(view, "_instruction", Text("Instruction", safe, font, .05f, .855f, .95f, .932f, 34));
                BindText(view, "_seed", Text("Seed", safe, font, .05f, .826f, .95f, .851f, 26));
                BindText(view, "_summary", Text("Summary", safe, font, .05f, .13f, .95f, .17f, 34));
                BindText(view, "_feedback", Text("Feedback", safe, font, .05f, .079f, .95f, .129f, 32));
                MainSceneAuthor.Bind(view, "_pause", Command("Pause", safe, font, .77f, .94f, .96f, .985f));
                MainSceneAuthor.Bind(view, "_board", Board(safe, font));
                Commands(view, safe, font);
                Colors(view);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static ApproachBoardView Board(Transform parent, Font font)
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Settings/Game/ApproachConfig.json");
            var template = new ApproachCatalog(JsonUtility.FromJson<ApproachCatalogData>(source.text))
                .Get(ApproachMode.Cleanup);
            var area = Rect("BoardArea", parent, new Vector2(.045f, .247f), new Vector2(.955f, .818f));
            var board = Rect("Board", area, Vector2.zero, Vector2.one);
            var ratio = board.gameObject.AddComponent<AspectRatioFitter>();
            ratio.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            ratio.aspectRatio = (float)template.Columns / template.Rows;
            board.gameObject.AddComponent<Image>().color = new Color32(232, 238, 220, 255);
            var view = board.gameObject.AddComponent<ApproachBoardView>();
            MainSceneAuthor.Bind(view, "_rect", board);
            var cells = new ApproachCellView[template.Columns * template.Rows];
            for (int cell = 0; cell < cells.Length; cell++) cells[cell] = Cell(board, font, cell, template);
            var serialized = new SerializedObject(view);
            var array = serialized.FindProperty("_cells");
            array.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static ApproachCellView Cell(Transform parent, Font font, int index, ApproachTemplate template)
        {
            int col = index % template.Columns, row = index / template.Columns;
            float width = 1f / template.Columns, height = 1f / template.Rows;
            var rect = Rect("Cell" + index, parent, new Vector2(col * width, 1 - (row + 1) * height),
                new Vector2((col + 1) * width, 1 - row * height));
            rect.offsetMin = new Vector2(2, 2);
            rect.offsetMax = new Vector2(-2, -2);
            var surface = rect.gameObject.AddComponent<Image>();
            surface.raycastTarget = false;
            var label = Text("Label", rect, font, .03f, .02f, .97f, .98f, 34);
            label.resizeTextMinSize = 20;
            var cell = rect.gameObject.AddComponent<ApproachCellView>();
            MainSceneAuthor.Bind(cell, "_surface", surface);
            MainSceneAuthor.Bind(cell, "_label", label);
            return cell;
        }

        private static void Commands(ApproachPanelView view, Transform parent, Font font)
        {
            var tools = Rect("Tools", parent, new Vector2(.04f, .181f), new Vector2(.96f, .239f));
            var layout = tools.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childForceExpandWidth = layout.childControlWidth = true;
            layout.childForceExpandHeight = layout.childControlHeight = true;
            foreach (string name in new[] { "Move", "Bridge", "Litter", "Weeds", "Mud" })
            {
                var button = Command(name, tools, font, 0, 0, 1, 1);
                button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                MainSceneAuthor.Bind(view, "_" + name.ToLowerInvariant(), button);
            }
            MainSceneAuthor.Bind(view, "_undo", Command("Undo", parent, font, .04f, .015f, .31f, .073f));
            MainSceneAuthor.Bind(view, "_return", Command("Return", parent, font, .33f, .015f, .60f, .073f));
            MainSceneAuthor.Bind(view, "_continue", Command("Continue", parent, font, .62f, .015f, .96f, .073f));
        }

        private static ApproachCommandView Command(string name, Transform parent, Font font,
            float x0, float y0, float x1, float y1)
        {
            var rect = Rect(name, parent, new Vector2(x0, y0), new Vector2(x1, y1));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color32(212, 225, 205, 255);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(.77f, .78f, .74f, .65f);
            button.colors = colors;
            var label = Text("Label", rect, font, .04f, .08f, .96f, .92f, 36);
            var command = rect.gameObject.AddComponent<ApproachCommandView>();
            MainSceneAuthor.Bind(command, "_button", button);
            MainSceneAuthor.Bind(command, "_label", label);
            return command;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text Text(string name, Transform parent, Font font,
            float x0, float y0, float x1, float y1, int fontSize)
        {
            var rect = Rect(name, parent, new Vector2(x0, y0), new Vector2(x1, y1));
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = label.resizeTextMaxSize = fontSize;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 24;
            label.color = new Color32(46, 65, 53, 255);
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.text = string.Empty;
            return label;
        }

        private static void BindText(ApproachPanelView view, string field, Text value) =>
            MainSceneAuthor.Bind(view, field, value);

        private static void Colors(ApproachPanelView view)
        {
            var serialized = new SerializedObject(view);
            var values = new[] { ("_floorColor", new Color32(240, 239, 223, 255)),
                ("_wallColor", new Color32(190, 199, 187, 255)), ("_waterColor", new Color32(181, 213, 227, 255)),
                ("_bridgeColor", new Color32(224, 194, 142, 255)), ("_boxColor", new Color32(223, 179, 130, 255)),
                ("_litterColor", new Color32(207, 204, 186, 255)), ("_weedsColor", new Color32(179, 202, 147, 255)),
                ("_mudColor", new Color32(214, 188, 163, 255)), ("_selectedColor", new Color32(245, 211, 115, 255)),
                ("_reachableColor", new Color32(226, 238, 209, 255)) };
            foreach (var value in values) serialized.FindProperty(value.Item1).colorValue = value.Item2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
