using System;
using System.IO;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.Siege
{
    public static class SiegeTrialAssets
    {
        public const string ScenePath = "Assets/EditorScenes/Siege/SC_SiegeTrial.unity";
        public const string PanelPath = "Assets/Prefabs/Panels/Panel_SiegeTrial.prefab";
        public const string CellPath = "Assets/Prefabs/Widgets/Widget_SiegeChoice.prefab";
        public const string DataPath = "Assets/EditorScenes/Siege/SiegeTrialData.json";
        public const string TextPath = "Assets/EditorScenes/Siege/SiegeTrialText.json";
        private const string CatPath = "Assets/EditorScenes/CityMapArt/Animals/T_Animal_CatOrange.png";
        private static readonly Color Ink = new Color32(32, 36, 40, 255);
        private static readonly Color Surface = new Color32(242, 243, 244, 255);

        [MenuItem("Homeless Pet Helper/Preview/Open Reverse Defense Trial")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void CreateAssets()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Author new trial assets in an isolated batch project.");
            foreach (string path in new[] { ScenePath, PanelPath, CellPath })
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException(
                        "Edit saved siege assets in Prefab Mode; creation refuses overwrites."
                    );
            var c = new SiegeCatalog(
                JsonUtility.FromJson<SiegeCatalogData>(AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath).text)
            );
            var text = new SiegeText(AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath));
            text.Validate(c);
            var cat = AssetDatabase.LoadAssetAtPath<Sprite>(CatPath);
            if (cat == null)
                throw new InvalidOperationException("Missing existing temporary orange cat sprite.");
            var cell = CreateCell();
            CreatePanel(cell, text, cat);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath));
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var camera = new GameObject("TrialCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        public static void BuildDevelopment()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use isolated build command.");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/SiegeTrial"));
            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = Path.Combine(folder, "SiegeTrial.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development,
                }
            );
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Siege development build failed.");
        }

        public static void RefineLayout()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use isolated asset editing.");
            var root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var component = root.GetComponent<SiegeTrialLayout>() ?? root.AddComponent<SiegeTrialLayout>();
                var field = root.GetComponentInChildren<SiegeBattlefield>(true);
                if (field.GetComponent<CanvasRenderer>() == null)
                    field.gameObject.AddComponent<CanvasRenderer>();
                Bind(component, "_safeArea", root.GetComponentInChildren<SafeAreaView>(true).transform);
                Bind(
                    component,
                    "_fieldLayout",
                    root.GetComponentInChildren<SiegeBattlefield>(true).GetComponent<LayoutElement>()
                );
                var view = new SerializedObject(root.GetComponent<SiegeTrialView>());
                var text = new SiegeText(AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath));
                foreach (var name in new[] { "_playerBar", "_animalBar" })
                {
                    var slider = (Slider)view.FindProperty(name).objectReferenceValue;
                    if (slider.GetComponentInChildren<Text>(true) != null)
                        continue;
                    var label = Label(
                        "GaugeLabel",
                        slider.transform,
                        text.Get(name == "_playerBar" ? "unity.playerBar" : "unity.animalBar"),
                        26,
                        40
                    );
                    Stretch(label.rectTransform);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SiegeChoiceCell CreateCell()
        {
            var root = Rect("Widget_SiegeChoice", null);
            root.SetActive(false);
            var image = root.AddComponent<Image>();
            image.color = Surface;
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            var le = root.AddComponent<LayoutElement>();
            le.preferredHeight = 108;
            le.minHeight = 88;
            le.flexibleWidth = 1;
            var label = Label("Label", root.transform, string.Empty, 32, 100);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(12, 4);
            label.rectTransform.offsetMax = new Vector2(-12, -4);
            var cell = root.AddComponent<SiegeChoiceCell>();
            Bind(cell, "_button", button);
            Bind(cell, "_label", label);
            root.SetActive(true);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, CellPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved.GetComponent<SiegeChoiceCell>();
        }

        private static void CreatePanel(SiegeChoiceCell cell, SiegeText text, Sprite cat)
        {
            var root = Rect("Panel_SiegeTrial", null);
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<Image>().color = Color.white;
            var safe = Vertical("SafeArea", root.transform, 10);
            Stretch((RectTransform)safe.transform);
            safe.AddComponent<SafeAreaView>();
            var column = safe.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(32, 32, 20, 24);
            var view = root.AddComponent<SiegeTrialView>();
            Bind(view, "_data", AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath));
            Bind(view, "_copy", AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath));
            Bind(view, "_cellPrefab", cell);
            var header = Row("Header", safe.transform, 60);
            Label("Title", header.transform, text.Get("unity.title"), 44, 60);
            Bind(view, "_seed", Label("Seed", header.transform, string.Empty, 28, 60));
            Bind(view, "_status", Label("Status", safe.transform, string.Empty, 32, 48));
            var bars = Row("Gauges", safe.transform, 40);
            Bind(
                view,
                "_playerBar",
                Gauge("PlayerGauge", bars.transform, new Color32(76, 165, 139, 255), text.Get("unity.playerBar"))
            );
            Bind(
                view,
                "_animalBar",
                Gauge("AnimalGauge", bars.transform, new Color32(224, 112, 104, 255), text.Get("unity.animalBar"))
            );
            Bind(view, "_intent", Label("Intent", safe.transform, string.Empty, 30, 62));
            var board = Rect("Battlefield", safe.transform);
            var be = board.AddComponent<LayoutElement>();
            be.flexibleHeight = 1;
            be.minHeight = 340;
            board.AddComponent<CanvasRenderer>();
            var field = board.AddComponent<SiegeBattlefield>();
            field.raycastTarget = true;
            Bind(view, "_field", field);
            var adaptation = root.AddComponent<SiegeTrialLayout>();
            Bind(adaptation, "_safeArea", safe.transform);
            Bind(adaptation, "_fieldLayout", be);
            var animal = Rect("Cat", board.transform).AddComponent<Image>();
            animal.sprite = cat;
            animal.preserveAspect = true;
            animal.raycastTarget = false;
            Bind(field, "_cat", animal);
            Bind(view, "_detail", Label("Detail", safe.transform, string.Empty, 28, 68));
            Bind(view, "_feedback", Label("Feedback", safe.transform, string.Empty, 32, 50));
            var hand = Row("Hand", safe.transform, 116);
            Bind(view, "_handRoot", hand.transform);
            var lanes = Row("DeployLanes", safe.transform, 88);
            BindButton(view, "_top", "Top", lanes, text.Get("lane.top"));
            BindButton(view, "_mid", "Mid", lanes, text.Get("lane.mid"));
            BindButton(view, "_bottom", "Bottom", lanes, text.Get("lane.bottom"));
            var tools = Row("PlanTools", safe.transform, 88);
            BindButton(view, "_materials", "Materials", tools, text.Get("unity.materials"));
            BindButton(view, "_merge", "Merge", tools, text.Get("unity.merge"));
            BindButton(view, "_reroute", "Reroute", tools, text.Get("unity.reroute"));
            CreateMaterialPanel(view, safe.transform, text);
            Bind(view, "_plan", Label("Plan", safe.transform, string.Empty, 28, 80));
            var actions = Row("Actions", safe.transform, 96);
            BindButton(view, "_clear", "Clear", actions, text.Get("unity.clear"));
            BindButton(view, "_discard", "Discard", actions, text.Get("unity.discard"));
            var submit = BindButton(view, "_submit", "Submit", actions, text.Get("unity.submit"));
            submit.image.color = new Color32(173, 225, 207, 255);
            var settings = Row("Settings", safe.transform, 88);
            BindButton(view, "_pause", "Pause", settings, text.Get("unity.pause"));
            BindButton(view, "_retry", "Retry", settings, text.Get("unity.retry"));
            BindButton(view, "_newSeed", "NewSeed", settings, text.Get("unity.new"));
            BindButton(view, "_profile", "Profile", settings, text.Get("animal.calm"));
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateMaterialPanel(SiegeTrialView view, Transform parent, SiegeText text)
        {
            var panel = Vertical("MaterialPanel", parent, 8);
            var element = panel.AddComponent<LayoutElement>();
            element.preferredHeight = 240;
            element.flexibleHeight = 0;
            var controls = Row("MergeControls", panel.transform, 88);
            BindButton(view, "_newMaterial", "NewMaterial", controls, text.Get("unity.newUnit"));
            BindButton(view, "_anchor", "Anchor", controls, text.Format("unity.anchor", 0));
            BindButton(view, "_destination", "Destination", controls, text.Get("lane.mid"));
            var scrollRoot = Rect("RosterScroll", panel.transform);
            scrollRoot.AddComponent<LayoutElement>().flexibleHeight = 1;
            scrollRoot.AddComponent<Image>().color = Surface;
            scrollRoot.AddComponent<RectMask2D>();
            var scroll = scrollRoot.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = (RectTransform)scrollRoot.transform;
            var roster = Vertical("Roster", scrollRoot.transform, 8);
            var rect = (RectTransform)roster.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = Vector2.zero;
            roster.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = rect;
            Bind(view, "_rosterRoot", rect);
            Bind(view, "_materialsPanel", panel);
            panel.SetActive(false);
        }

        private static GameObject Rect(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result;
        }

        private static GameObject Vertical(string name, Transform parent, float spacing)
        {
            var result = Rect(name, parent);
            var group = result.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return result;
        }

        private static GameObject Row(string name, Transform parent, float height)
        {
            var result = Rect(name, parent);
            var element = result.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleHeight = 0;
            var group = result.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 10;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;
            return result;
        }

        private static Text Label(string name, Transform parent, string value, int size, float height)
        {
            var root = Rect(name, parent);
            var element = root.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleHeight = 0;
            element.flexibleWidth = 1;
            var label = root.AddComponent<Text>();
            label.text = value;
            label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 24;
            label.resizeTextMaxSize = size;
            label.color = Ink;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }

        private static Button BindButton(
            SiegeTrialView view,
            string field,
            string name,
            GameObject parent,
            string label
        )
        {
            var root = Rect(name, parent.transform);
            var element = root.AddComponent<LayoutElement>();
            element.flexibleWidth = 1;
            element.minHeight = 88;
            var image = root.AddComponent<Image>();
            image.color = Surface;
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            var title = Label("Label", root.transform, label, 30, 88);
            Stretch(title.rectTransform);
            title.rectTransform.offsetMin = new Vector2(8, 2);
            title.rectTransform.offsetMax = new Vector2(-8, -2);
            Bind(view, field, button);
            return button;
        }

        private static Slider Gauge(string name, Transform parent, Color tint, string title)
        {
            var root = Rect(name, parent);
            root.AddComponent<LayoutElement>().flexibleWidth = 1;
            root.AddComponent<Image>().color = Surface;
            var slider = root.AddComponent<Slider>();
            slider.interactable = false;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            var fill = Rect("Fill", root.transform).AddComponent<Image>();
            fill.color = tint;
            fill.raycastTarget = false;
            Stretch(fill.rectTransform);
            slider.fillRect = fill.rectTransform;
            var label = Label("GaugeLabel", root.transform, title, 26, 40);
            Stretch(label.rectTransform);
            return slider;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Bind(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(name);
            if (field == null)
                throw new InvalidOperationException("Missing binding: " + name);
            field.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
