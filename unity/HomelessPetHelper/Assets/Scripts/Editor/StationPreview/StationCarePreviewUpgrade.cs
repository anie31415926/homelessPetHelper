using System;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Modules.Station.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.StationPreview.StationPreviewLayout;

namespace HomelessPetHelper.Editor.StationPreview
{
    /// <summary>One-time, additive Prefab Mode migration of the saved panel, never a panel rebuild.</summary>
    public static class StationCarePreviewUpgrade
    {
        public static void UpgradeSavedPrefab()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch snapshot.");
            ImportArt();
            GameObject root = PrefabUtility.LoadPrefabContents(StationPreviewAssets.PanelPath);
            try
            {
                var view = root.GetComponent<StationPanelView>();
                var fields = new SerializedObject(view);
                if (fields.FindProperty("_groom").objectReferenceValue == null)
                {
                    AddCommands(view, fields);
                }

                foreach (DirtSurfaceView surface in root.GetComponentsInChildren<DirtSurfaceView>(true))
                {
                    AddLayers(surface);
                    surface.GetComponent<AspectRatioFitter>().aspectRatio = 1;
                }

                foreach (CareBubbleView bubbles in root.GetComponentsInChildren<CareBubbleView>(true))
                {
                    if (bubbles.GetComponent<CanvasRenderer>() == null)
                    {
                        bubbles.gameObject.AddComponent<CanvasRenderer>();
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, StationPreviewAssets.PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        private static void AddCommands(StationPanelView view, SerializedObject fields)
        {
            var text = new OptionPreviewText(AssetDatabase.LoadAssetAtPath<TextAsset>(StationPreviewAssets.TextPath));
            var clean = (Button)fields.FindProperty("_clean").objectReferenceValue;
            var finish = (Button)fields.FindProperty("_finish").objectReferenceValue;
            Transform commands = clean.transform.parent;
            GameObject tools = Row("CareTools", commands.parent, 104);
            tools.transform.SetSiblingIndex(commands.GetSiblingIndex() + 1);
            clean.transform.SetParent(tools.transform, false);
            finish.transform.SetParent(tools.transform, false);
            Button groom = Button("Groom", tools.transform, text.Get("groom"));
            groom.transform.SetSiblingIndex(1);
            Bind(view, "_groom", groom);
            Bind(view, "_quietSpace", Button("QuietSpace", commands, text.Get("quietSpace")));
            finish.GetComponentInChildren<Text>(true).text = text.Get("finish");
        }

        private static void AddLayers(DirtSurfaceView surface)
        {
            var fields = new SerializedObject(surface);
            if (fields.FindProperty("_fur").objectReferenceValue != null) return;
            GameObject fur = Rect("MessyFur", surface.transform);
            Stretch((RectTransform)fur.transform);
            fur.transform.SetSiblingIndex(1);
            var overlay = fur.AddComponent<RawImage>();
            overlay.raycastTarget = false;
            Bind(surface, "_fur", overlay);
            GameObject bubbles = Rect("Bubbles", surface.transform);
            Stretch((RectTransform)bubbles.transform);
            var feedback = bubbles.AddComponent<CareBubbleView>();
            feedback.raycastTarget = false;
            Bind(surface, "_bubbles", feedback);
        }

        private static void ImportArt()
        {
            foreach (string kind in new[] { "Dog", "Cat" })
            {
                string path = StationPreviewAssets.CareArtFolder + "/T_Station_" + kind + "Temporary.png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) throw new InvalidOperationException("Missing temporary art: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }
    }
}
