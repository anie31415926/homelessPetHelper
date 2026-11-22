using System;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.Field
{
    /// <summary>One-time incremental upgrade. Existing assets and GUIDs are preserved.</summary>
    public static class YardFieldAuthor
    {
        private const string ApproachPath = "Assets/Prefabs/Panels/Panel_Approach.prefab";
        private const string SiegePath = "Assets/Prefabs/Panels/Panel_SiegeTrial.prefab";
        private const string ChoicePath = "Assets/Prefabs/Widgets/Widget_SiegeChoice.prefab";
        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated YardFieldAssets.");
            ImportAnimal("Dog"); ImportAnimal("Cat"); Choice(); Approach(); Siege();
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            ContentFields(content);
            content.Validate(); AssetDatabase.SaveAssets();
        }
        public static void Refine()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated YardFieldRefine.");
            var approach = PrefabUtility.LoadPrefabContents(ApproachPath);
            try
            {
                var view = approach.GetComponent<ApproachPanelView>();
                var board = Ref<ApproachBoardView>(view, "_sceneBoard");
                foreach (var image in
                    board.GetComponentsInChildren<Image>(true).Where(image => image.name == "LocationBackground"))
                    UnityEngine.Object.DestroyImmediate(image.gameObject);
                var shared = Ref<OutingFieldView>(view, "_scene");
                Ref<Image>(shared, "_background").name = "LocationBackground";
                var terrain = Ref<OutingFieldGraphic>(shared, "_terrain");
                if (terrain.GetComponent<CanvasRenderer>() == null) terrain.gameObject.AddComponent<CanvasRenderer>();
                RefineScene(shared);
                RefineApproachUi(view);
                PrefabUtility.SaveAsPrefabAsset(approach, ApproachPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(approach); }
            var siege = PrefabUtility.LoadPrefabContents(SiegePath);
            try
            {
                var shared = siege.GetComponent<OutingFieldView>();
                Ref<Image>(shared, "_background").name = "LocationBackground";
                var terrain = Ref<OutingFieldGraphic>(shared, "_terrain");
                if (terrain.GetComponent<CanvasRenderer>() == null) terrain.gameObject.AddComponent<CanvasRenderer>();
                var legacy = siege.GetComponent<SiegeTrialView>();
                var scene = Ref<SiegeFieldView>(legacy, "_scene");
                var safe = siege.GetComponentInChildren<SafeAreaView>(true).transform;
                Bind(legacy, "_legacyContent", WrapLegacy(safe, scene.transform).gameObject);
                foreach (var icon in scene.GetComponentsInChildren<SiegePropIcon>(true))
                    if (icon.GetComponent<CanvasRenderer>() == null) icon.gameObject.AddComponent<CanvasRenderer>();
                RefineScene(shared);
                RefineSiegeUi(scene);
                PrefabUtility.SaveAsPrefabAsset(siege, SiegePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(siege); }
            var choice = PrefabUtility.LoadPrefabContents(ChoicePath);
            try
            {
                foreach (var icon in choice.GetComponentsInChildren<SiegePropIcon>(true))
                    if (icon.GetComponent<CanvasRenderer>() == null) icon.gameObject.AddComponent<CanvasRenderer>();
                PrefabUtility.SaveAsPrefabAsset(choice, ChoicePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(choice); }
            ContentFields(AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset"));
            AssetDatabase.SaveAssets();
        }

        private static void ContentFields(GameContent content)
        {
            var serialized = new SerializedObject(content);
            var fields = serialized.FindProperty("_fieldConfigs");
            if (fields.arraySize == 0)
            {
                fields.arraySize = 1;
                fields.GetArrayElementAtIndex(0).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Settings/Game/YardFieldConfig.json");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void RefineScene(OutingFieldView scene)
        {
            Place((RectTransform)Ref<RectTransform>(scene, "_frame").parent, 0, .105f, 1, .985f);
            var background = Ref<Image>(scene, "_background");
            background.transform.SetParent(scene.transform, false);
            background.transform.SetAsFirstSibling();
            var safe = scene.GetComponentInChildren<SafeAreaView>(true).transform;
            background.transform.SetSiblingIndex(safe.GetSiblingIndex() - 1);
            Place(background.rectTransform, 0, 0, 1, 1);
            var fit = background.GetComponent<AspectRatioFitter>();
            if (fit == null) fit = background.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = background.sprite.rect.width / background.sprite.rect.height;
            background.gameObject.SetActive(false);
        }

        private static void RefineApproachUi(ApproachPanelView view)
        {
            var pause = Ref<ApproachCommandView>(view, "_pause");
            var chrome = new SerializedObject(Ref<FieldPanelPlacement>(view, "_sceneChrome"));
            var placements = chrome.FindProperty("_placements");
            var item = Enumerable.Range(0, placements.arraySize).Select(placements.GetArrayElementAtIndex)
                .Single(value => value.FindPropertyRelative("Target").objectReferenceValue == pause.transform);
            item.FindPropertyRelative("SceneMin").vector2Value = new Vector2(.72f, .89f);
            item.FindPropertyRelative("SceneMax").vector2Value = new Vector2(.97f, .945f);
            chrome.ApplyModifiedPropertiesWithoutUndo();
            if (Ref<GameObject>(view, "_sceneMenu") != null)
            { HelpText(Ref<Text>(view, "_sceneMenuText")); return; }
            var safe = view.GetComponentInChildren<SafeAreaView>(true).transform;
            var notice = Text("FieldNotice", safe, .12f, .14f, .88f, .20f, 32);
            Tint(notice.rectTransform);
            Bind(view, "_sceneNotice", notice); notice.gameObject.SetActive(false);
            var menu = Rect("FieldMenu", safe); Surface(menu, new Color32(36, 52, 44, 180), true);
            var card = Rect("Card", menu, .08f, .23f, .92f, .77f);
            Surface(card, new Color32(247, 243, 225, 255));
            Bind(view, "_sceneMenuText", Text("FieldHelp", card, .07f, .40f, .93f, .94f, 38));
            HelpText(Ref<Text>(view, "_sceneMenuText"));
            Bind(view, "_sceneResume", Button("FieldResume", card, .08f, .22f, .92f, .35f));
            Bind(view, "_sceneReturn", Button("FieldReturn", card, .08f, .06f, .92f, .19f));
            Bind(view, "_sceneMenu", menu.gameObject); menu.gameObject.SetActive(false);
        }

        private static void RefineSiegeUi(SiegeFieldView view)
        {
            var status = Ref<Text>(view, "_status");
            Place(status.rectTransform, .025f, .82f, .31f, .945f);
            status.alignment = TextAnchor.MiddleLeft;
            Tint(status.rectTransform);
            Place((RectTransform)Ref<Button>(view, "_pause").transform, .72f, .89f, .97f, .945f);
            var intent = Ref<Text>(view, "_intent");
            intent.transform.SetParent(Ref<SiegeBattlefield>(view, "_field").transform.parent, false);
            Place(intent.rectTransform, .56f, .695f, .98f, .775f);
            Tint(intent.rectTransform);
            Place((RectTransform)Ref<Slider>(view, "_playerBar").transform, .04f, .105f, .48f, .114f);
            Place((RectTransform)Ref<Slider>(view, "_animalBar").transform, .52f, .105f, .96f, .114f);
            var hint = Ref<Text>(view, "_hint");
            Place(hint.rectTransform, .04f, .12f, .70f, .175f); Tint(hint.rectTransform);
            var detail = Ref<Text>(view, "_detail");
            Place(detail.rectTransform, .08f, .23f, .92f, .275f);
            var menu = Ref<GameObject>(view, "_pauseMenu");
            var card = Ref<Text>(view, "_pauseInfo").transform.parent;
            Place((RectTransform)card, .08f, .20f, .92f, .80f);
            Place(Ref<Text>(view, "_pauseInfo").rectTransform, .07f, .34f, .93f, .94f);
            HelpText(Ref<Text>(view, "_pauseInfo"));
            Place((RectTransform)Ref<Button>(view, "_resume").transform, .08f, .19f, .92f, .30f);
            Place((RectTransform)Ref<Button>(view, "_retry").transform, .08f, .05f, .92f, .16f);
            menu.transform.SetAsLastSibling();
        }

        private static void Tint(RectTransform rect)
        {
            var outline = rect.GetComponent<Outline>();
            if (outline == null) outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(255, 252, 235, 255);
            outline.effectDistance = new Vector2(2, -2);
        }

        private static void HelpText(Text text)
        {
            text.alignment = TextAnchor.UpperLeft;
            text.fontSize = text.resizeTextMaxSize = 38;
            text.resizeTextMinSize = 32;
            text.lineSpacing = 1.1f;
        }
        private static RectTransform WrapLegacy(Transform safe, Transform scene = null)
        {
            var existing = safe.Cast<Transform>().SingleOrDefault(child => child.name == "LegacyContent");
            if (existing != null) return (RectTransform)existing;
            var children = safe.Cast<Transform>().Where(child => child != scene).ToArray();
            var group = Rect("LegacyContent", safe);
            var layout = safe.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                EditorUtility.CopySerialized(layout, group.gameObject.AddComponent<VerticalLayoutGroup>());
                UnityEngine.Object.DestroyImmediate(layout);
            }
            foreach (var child in children) child.SetParent(group, false);
            return group;
        }
        private static void ImportAnimal(string animal)
        {
            string path = "Assets/Art/Outing/T_Outing_" + animal + "Natural.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = 256; importer.mipmapEnabled = false; importer.isReadable = false;
            importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        private static void Choice()
        {
            var root = PrefabUtility.LoadPrefabContents(ChoicePath);
            try
            {
                var cell = root.GetComponent<SiegeChoiceCell>();
                if (Ref<SiegePropIcon>(cell, "_art") == null)
                {
                    var art = Rect("ScenePropIcon", root.transform, .05f, .25f, .30f, .78f)
                        .gameObject.AddComponent<SiegePropIcon>();
                    art.raycastTarget = false; art.gameObject.SetActive(false); Bind(cell, "_art", art);
                }
                PrefabUtility.SaveAsPrefabAsset(root, ChoicePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void Approach()
        {
            var root = PrefabUtility.LoadPrefabContents(ApproachPath);
            try
            {
                var view = root.GetComponent<ApproachPanelView>();
                if (Ref<OutingFieldView>(view, "_scene") != null) throw new
                    InvalidOperationException("Yard field already authored; edit saved prefab.");
                var safe = root.GetComponentInChildren<SafeAreaView>(true).transform;
                var scene = Scene(root, safe, out var frame, out var grid, out var animal);
                Bind(view, "_scene", scene);
                Ref<Image>(scene, "_background").name = "LocationBackground";
                var original = Ref<ApproachBoardView>(view, "_board");
                var board = UnityEngine.Object.Instantiate(original, grid, false);
                board.name = "SceneBoard";
                foreach (var image in
                    board.GetComponentsInChildren<Image>(true).Where(image => image.name == "LocationBackground"))
                    UnityEngine.Object.DestroyImmediate(image.gameObject);
                UnityEngine.Object.DestroyImmediate(board.GetComponent<AspectRatioFitter>());
                Place((RectTransform)board.transform, 0, 0, 1, 1);
                board.GetComponent<Image>().color = Color.clear;
                Bind(view, "_sceneBoard", board);
                var placement = root.AddComponent<FieldPanelPlacement>();
                var serialized = new SerializedObject(placement); var list = serialized.FindProperty("_placements");
                var fields = new[]
                    {"_title","_instruction","_seed","_summary","_feedback","_pause","_undo","_return","_continue"};
                var bounds = new[] {new Vector4(.04f,.94f,.72f,.985f),new Vector4(.05f,.865f,.95f,.93f),
                    new Vector4(0,0,0,0),new Vector4(.05f,.145f,.95f,.175f),new Vector4(.05f,.09f,.95f,.14f),
                    new Vector4(.77f,.94f,.96f,.985f),new Vector4(.04f,.02f,.30f,.078f),
                    new Vector4(.33f,.02f,.60f,.078f),new Vector4(.62f,.02f,.96f,.078f)};
                list.arraySize = fields.Length + 1;
                for (int i = 0; i < fields.Length; i++)
                    Placement(list.GetArrayElementAtIndex(i),
                        ((Component)Ref<UnityEngine.Object>(view, fields[i])).transform as RectTransform, bounds[i]);
                var tools = (RectTransform)Ref<ApproachCommandView>(view, "_litter").transform.parent;
                Placement(list.GetArrayElementAtIndex(fields.Length), tools, new Vector4(.32f, .02f, .96f, .078f));
                serialized.ApplyModifiedPropertiesWithoutUndo(); Bind(view, "_sceneChrome", placement);
                PrefabUtility.SaveAsPrefabAsset(root, ApproachPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void Placement(SerializedProperty item, RectTransform rect, Vector4 bounds)
        {
            item.FindPropertyRelative("Target").objectReferenceValue = rect;
            item.FindPropertyRelative("LegacyMin").vector2Value = rect.anchorMin;
            item.FindPropertyRelative("LegacyMax").vector2Value = rect.anchorMax;
            item.FindPropertyRelative("LegacyOffsetMin").vector2Value = rect.offsetMin;
            item.FindPropertyRelative("LegacyOffsetMax").vector2Value = rect.offsetMax;
            item.FindPropertyRelative("SceneMin").vector2Value = new Vector2(bounds.x, bounds.y);
            item.FindPropertyRelative("SceneMax").vector2Value = new Vector2(bounds.z, bounds.w);
        }
        private static void Siege()
        {
            var root = PrefabUtility.LoadPrefabContents(SiegePath);
            try
            {
                var legacy = root.GetComponent<SiegeTrialView>();
                if (Ref<SiegeFieldView>(legacy, "_scene") != null) throw new
                    InvalidOperationException("Yard field already authored; edit saved prefab.");
                var safe = root.GetComponentInChildren<SafeAreaView>(true).transform;
                // The old column owns its layout group; preserve that complete subtree.
                var old = Ref<SiegeBattlefield>(legacy, "_field");
                Bind(legacy, "_legacyContent", WrapLegacy(safe).gameObject);
                var sceneRoot = Rect("SceneControls", safe); sceneRoot.gameObject.SetActive(false);
                var input = sceneRoot.gameObject.AddComponent<SiegeFieldView>(); Bind(legacy, "_scene", input);
                var scene = Scene(root, sceneRoot, out var frame, out var grid, out var animal);
                var field = UnityEngine.Object.Instantiate(old, frame, false); field.name = "SceneBattlefield";
                var le = field.GetComponent<LayoutElement>(); if (le != null) UnityEngine.Object.DestroyImmediate(le);
                var fit = field.GetComponent<AspectRatioFitter>(); if (fit != null)
                    UnityEngine.Object.DestroyImmediate(fit);
                Place(field.rectTransform, 0, 0, 1, 1);
                UnityEngine.Object.DestroyImmediate(animal.gameObject);
                Bind(scene, "_animal", Ref<Image>(field, "_cat")); Bind(input, "_field", field);
                // Portals and context share exactly the scene coordinate frame.
                foreach (var item in new[] { ("_top", "Top"), ("_mid", "Mid"), ("_bottom", "Bottom") })
                {
                    var button = Button(item.Item2, frame, 0, 0, 0, 0);
                    ((RectTransform)button.transform).sizeDelta = new Vector2(120, 96); Bind(input, item.Item1, button);
                }
                var context = Rect("Context", frame); context.sizeDelta = new Vector2(470, 100);
                var layout = context.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 10;
                layout.childForceExpandWidth = layout.childControlWidth = true;
                layout.childForceExpandHeight = layout.childControlHeight = true; Bind(input, "_context", context);
                foreach (var item in new[] { ("_merge", "Merge"), ("_reroute", "Reroute"), ("_discard", "Discard") })
                {
                    var button = Button(item.Item2, context, 0, 0, 1, 1);
                    button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1; Bind(input, item.Item1, button);
                }
                Bind(input, "_status", Text("Status", sceneRoot, .04f, .94f, .76f, .985f, 36));
                Bind(input, "_pause", Button("Pause", sceneRoot, .79f, .94f, .96f, .985f));
                Bind(input, "_intent", Text("Intent", sceneRoot, .04f, .89f, .96f, .93f, 32));
                Bind(input, "_playerBar", Bar("PlayerCharge", sceneRoot, .04f, .867f, .48f, .878f, new
                    Color32(100, 162, 134, 255)));
                Bind(input, "_animalBar", Bar("AnimalCharge", sceneRoot, .52f, .867f, .96f, .878f, new
                    Color32(202, 142, 101, 255)));
                Bind(input, "_detail", Text("Detail", sceneRoot, .05f, .145f, .95f, .17f, 27));
                Bind(input, "_hint", Text("Hint", sceneRoot, .04f, .10f, .96f, .145f, 30));
                var hand = Rect("Hand", sceneRoot, .04f, .022f, .70f, .094f);
                var handLayout = hand.gameObject.AddComponent<HorizontalLayoutGroup>(); handLayout.spacing = 10;
                handLayout.childForceExpandWidth = false; handLayout.childControlWidth = true;
                handLayout.childControlHeight = handLayout.childForceExpandHeight = true; Bind(input, "_hand", hand);
                Bind(input, "_cellPrefab",
                    AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePath).GetComponent<SiegeChoiceCell>());
                Bind(input, "_submit", Button("Submit", sceneRoot, .73f, .022f, .96f, .094f));
                Bind(input, "_clear", Button("Clear", sceneRoot, .76f, .175f, .96f, .222f));
                var ghost = Rect("DragGhost", sceneRoot, 0, 0, 0, 0); ghost.sizeDelta = new Vector2(110, 110);
                var icon = ghost.gameObject.AddComponent<SiegePropIcon>(); icon.raycastTarget = false;
                Bind(input, "_dragGhost", ghost); Bind(input, "_dragIcon", icon); ghost.gameObject.SetActive(false);
                var menu = Rect("PauseMenu", sceneRoot); Surface(menu, new Color32(36, 52, 44, 180), true);
                var card = Rect("Card", menu, .12f, .34f, .88f, .66f); Surface(card, new Color32(247, 243, 225, 255));
                Bind(input, "_pauseInfo", Text("PauseInfo", card, .06f, .56f, .94f, .93f, 36));
                Bind(input, "_resume", Button("Resume", card, .07f, .30f, .93f, .49f));
                Bind(input, "_retry", Button("Retry", card, .07f, .06f, .93f, .25f));
                Bind(input, "_pauseMenu", menu.gameObject); menu.gameObject.SetActive(false);
                foreach (var node in sceneRoot.GetComponentsInChildren<Transform>(true))
                    node.name = "Field" + node.name;
                Ref<Image>(scene, "_background").name = "LocationBackground";
                PrefabUtility.SaveAsPrefabAsset(root, SiegePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
