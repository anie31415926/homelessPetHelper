using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Station.UI;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.LevelUi
{
    internal static class UiLayoutLegacyMigration
    {
        internal static void PetMotion(GameObject root)
        {
            var dropdown = root.GetComponentInChildren<Dropdown>(true);
            var bounds = root.GetComponentInChildren<SafeAreaView>(true).GetComponent<RectTransform>();
            Bind(dropdown.template.gameObject.AddComponent<BoundedPopup>(), "_bounds", bounds);
        }

        internal static void Page(GameObject root)
        {
            var safe = root.GetComponentInChildren<HomelessPetHelper.Modules.Rescue.UI.SafeAreaView>(true);
            var column = safe.GetComponentsInChildren<VerticalLayoutGroup>(true).First();
            UiLayoutMigration.ScrollContent((RectTransform)column.transform);
        }

        internal static void Station(GameObject root)
        {
            var panel = root.GetComponent<StationPanelView>();
            foreach (string field in new[] { "_summary", "_detail", "_overview" })
            {
                var page = Ref<GameObject>(panel, field);
                var viewport = UiLayoutMigration.ScrollContent((RectTransform)page.transform);
                Bind(panel, field, viewport.gameObject);
            }
            var stage = (RectTransform)Ref<HomelessPetHelper.Modules.Station.UI.DirtSurfaceView>(panel,
                "_surface").transform.parent;
            var size = stage.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 720; size.flexibleHeight = 0;
            SetHeight(Ref<Image>(panel, "_summaryAnimal").transform.parent, 600);
            UiLayoutMigration.WrapRow((RectTransform)Ref<Button>(panel, "_check").transform.parent);
            UiLayoutMigration.WrapRow((RectTransform)Ref<Button>(panel, "_clean").transform.parent);
        }

        internal static void ActionBar(GameObject root)
        {
            var size = root.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 168;
        }

        internal static void Rescue(GameObject root)
        {
            var safe = root.GetComponentInChildren<SafeAreaView>(true).transform;
            var column = safe.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(48, 48, 48, 48); column.spacing = 24;
            column.childControlHeight = column.childControlWidth = true;
            column.childForceExpandHeight = false;
            string[] order = { "Title", "SessionControls", "Status", "Animal", "Section", "Observation", "ActionDock" };
            var nodes = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < order.Length; i++)
                nodes.Single(item => item.name == order[i]).SetSiblingIndex(i);
            nodes.Single(item => item.name == "HeaderSurface").gameObject.SetActive(false);
            var observation = nodes.Single(item => item.name == "Observation");
            SetHeight(observation, 720);
            var cat = nodes.Single(item => item.name == "Cat");
            Place(cat, 0, 0, 1, 1);
            var dock = nodes.Single(item => item.name == "ActionDock");
            var dockLayout = dock.gameObject.AddComponent<VerticalLayoutGroup>();
            dockLayout.spacing = 24; dockLayout.childControlHeight = dockLayout.childControlWidth = true;
            dockLayout.childForceExpandHeight = false;
            var choiceList = Ref<ScrollRect>(root.GetComponent<OptionPanelView>(), "_scroll");
            SetHeight(choiceList.transform, 528);
            UiLayoutMigration.WrapRow(nodes.Single(item => item.name == "SessionControls"));
            UiLayoutMigration.ScrollContent((RectTransform)safe);
        }

        private static void SetHeight(Transform node, float height)
        {
            var size = node.GetComponent<LayoutElement>();
            if (size == null) size = node.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height; size.flexibleHeight = 0;
        }
    }
}
