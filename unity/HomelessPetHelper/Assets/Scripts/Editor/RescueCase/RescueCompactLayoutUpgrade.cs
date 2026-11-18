using System;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Editor.RescueCase
{
    // Additive Prefab Mode migration. Existing panel, scene, text and artwork bindings keep their GUIDs.
    public static class RescueCompactLayoutUpgrade
    {
        public const string CardPath = "Assets/Prefabs/Widgets/Widget_RescueActionBar.prefab";
        public const string BackdropPath = "Assets/EditorScenes/RescueCaseArt/T_Rescue_ShelterTemporary.jpg";
        private static readonly Color Ink = new Color32(49, 65, 58, 255);
        private static readonly Color Paper = new Color32(248, 252, 249, 255);

        public static void UpgradeSavedPrefab()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch snapshot.");
            ImportBackdrop();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CardPath) == null) AuthorCard();
            GameObject root = PrefabUtility.LoadPrefabContents(RescueCaseAssets.PanelPath);
            try
            {
                if (root.transform.Find("SafeArea/Observation") != null)
                    throw new InvalidOperationException("Compact layout already saved; edit that Prefab in place.");
                AuthorPanel(root);
                PrefabUtility.SaveAsPrefabAsset(root, RescueCaseAssets.PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void RefineSavedPrefab()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch snapshot.");
            GameObject card = PrefabUtility.LoadPrefabContents(CardPath);
            try
            {
                var progress = card.GetComponentsInChildren<Image>(true);
                foreach (Image image in progress)
                {
                    if (image.name != "HoldProgress") continue;
                    image.sprite = null;
                    image.type = Image.Type.Simple;
                    image.fillAmount = 0;
                    image.enabled = false;
                    Place(image.rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
                }
                PrefabUtility.SaveAsPrefabAsset(card, CardPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(card); }
            GameObject root = PrefabUtility.LoadPrefabContents(RescueCaseAssets.PanelPath);
            try
            {
                Transform safe = root.transform.Find("SafeArea");
                Transform scene = safe.Find("Observation");
                Text details = Field<Text>(root.GetComponent<RescueCaseView>(), "_details");
                LayoutElement fixedHeight = details.GetComponent<LayoutElement>();
                if (fixedHeight != null) Object.DestroyImmediate(fixedHeight);
                Place((RectTransform)scene.Find("Cat"), Vector2.zero, Vector2.one,
                    new Vector2(0, 0), new Vector2(0, -456));
                if (safe.Find("HeaderSurface") == null)
                {
                    Image header = Rect("HeaderSurface", safe).AddComponent<Image>();
                    header.transform.SetSiblingIndex(1);
                    Place(header.rectTransform, new Vector2(0, 1), Vector2.one,
                        new Vector2(0, -456), Vector2.zero);
                    header.color = new Color32(248, 252, 249, 235);
                    header.raycastTarget = false;
                }
                Style(safe.Find("Title").GetComponent<Text>(), 50, TextAnchor.MiddleLeft);
                foreach (Selectable command in safe.Find("SessionControls").GetComponentsInChildren<Selectable>(true))
                {
                    LayoutElement element = command.GetComponent<LayoutElement>();
                    element.minWidth = 0;
                    element.preferredWidth = -1;
                    element.flexibleWidth = 1;
                }
                PrefabUtility.SaveAsPrefabAsset(root, RescueCaseAssets.PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static void ImportBackdrop()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(BackdropPath);
            if (importer == null) throw new InvalidOperationException("Missing selected temporary backdrop.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static void AuthorCard()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(OptionPreviewAssets.CardPath);
            try
            {
                root.SetActive(false);
                root.name = "Widget_RescueActionBar";
                Object.DestroyImmediate(root.GetComponent<HorizontalLayoutGroup>());
                Height(root.transform, 120);
                var view = root.GetComponent<OptionCardView>();
                Text title = Field<Text>(view, "_title");
                Text identity = Field<Text>(view, "_identity");
                Toggle selection = Field<Toggle>(view, "_selection");
                Toggle retention = Field<Toggle>(view, "_retention");
                Image surface = Field<Image>(view, "_surface");
                foreach (LayoutGroup layout in root.GetComponentsInChildren<LayoutGroup>(true))
                    Object.DestroyImmediate(layout);
                RectTransform content = (RectTransform)title.transform.parent;
                Place(content, new Vector2(0, 0), Vector2.one, Vector2.zero, Vector2.zero);
                selection.gameObject.SetActive(false);
                // The hold surface excludes the independent retention control.
                GameObject action = Rect("HoldAction", root.transform);
                Place((RectTransform)action.transform, Vector2.zero, Vector2.one, new Vector2(132, 0), Vector2.zero);
                Image actionSurface = action.AddComponent<Image>();
                actionSurface.color = Color.white;
                var hold = action.AddComponent<HoldActionButton>();
                hold.targetGraphic = actionSurface;
                var colors = hold.colors;
                colors.highlightedColor = new Color32(236, 246, 239, 255);
                colors.pressedColor = new Color32(220, 239, 225, 255);
                colors.disabledColor = new Color32(225, 229, 226, 255);
                hold.colors = colors;
                Image progress = Rect("HoldProgress", action.transform).AddComponent<Image>();
                Place(progress.rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
                progress.color = new Color32(161, 209, 177, 170);
                progress.type = Image.Type.Simple;
                progress.enabled = false;
                progress.fillAmount = 0;
                progress.raycastTarget = false;
                Bind(hold, "_progress", progress);
                title.transform.SetParent(action.transform, false);
                Place(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 20), new Vector2(-20, -20));
                Style(title, 40, TextAnchor.MiddleCenter);
                identity.transform.SetParent(action.transform, false);
                Place(identity.rectTransform, Vector2.zero, new Vector2(1, 0.38f),
                    new Vector2(16, 2), new Vector2(-16, 0));
                Style(identity, 26, TextAnchor.MiddleCenter);
                identity.gameObject.SetActive(false);
                // Disabled actions retain a reason without changing the row's dimensions.
                title.resizeTextMinSize = 28;
                retention.transform.SetParent(root.transform, false);
                Place((RectTransform)retention.transform, Vector2.zero, new Vector2(0, 1),
                    Vector2.zero, new Vector2(120, 0));
                Image pinSurface = retention.GetComponent<Image>();
                if (pinSurface == null) pinSurface = retention.gameObject.AddComponent<Image>();
                pinSurface.color = new Color32(232, 242, 234, 255);
                retention.targetGraphic = pinSurface;
                Text pinLabel = retention.GetComponentInChildren<Text>(true);
                pinLabel.text = new OptionPreviewText(AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/EditorScenes/OptionPreviewText.json")).Get("retain");
                Place(pinLabel.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(8, 0), new Vector2(-8, -28));
                Style(pinLabel, 30, TextAnchor.MiddleCenter);
                var indicator = (RectTransform)retention.graphic.transform;
                Place(indicator, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(-20, 12), new Vector2(20, 22));
                retention.graphic.color = new Color32(63, 126, 85, 255);
                surface.color = Color.white;
                Bind(view, "_hold", hold);
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, CardPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void AuthorPanel(GameObject root)
        {
            root.GetComponent<CanvasScaler>().matchWidthOrHeight = 0;
            root.GetComponent<Image>().color = Paper;
            var panel = root.GetComponent<OptionPanelView>();
            var view = root.GetComponent<RescueCaseView>();
            Transform safe = root.transform.Find("SafeArea");
            Object.DestroyImmediate(safe.GetComponent<VerticalLayoutGroup>());
            GameObject scene = Rect("Observation", safe);
            scene.transform.SetAsFirstSibling();
            Place((RectTransform)scene.transform, Vector2.zero, Vector2.one, new Vector2(0, 576), Vector2.zero);
            scene.AddComponent<RectMask2D>();
            Image backdrop = Rect("Backdrop", scene.transform).AddComponent<Image>();
            backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropPath);
            backdrop.raycastTarget = false;
            Place(backdrop.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fit = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1;
            Image cat = Rect("Cat", scene.transform).AddComponent<Image>();
            cat.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/EditorScenes/StationCareArt/T_Station_CatTemporary.png");
            cat.raycastTarget = false;
            cat.preserveAspect = true;
            Place(cat.rectTransform, new Vector2(0.52f, 0.26f), new Vector2(0.52f, 0.26f),
                new Vector2(-220, -220), new Vector2(220, 220));

            Transform title = safe.Find("Title");
            AtTop(title, 24, 64); Style(title.GetComponent<Text>(), 44, TextAnchor.MiddleLeft);
            Transform status = safe.Find("Status");
            AtTop(status, 194, 48);
            foreach (Text text in status.GetComponentsInChildren<Text>(true))
                Style(text, 32, TextAnchor.MiddleLeft);
            AtTop(Field<Text>(view, "_animal").transform, 258, 92);
            Style(Field<Text>(view, "_animal"), 34, TextAnchor.UpperLeft);
            AtTop(Field<Text>(view, "_intent").transform, 360, 96);
            Style(Field<Text>(view, "_intent"), 32, TextAnchor.UpperLeft);
            // Reuse commands and bindings; only their saved parents and geometry change.
            Transform session = safe.Find("SessionControls");
            AtTop(session, 96, 80);
            Transform help = safe.Find("Help");
            foreach (Transform child in new[] { Field<Button>(view, "_handbook").transform,
                Field<Button>(view, "_assistance").transform }) child.SetParent(session, false);
            help.gameObject.SetActive(false);
            var sessionRow = session.GetComponent<HorizontalLayoutGroup>();
            sessionRow.spacing = 12;
            sessionRow.childForceExpandWidth = true;
            sessionRow.childControlWidth = true;
            sessionRow.childControlHeight = true;
            foreach (Selectable command in session.GetComponentsInChildren<Selectable>(true))
            {
                Height(command.transform, 80);
                foreach (Text text in command.GetComponentsInChildren<Text>(true))
                    Style(text, 30, TextAnchor.MiddleCenter);
            }

            GameObject dock = Rect("ActionDock", safe);
            Place((RectTransform)dock.transform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 576));
            dock.AddComponent<Image>().color = Paper;
            Text feedback = Field<Text>(panel, "_feedback");
            feedback.transform.SetParent(dock.transform, false);
            AtTop(feedback.transform, 12, 100);
            Style(feedback, 34, TextAnchor.MiddleCenter);
            Text hint = Rect("HoldHint", dock.transform).AddComponent<Text>();
            AtTop(hint.transform, 120, 40); Style(hint, 29, TextAnchor.MiddleCenter);
            Bind(view, "_holdHint", hint);
            ScrollRect list = Field<ScrollRect>(panel, "_scroll");
            list.transform.SetParent(dock.transform, false);
            AtTop(list.transform, 168, 384);
            list.GetComponent<Image>().color = Color.clear;
            list.inertia = false;
            var listLayout = list.content.GetComponent<VerticalLayoutGroup>();
            listLayout.spacing = 12;
            listLayout.padding = new RectOffset(0, 0, 0, 0);
            listLayout.childControlHeight = true;
            listLayout.childForceExpandHeight = false;
            Bind(panel, "_cardPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(CardPath)
                .GetComponent<OptionCardView>());
            var fields = new SerializedObject(panel);
            fields.FindProperty("_holdToConfirm").boolValue = true;
            fields.ApplyModifiedPropertiesWithoutUndo();
            Field<Button>(panel, "_use").gameObject.SetActive(false);
            Button advance = Field<Button>(view, "_advance");
            advance.transform.SetParent(dock.transform, false);
            Place((RectTransform)advance.transform, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(32, 24), new Vector2(-32, 144));
            Style(advance.GetComponentInChildren<Text>(true), 38, TextAnchor.MiddleCenter);
            AddDetails(scene.transform, view);
        }

        private static void AddDetails(Transform scene, RescueCaseView view)
        {
            var box = Rect("DetailsScroll", scene);
            Place((RectTransform)box.transform, new Vector2(0, 0.08f), new Vector2(1, 1),
                new Vector2(32, 0), new Vector2(-32, -470));
            box.AddComponent<Image>().color = new Color32(248, 252, 249, 247);
            box.AddComponent<RectMask2D>();
            ScrollRect scroll = box.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = (RectTransform)box.transform;
            Text details = Field<Text>(view, "_details");
            details.transform.SetParent(box.transform, false);
            Place(details.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, 0), new Vector2(-24, 0));
            details.rectTransform.pivot = new Vector2(0.5f, 1);
            Style(details, 34, TextAnchor.UpperLeft);
            details.resizeTextForBestFit = false;
            details.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = details.rectTransform;
            Bind(view, "_detailsScroll", scroll);
        }

        private static GameObject Rect(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result;
        }

        private static void AtTop(Transform target, float top, float height) => Place((RectTransform)target,
            new Vector2(0, 1), Vector2.one, new Vector2(32, -top - height), new Vector2(-32, -top));

        private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = low; rect.offsetMax = high;
        }

        private static void Height(Transform target, float height)
        {
            var element = target.GetComponent<LayoutElement>();
            if (element == null) element = target.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height; element.preferredHeight = height; element.flexibleHeight = 0;
            element.flexibleWidth = 1;
        }

        private static void Style(Text text, int size, TextAnchor alignment)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.color = Ink; text.alignment = alignment;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = size - 4; text.resizeTextMaxSize = size;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        private static T Field<T>(Object target, string field) where T : Object =>
            (T)new SerializedObject(target).FindProperty(field).objectReferenceValue;

        private static void Bind(Object target, string field, Object value)
        {
            var fields = new SerializedObject(target);
            fields.FindProperty(field).objectReferenceValue = value;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
