using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.Loading
{
    public static class LoadingArtworkUpgrade
    {
        private const string Art = "Assets/Art/Loading/";

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated LoadingArt authoring.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Art + "LoadingSlices.json"));
            Import(manifest);
            GameObject root = PrefabUtility.LoadPrefabContents(LoadingSceneAuthor.PrefabPath);
            try
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(Art + "T_Loading_Elements.png").OfType<Sprite>()
                    .ToDictionary(sprite => sprite.name);
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.sprite == null || AssetDatabase.GetAssetPath(image.sprite) !=
                        Art + "T_Loading_Elements.png") continue;
                    image.sprite = sprites[image.sprite.name];
                    if (image.name == "ProgressTrack")
                    {
                        image.preserveAspect = false;
                        image.rectTransform.sizeDelta = new Vector2(650f, 92f);
                    }
                    if (image.name == "ProgressFill")
                    {
                        image.rectTransform.offsetMin = new Vector2(16f, 22f);
                        image.rectTransform.offsetMax = new Vector2(-20f, -22f);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, LoadingSceneAuthor.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Updated Loading artwork in Prefab Mode; hierarchy, animation bindings and scene remain intact.");
        }

        private static void Import(Manifest manifest)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "T_Loading_Elements.png");
            Configure(importer);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(rect => rect.name);
            if (manifest.sprites.Length != previous.Count || manifest.sprites.Any(s => !previous.ContainsKey(s.name)))
                throw new InvalidDataException("Loading art refresh must preserve all existing sprite identities.");
            var rects = manifest.sprites.Select(slice => new SpriteRect
            {
                name = slice.name,
                rect = new Rect(slice.rect[0], manifest.atlasSize[1] - slice.rect[1] - slice.rect[3],
                    slice.rect[2], slice.rect[3]),
                pivot = new Vector2(.5f, .5f),
                alignment = SpriteAlignment.Center,
                spriteID = previous[slice.name].spriteID
            }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            importer = (TextureImporter)AssetImporter.GetAtPath(Art + "T_Loading_Background.png");
            Configure(importer);
            importer.SaveAndReimport();
        }

        private static void Configure(TextureImporter importer)
        {
            importer.maxTextureSize = 1024;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        [Serializable] private sealed class Manifest { public int[] atlasSize; public Slice[] sprites; }
        [Serializable] private sealed class Slice { public string name; public int[] rect; }
    }
}
