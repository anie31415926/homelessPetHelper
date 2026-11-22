using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Siege.UI;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.Field
{
    /// <summary>Import prepared atlas pages, then bind existing graphics in Prefab Mode.</summary>
    public static class FieldPropArtImport
    {
        [Serializable] private sealed class Manifest { public Page[] pages; }
        [Serializable] private sealed class Page
        {
            public string texture, palette;
            public Slice[] slices;
        }
        [Serializable] private sealed class Slice
        {
            public string id;
            public int x, y, width, height;
        }

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated FieldPropAssets.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("FieldPropSource/manifest.json"));
            var palettes = new List<SceneSpritePalette>();
            foreach (var page in manifest.pages) palettes.Add(Import(page));
            Edit("Assets/Prefabs/Panels/Panel_Approach.prefab", root => BindTerrain(root, palettes[0]));
            Edit("Assets/Prefabs/Panels/Panel_SiegeTrial.prefab", root =>
            {
                BindTerrain(root, palettes[0]);
                foreach (var field in root.GetComponentsInChildren<SiegeBattlefield>(true))
                    Bind(field, "_spritePalette", palettes[1]);
                foreach (var carry in root.GetComponentsInChildren<SiegeCarryOverlay>(true))
                    Bind(carry, "_spritePalette", palettes[1]);
                BindIcons(root, palettes[1]);
            });
            Edit("Assets/Prefabs/Widgets/Widget_SiegeChoice.prefab", root => BindIcons(root, palettes[1]));
            AssetDatabase.SaveAssets();
        }

        private static SceneSpritePalette Import(Page page)
        {
            ImportTexture(page);
            var palette = AssetDatabase.LoadAssetAtPath<SceneSpritePalette>(page.palette);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<SceneSpritePalette>();
                AssetDatabase.CreateAsset(palette, page.palette);
            }
            var serialized = new SerializedObject(palette);
            serialized.FindProperty("_texture").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(page.texture);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(page.texture).OfType<Sprite>().ToArray();
            var entries = serialized.FindProperty("_sprites");
            entries.arraySize = sprites.Length;
            for (int index = 0; index < sprites.Length; index++)
                entries.GetArrayElementAtIndex(index).objectReferenceValue = sprites[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(palette);
            return palette;
        }

        private static void ImportTexture(Page page)
        {
            File.Copy("FieldPropSource/" + Path.GetFileName(page.texture), page.texture, true);
            AssetDatabase.ImportAsset(page.texture, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(page.texture);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            SetSlices(importer, page.slices);
            importer.SaveAndReimport();
        }

        private static void SetSlices(TextureImporter importer, Slice[] slices)
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(rect => rect.name);
            var rects = slices.Select(slice => new SpriteRect
            {
                name = slice.id,
                rect = new Rect(slice.x, slice.y, slice.width, slice.height),
                pivot = new Vector2(.5f, .5f),
                alignment = SpriteAlignment.Center,
                spriteID = previous.TryGetValue(slice.id, out var old) ? old.spriteID : GUID.Generate()
            }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
        }

        private static void BindTerrain(GameObject root, SceneSpritePalette palette)
        {
            foreach (var graphic in root.GetComponentsInChildren<OutingFieldGraphic>(true))
                Bind(graphic, "_spritePalette", palette);
        }
        private static void BindIcons(GameObject root, SceneSpritePalette palette)
        {
            foreach (var icon in root.GetComponentsInChildren<SiegePropIcon>(true))
                Bind(icon, "_spritePalette", palette);
        }
        private static void Edit(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
