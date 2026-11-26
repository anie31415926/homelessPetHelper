using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.StationPreview
{
    /// <summary>
    /// Isolated Prefab Mode pass for the station handbook. Rebuilds only the menu page bodies,
    /// keeps every gameplay binding on the existing entry buttons, and leaves unrelated nodes untouched.
    /// </summary>
    public static class StationHandbookAuthor
    {
        private const string PrefabPath = "Assets/Prefabs/Panels/Panel_StationLoop.prefab";
        private const string AtlasPath = "Assets/Art/UI/T_UI_LevelControls.png";
        private const string GearPath = "Assets/Art/UI/T_UI_Gear.png";
        private const string FontPath = "Assets/Art/Fonts/PetRound-Game.ttf";
        private static readonly Color Ink = new Color32(42, 61, 48, 255);
        private static readonly Color Muted = new Color32(88, 104, 92, 200);
        private static Dictionary<string, Sprite> _sprites;
        private static Font _font;

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use StationHandbook.");
            _sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);
            Sprite gear = AssetDatabase.LoadAssetAtPath<Sprite>(GearPath);
            if (gear != null) _sprites["Gear"] = gear;
            _font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (_font == null) throw new InvalidOperationException("Missing handbook font.");

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Edit(GameObject root)
        {
            Transform menu = Find(root.transform, "MenuItems");
            if (menu == null) throw new InvalidOperationException("Missing MenuItems.");
            SetVertical(menu.GetComponent<VerticalLayoutGroup>(), 16, 48);

            Transform heading = Find(menu, "MenuHeading");
            Text title = heading.GetComponentInChildren<Text>(true);
            title.text = "救助站手账";
            title.fontSize = 48;
            title.lineSpacing = 1;

            // The old clock line and capacity line collapse into one quiet subtitle.
            Transform clock = Find(menu, "Clock");
            if (clock != null) clock.gameObject.SetActive(false);
            Transform capacity = Find(menu, "Capacity");
            Text status = capacity.GetComponentInChildren<Text>(true);
            status.fontSize = 34; status.alignment = TextAnchor.MiddleLeft; status.color = Muted;
            status.lineSpacing = 1; status.verticalOverflow = VerticalWrapMode.Overflow;
            status.text = "临时安置  3/4 · 1年1月1日 00:00";
            Layout(capacity.gameObject, -1, 44, -1, 44);

            Transform tabBar = Find(menu, "TabBar");
            HorizontalLayoutGroup bar = tabBar.GetComponent<HorizontalLayoutGroup>();
            SetHorizontal(bar, 0, 0);
            bar.childAlignment = TextAnchor.MiddleCenter;
            Layout(tabBar.gameObject, -1, 144, -1, 144);
            Image strip = tabBar.gameObject.GetComponent<Image>() ?? Surface(tabBar.gameObject, "Secondary");
            strip.raycastTarget = false;
            strip.enabled = false;
            TabIcon(tabBar, "TabStation", "Home");
            TabIcon(tabBar, "TabEconomy", "Objectives");
            TabIcon(tabBar, "TabMemorial", "Paw");
            TabIcon(tabBar, "TabSettings", "Gear");

            Transform stationPage = Find(menu, "PageStation");
            Transform economyPage = Find(menu, "PageEconomy");
            Transform memorialPage = Find(menu, "PageMemorial");
            Transform settingsPage = Find(menu, "PageSettings");
            Text[] pageTitles =
            {
                EnsureHeading(stationPage, "站务"),
                EnsureHeading(economyPage, "建设"),
                EnsureHeading(memorialPage, "纪念"),
                EnsureHeading(settingsPage, "设置")
            };

            AlignRow(Find(stationPage, "History"), 104);
            RowIcon(Find(stationPage, "History"), "Objectives");
            Transform pause = Find(stationPage, "Pause");
            AlignRow(pause, 104);
            if (pause != null)
            {
                pause.GetComponentInChildren<Text>(true).text = "暂停";
                DestroyChild(Find(pause, "ToggleMotionVisual"), "ToggleBox");
                DestroyChild(Find(pause, "ToggleMotionVisual"), "Indicator");
                GameObject pauseIcon = RowIcon(pause, "Pause", 52, null, "IconPause");
                GameObject continueIcon = RowIcon(pause, "Play", 52, null, "IconPlay");
                if (continueIcon != null) continueIcon.SetActive(false);
                if (pauseIcon != null) pauseIcon.SetActive(true);
            }
            AlignRow(Find(settingsPage, "StationSettings"), 104);
            RowIcon(Find(settingsPage, "StationSettings"), "Gear", 44);

            EconomyPage(economyPage);
            MemorialPage(memorialPage);

            Sheet(menu, title, status, pageTitles);
        }

        private static void EconomyPage(Transform page)
        {
            DestroyChild(page, "StationEconomy");
            DestroyChild(page, "EconomyAid");
            DestroyChild(page, "EconomyList");
            DestroyChild(page, "EconomyEmpty");

            Text aid = Label(page, "EconomyAid", 32, Muted, TextAnchor.MiddleLeft);
            aid.text = "援助金 320";
            aid.rectTransform.sizeDelta = new Vector2(0, 44);
            Layout(aid.gameObject, -1, 44, -1, 44);

            RectTransform list = Rect(page, "EconomyList");
            VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            SetVertical(layout, 12, 0);

            GameObject template = RowTemplate(list, "EconomyOfferTemplate");
            template.SetActive(false);

            Text empty = Label(page, "EconomyEmpty", 32, Muted, TextAnchor.MiddleCenter);
            empty.text = "还没有可以建设的项目。";
            empty.rectTransform.sizeDelta = new Vector2(0, 96);
            Layout(empty.gameObject, -1, 96, -1, 96);
        }

        private static void MemorialPage(Transform page)
        {
            DestroyChild(page, "StationMemorial");
            DestroyChild(page, "MemorialScroll");
            DestroyChild(page, "MemorialEmpty");

            RectTransform scroll = Rect(page, "MemorialScroll");
            scroll.sizeDelta = new Vector2(0, 440);
            Layout(scroll.gameObject, -1, 440, -1, 440);
            ScrollRect view = scroll.gameObject.AddComponent<ScrollRect>();
            view.horizontal = false; view.vertical = true; view.scrollSensitivity = 30;
            view.movementType = ScrollRect.MovementType.Clamped;

            RectTransform port = Rect(scroll, "Viewport");
            Stretch(port);
            port.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Rect(port, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 216);
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(312, 240);
            grid.spacing = new Vector2(16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject card = MemorialTemplate(content);
            card.SetActive(false);

            Text empty = Label(page, "MemorialEmpty", 32, Muted, TextAnchor.MiddleCenter);
            empty.text = "送养完成后，这里会留下它的名字和样子。";
            empty.rectTransform.sizeDelta = new Vector2(0, 120);
            Layout(empty.gameObject, -1, 120, -1, 120);

            view.viewport = port;
            view.content = content;
        }

        private static GameObject RowTemplate(Transform parent, string name)
        {
            RectTransform row = Rect(parent, name);
            Layout(row.gameObject, -1, 144, -1, 144);
            Image paper = Surface(row.gameObject, "Secondary");
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = paper;

            RectTransform icon = Rect(row, "Icon");
            icon.anchorMin = new Vector2(0, 0.5f);
            icon.anchorMax = new Vector2(0, 0.5f);
            icon.pivot = new Vector2(0, 0.5f);
            icon.anchoredPosition = new Vector2(32, 0);
            icon.sizeDelta = new Vector2(64, 64);
            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = _sprites["Objectives"];
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            Text label = Label(row, "Label", 40, Ink, TextAnchor.MiddleLeft);
            label.rectTransform.anchorMin = new Vector2(0, 0);
            label.rectTransform.anchorMax = new Vector2(0.62f, 1);
            label.rectTransform.offsetMin = new Vector2(116, 8);
            label.rectTransform.offsetMax = new Vector2(0, -8);
            label.lineSpacing = 1;

            Text state = Label(row, "State", 28, Muted, TextAnchor.MiddleRight);
            state.rectTransform.anchorMin = new Vector2(0.55f, 0);
            state.rectTransform.anchorMax = new Vector2(1, 1);
            state.rectTransform.offsetMin = new Vector2(8, 8);
            state.rectTransform.offsetMax = new Vector2(-32, -8);

            StationHandbookOfferView view = row.gameObject.AddComponent<StationHandbookOfferView>();
            Bind(view, "_surface", paper, "_icon", iconImage, "_label", label, "_state", state,
                "_button", button, "_availablePaper", _sprites["Secondary"], "_ownedPaper", _sprites["Secondary"]);
            return row.gameObject;
        }

        private static GameObject MemorialTemplate(Transform parent)
        {
            RectTransform card = Rect(parent, "MemorialCardTemplate");
            Layout(card.gameObject, 312, 240, 312, 240);
            Image paper = Surface(card.gameObject, "Secondary");
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = paper;

            RectTransform portrait = Rect(card, "Portrait");
            portrait.anchorMin = new Vector2(0.5f, 1);
            portrait.anchorMax = new Vector2(0.5f, 1);
            portrait.pivot = new Vector2(0.5f, 1);
            portrait.anchoredPosition = new Vector2(0, -16);
            portrait.sizeDelta = new Vector2(136, 136);
            Image portraitImage = portrait.gameObject.AddComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            Text label = Label(card, "Label", 28, Ink, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = new Vector2(0, 0);
            label.rectTransform.anchorMax = new Vector2(1, 0);
            label.rectTransform.pivot = new Vector2(0.5f, 0);
            label.rectTransform.anchoredPosition = new Vector2(0, 10);
            label.rectTransform.sizeDelta = new Vector2(-16, 48);
            label.lineSpacing = 1;

            StationHandbookMemoryView view = card.gameObject.AddComponent<StationHandbookMemoryView>();
            Bind(view, "_portrait", portraitImage, "_label", label, "_button", button);
            return card.gameObject;
        }

        private static void Sheet(Transform menu, Text title, Text status, Text[] pageTitles)
        {
            StationHandbookSheet sheet = menu.GetComponent<StationHandbookSheet>();
            if (sheet == null) throw new InvalidOperationException("Missing StationHandbookSheet.");
            Button[] tabs = Enumerable.Range(1, 4)
                .Select(index => Find(menu, "TabStation|TabEconomy|TabMemorial|TabSettings".Split('|')[index - 1])
                    .GetComponent<Button>()).ToArray();
            Image[] surfaces = tabs.Select(tab => tab.GetComponent<Image>()).ToArray();
            Text[] labels = tabs.Select(tab => tab.GetComponentInChildren<Text>(true)).ToArray();
            GameObject[] pages = new[] { "PageStation", "PageEconomy", "PageMemorial", "PageSettings" }
                .Select(name => Find(menu, name).gameObject).ToArray();
            Transform list = Find(menu, "EconomyList");
            Transform grid = Find(menu, "PageMemorial/MemorialScroll/Viewport/Content");

            var so = new SerializedObject(sheet);
            Array(so, "_tabs", tabs); Array(so, "_tabSurfaces", surfaces); Array(so, "_tabLabels", labels);
            Array(so, "_pages", pages); Array(so, "_pageTitles", pageTitles);
            Object(so, "_title", title); Object(so, "_status", status);
            Object(so, "_idlePaper", _sprites["Secondary"]); Object(so, "_selectedPaper", _sprites["Action"]);
            Object(so, "_aid", Find(menu, "PageEconomy/EconomyAid").GetComponent<Text>());
            Object(so, "_offerList", list);
            Object(so, "_offerTemplate", Find(menu, "PageEconomy/EconomyList/EconomyOfferTemplate")
                .GetComponent<StationHandbookOfferView>());
            Object(so, "_offerEmpty", Find(menu, "PageEconomy/EconomyEmpty").GetComponent<Text>());
            Array(so, "_offerIcons", new[]
            {
                (UnityEngine.Object)_sprites["Home"], _sprites["Paw"], _sprites["Play"]
            });
            Object(so, "_memoryGrid", grid);
            Object(so, "_memoryScroll", Find(menu, "PageMemorial/MemorialScroll").gameObject);
            Object(so, "_memoryTemplate", Find(menu, "PageMemorial/MemorialScroll/Viewport/Content/MemorialCardTemplate")
                .GetComponent<StationHandbookMemoryView>());
            Object(so, "_memoryEmpty", Find(menu, "PageMemorial/MemorialEmpty").GetComponent<Text>());
            Object(so, "_pause", Find(menu, "PageStation/Pause").GetComponent<Toggle>());
            Object(so, "_pauseLabel", Find(menu, "PageStation/Pause").GetComponentInChildren<Text>(true));
            Object(so, "_pauseIcon", Find(menu, "PageStation/Pause/ToggleMotionVisual/IconPause").gameObject);
            Object(so, "_continueIcon", Find(menu, "PageStation/Pause/ToggleMotionVisual/IconPlay").gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Text EnsureHeading(Transform page, string text)
        {
            DestroyChild(page, "PageHeading");
            Text heading = Label(page, "PageHeading", 34, new Color(Ink.r, Ink.g, Ink.b, .72f),
                TextAnchor.MiddleLeft);
            heading.text = text;
            heading.rectTransform.sizeDelta = new Vector2(0, 40);
            Layout(heading.gameObject, -1, 40, -1, 40);
            RectTransform rule = Rect(heading.transform, "Rule");
            rule.anchorMin = new Vector2(0, 0);
            rule.anchorMax = new Vector2(1, 0);
            rule.pivot = new Vector2(0.5f, 0);
            rule.anchoredPosition = new Vector2(0, -4);
            rule.sizeDelta = new Vector2(0, 2);
            Image line = rule.gameObject.AddComponent<Image>();
            line.color = new Color(Ink.r, Ink.g, Ink.b, .16f);
            line.raycastTarget = false;
            heading.transform.SetAsFirstSibling();
            return heading;
        }

        private static void TabIcon(Transform tabBar, string tab, string sprite)
        {
            Transform target = Find(tabBar, tab);
            DestroyChild(target, "Icon");
            RectTransform icon = Rect(target, "Icon");
            icon.anchorMin = new Vector2(0.5f, 1);
            icon.anchorMax = new Vector2(0.5f, 1);
            icon.pivot = new Vector2(0.5f, 1);
            icon.anchoredPosition = new Vector2(0, -14);
            icon.sizeDelta = new Vector2(44, 44);
            Image image = icon.gameObject.AddComponent<Image>();
            image.sprite = _sprites[sprite];
            image.preserveAspect = true;
            image.raycastTarget = false;
            RectTransform label = (RectTransform)target.Find("Label");
            label.offsetMin = new Vector2(8, 10);
            label.offsetMax = new Vector2(-8, -62);
            label.GetComponent<Text>().fontSize = 34;
        }

        private static GameObject RowIcon(Transform row, string sprite, float size = 52,
            Color? tint = null, string name = "Icon")
        {
            if (row == null) return null;
            Transform host = Find(row, "MotionVisual") ?? Find(row, "ToggleMotionVisual") ?? row;
            DestroyChild(row, name);
            DestroyChild(host, name);
            RectTransform icon = Rect(host, name);
            icon.anchorMin = new Vector2(0, 0.5f);
            icon.anchorMax = new Vector2(0, 0.5f);
            icon.pivot = new Vector2(0, 0.5f);
            icon.anchoredPosition = new Vector2(host == row ? 36 : 24, 0);
            icon.sizeDelta = new Vector2(size, size);
            Image image = icon.gameObject.AddComponent<Image>();
            image.sprite = _sprites[sprite];
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (tint.HasValue) image.color = tint.Value;
            return icon.gameObject;
        }

        private static void AlignRow(Transform row, float left)
        {
            if (row == null) return;
            Text label = row.GetComponentInChildren<Text>(true);
            if (label == null) return;
            label.alignment = TextAnchor.MiddleLeft;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(left, 8);
            rect.offsetMax = new Vector2(-32, -8);
            label.fontSize = 44;
            label.lineSpacing = 1;
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform old = parent.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }

        private static Transform Find(Transform root, string path)
        {
            if (!path.Contains("/"))
                return root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(child => child != root && child.name == path);
            string[] parts = path.Split('/');
            Transform current = root;
            foreach (string part in parts)
            {
                current = current.Cast<Transform>().FirstOrDefault(child => child.name == part);
                if (current == null) return null;
            }
            return current;
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image Surface(GameObject target, string sprite)
        {
            Image image = target.AddComponent<Image>();
            image.sprite = _sprites[sprite];
            image.type = Image.Type.Sliced;
            return image;
        }

        private static Text Label(Transform parent, string name, int size, Color color, TextAnchor anchor)
        {
            RectTransform rect = Rect(parent, name);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.lineSpacing = 1;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Layout(GameObject target, float minWidth, float minHeight,
            float preferredWidth, float preferredHeight)
        {
            LayoutElement element = target.GetComponent<LayoutElement>() ??
                target.AddComponent<LayoutElement>();
            element.minWidth = minWidth;
            element.minHeight = minHeight;
            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = 0;
            element.flexibleHeight = 0;
        }

        private static void SetVertical(VerticalLayoutGroup group, int spacing, int padding)
        {
            if (group == null) throw new InvalidOperationException("Missing vertical layout.");
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
        }

        private static void SetHorizontal(HorizontalLayoutGroup group, int spacing, int padding)
        {
            if (group == null) throw new InvalidOperationException("Missing horizontal layout.");
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, 0, 0);
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;
        }

        private static void Bind(UnityEngine.Object target, params object[] pairs)
        {
            var so = new SerializedObject(target);
            for (int index = 0; index < pairs.Length; index += 2)
                Object(so, (string)pairs[index], (UnityEngine.Object)pairs[index + 1]);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Object(SerializedObject so, string field, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) throw new InvalidOperationException("Missing field " + field);
            property.objectReferenceValue = value;
        }

        private static void Array(SerializedObject so, string field, UnityEngine.Object[] values)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) throw new InvalidOperationException("Missing field " + field);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }
    }
}
