using System;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.LevelUi
{
    /// <summary>One-time Prefab Mode migration: full-canvas dimming, safe-area foreground.</summary>
    public static class LevelOverlayFullscreen
    {
        private const string WidgetPath = "Assets/Prefabs/Widgets/Widget_LevelOverlays.prefab";
        private static readonly Color Dim = new Color32(0, 0, 0, 88);

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated LevelUiFullscreen.");
            Edit(WidgetPath, UpgradeWidget);
            foreach (string path in new[] { "Assets/Prefabs/Panels/Panel_Approach.prefab",
                "Assets/Prefabs/Panels/Panel_SiegeTrial.prefab" }) Edit(path, MoveToCanvas);
            AssetDatabase.SaveAssets();
        }

        private static void UpgradeWidget(GameObject root)
        {
            var view = root.GetComponent<LevelOverlayView>();
            if (Ref<GameObject>(view, "_modalBackdrop") != null)
                throw new InvalidOperationException("Fullscreen overlay already exists. Edit the saved prefab.");
            var content = Rect("SafeContent", root.transform);
            content.gameObject.AddComponent<SafeAreaView>();
            var guide = Ref<GuideMaskGraphic>(view, "_guideMask");
            guide.color = Dim;
            var backdrop = Rect("ModalBackdrop", root.transform);
            Surface(backdrop, Dim, true);
            Bind(view, "_modalBackdrop", backdrop.gameObject);
            var modal = Ref<GameObject>(view, "_modalRoot");
            UnityEngine.Object.DestroyImmediate(modal.GetComponent<Image>());
            UnityEngine.Object.DestroyImmediate(modal.GetComponent<CanvasRenderer>());
            Ref<GameObject>(view, "_guideCaption").transform.SetParent(content, false);
            modal.transform.SetParent(content, false);
            Ref<Text>(view, "_notice").transform.SetParent(content, false);
            guide.transform.SetAsFirstSibling();
            backdrop.SetSiblingIndex(1);
            content.SetAsLastSibling();
            view.Close();
        }

        private static void MoveToCanvas(GameObject root)
        {
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Expected a panel root Canvas.");
            var overlay = root.GetComponentInChildren<LevelOverlayView>(true);
            overlay.transform.SetParent(canvas.transform, false);
            Place((RectTransform)overlay.transform, 0, 0, 1, 1);
            overlay.transform.SetAsLastSibling();
        }

        private static void Edit(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
