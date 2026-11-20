using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Editor.Atlas
{
    public static class UiAtlasTools
    {
        private static readonly string[] Platforms =
            { "DefaultTexturePlatform", "Standalone", "WebGL", "Android", "iPhone" };

        [MenuItem("Homeless Pet Helper/Atlas/Sync UI Atlases")]
        public static void Sync()
        {
            var config = UiAtlasConfig.Read();
            Directory.CreateDirectory(UiAtlasConfig.OutputFolder);
            AssetDatabase.Refresh();
            foreach (var group in config.groups) SyncGroup(group);
            AssetDatabase.SaveAssets();
            Validate();
        }

        public static void SyncGroup(UiAtlasConfig.Group group)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(group.AtlasPath);
            if (atlas == null)
            {
                if (File.Exists(group.AtlasPath))
                    throw new InvalidDataException("Not a SpriteAtlas: " + group.AtlasPath);
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, group.AtlasPath);
            }
            if (!group.includeInBuild)
            {
                // Removing packables alone can retain packed-Sprite cache from an earlier pack/build.
                var empty = new SpriteAtlas { name = atlas.name };
                EditorUtility.CopySerialized(empty, atlas);
                Object.DestroyImmediate(empty);
            }
            atlas.SetIsVariant(false);
            atlas.SetIncludeInBuild(group.includeInBuild);
            atlas.SetPackingSettings(new SpriteAtlasPackingSettings
            {
                enableRotation = false, enableTightPacking = false, enableAlphaDilation = true,
                padding = group.padding,
            });
            atlas.SetTextureSettings(new SpriteAtlasTextureSettings
            {
                generateMipMaps = false, readable = false, sRGB = true, filterMode = FilterMode.Bilinear,
            });
            foreach (string platform in Platforms)
            {
                var settings = atlas.GetPlatformSettings(platform);
                settings.name = platform;
                settings.overridden = platform != "DefaultTexturePlatform";
                settings.maxTextureSize = group.maxSize;
                settings.format = TextureImporterFormat.RGBA32;
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                settings.crunchedCompression = false;
                settings.allowsAlphaSplitting = false;
                atlas.SetPlatformSettings(settings);
            }
            atlas.Remove(atlas.GetPackables());
            // A disabled atlas must not claim Sprites: Unity can strip their source textures from builds.
            if (group.includeInBuild)
                atlas.Add(group.textures.SelectMany(UiAtlasConfig.LoadSprites).Cast<Object>().ToArray());
            EditorUtility.SetDirty(atlas);
        }

        public static void Validate()
        {
            var config = UiAtlasConfig.Read();
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
                throw new InvalidDataException("Sprite packing is disabled.");
            foreach (var group in config.groups)
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(group.AtlasPath);
                if (atlas == null)
                    throw new InvalidDataException("Missing UI atlas; run Atlas/Sync UI Atlases: " + group.AtlasPath);
                ValidateGroup(atlas, group);
            }
            ValidateMembership();
        }

        public static void ValidateGroup(SpriteAtlas atlas, UiAtlasConfig.Group group)
        {
            var packing = atlas.GetPackingSettings();
            var texture = atlas.GetTextureSettings();
            if (atlas.isVariant || atlas.IsIncludeInBuild() != group.includeInBuild ||
                packing.enableRotation || packing.enableTightPacking ||
                !packing.enableAlphaDilation || packing.padding != group.padding ||
                texture.generateMipMaps || texture.readable ||
                !texture.sRGB || texture.filterMode != FilterMode.Bilinear)
                throw new InvalidDataException("Unsafe UI atlas settings: " + group.AtlasPath);
            var expected = group.includeInBuild ? group.textures.SelectMany(UiAtlasConfig.LoadSprites)
                .Select(SpriteKey).OrderBy(x => x).ToArray() : Array.Empty<string>();
            var actual = atlas.GetPackables();
            if (!group.includeInBuild && atlas.spriteCount != 0)
                throw new InvalidDataException("Disabled UI atlas has stale packed Sprite cache; sync: " +
                    group.AtlasPath);
            if (actual.Any(x => !(x is Sprite)) ||
                !expected.SequenceEqual(actual.Cast<Sprite>().Select(SpriteKey).OrderBy(x => x)))
                throw new InvalidDataException("Stale UI atlas membership: " + group.AtlasPath);
            foreach (string platform in Platforms)
            {
                var settings = atlas.GetPlatformSettings(platform);
                if (settings.maxTextureSize != group.maxSize || settings.format != TextureImporterFormat.RGBA32 ||
                    settings.textureCompression != TextureImporterCompression.Uncompressed ||
                    settings.crunchedCompression ||
                    settings.allowsAlphaSplitting || (platform != "DefaultTexturePlatform" && !settings.overridden))
                    throw new InvalidDataException("Unsafe UI atlas platform: " + group.AtlasPath + " / " + platform);
            }
        }

        private static string SpriteKey(Sprite sprite)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id))
                throw new InvalidDataException("Sprite has no persistent identity: " + sprite.name);
            return guid + ":" + id;
        }

        private static void ValidateMembership()
        {
            var owners = new Dictionary<string, string>();
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p =>
                p.EndsWith(".spriteatlas", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith(".spriteatlasv2", StringComparison.OrdinalIgnoreCase)))
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (atlas == null) throw new InvalidDataException("Cannot inspect atlas: " + path);
                if (atlas.isVariant) continue;
                foreach (var packable in atlas.GetPackables())
                {
                    string assetPath = AssetDatabase.GetAssetPath(packable);
                    var sprites = packable is Sprite sprite ? new[] { sprite } :
                        AssetDatabase.IsValidFolder(assetPath)
                            ? AssetDatabase.FindAssets("t:Texture2D", new[] {assetPath})
                                .Select(AssetDatabase.GUIDToAssetPath).SelectMany(UiAtlasConfig.LoadSprites)
                            : UiAtlasConfig.LoadSprites(assetPath);
                    foreach (var item in sprites)
                    {
                        string key = SpriteKey(item);
                        if (owners.TryGetValue(key, out string owner))
                            throw new InvalidDataException("Duplicate Sprite atlas membership: " +
                                owner + " / " + path + " / " + assetPath);
                        owners.Add(key, path);
                    }
                }
            }
        }

        [MenuItem("Homeless Pet Helper/Atlas/Pack UI Preview")]
        public static void PackPreview()
        {
            Validate();
            var config = UiAtlasConfig.Read();
            var atlases = config.groups.Where(g => g.includeInBuild)
                .Select(g => AssetDatabase.LoadAssetAtPath<SpriteAtlas>(g.AtlasPath)).ToArray();
            var report = new List<string>();
            foreach (var group in config.groups.Where(g => !g.includeInBuild))
                report.Add(group.id + ": standalone Sprites selected; atlas packing list is empty.");
            foreach (var target in new[] { BuildTarget.StandaloneWindows64, BuildTarget.WebGL })
            {
                if (atlases.Length == 0) break;
                SpriteAtlasUtility.PackAtlases(atlases, target);
                foreach (var atlas in atlases)
                {
                    if (atlas.spriteCount == 0) throw new InvalidDataException("Empty packed atlas: " + atlas.name);
                    var sprites = new Sprite[atlas.spriteCount];
                    atlas.GetSprites(sprites);
                    var pages = sprites.Select(s => UnityEditor.Sprites.SpriteUtility.GetSpriteTexture(s, true))
                        .Distinct().ToArray();
                    var group = config.groups.Single(g => g.AtlasPath == AssetDatabase.GetAssetPath(atlas));
                    if (sprites.Length != group.textures.Sum(p => UiAtlasConfig.LoadSprites(p).Length) ||
                        group.textures.SelectMany(UiAtlasConfig.LoadSprites).Any(s => !atlas.CanBindTo(s)))
                        throw new InvalidDataException("Packed atlas cannot bind all original Sprite references.");
                    report.Add(target + " / " + atlas.name + ": " + sprites.Length + " sprites, " + pages.Length +
                        " pages: " + string.Join(", ", pages.Select(p => p.width + "x" + p.height + " " + p.format)));
                    report.Add("Base RGBA32 bytes: " + pages.Sum(p => (long)p.width * p.height * 4) +
                        "; rectangular occupancy: " + sprites.Sum(s => s.rect.width * s.rect.height) /
                        pages.Sum(p => (float)p.width * p.height));
                    for (int page = 0; page < pages.Length; page++)
                        WritePreview(pages[page], target + "-" + atlas.name + "-" + page);
                    foreach (var sprite in sprites) Object.DestroyImmediate(sprite);
                }
            }
            Directory.CreateDirectory("Library/AtlasAudit");
            File.WriteAllLines("Library/AtlasAudit/packed-ui.txt", report);
            Debug.Log(string.Join("\n", report));
        }

        private static void WritePreview(Texture2D page, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Atlas preview requires a graphics device; use -Graphics.");
            var target = RenderTexture.GetTemporary(page.width, page.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var readable = new Texture2D(page.width, page.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(page, target);
                RenderTexture.active = target;
                readable.ReadPixels(new Rect(0, 0, page.width, page.height), 0, 0);
                readable.Apply();
                if (readable.GetPixels32().All(pixel => pixel.a == 0))
                    throw new InvalidDataException("Blank packed atlas preview: " + name);
                Directory.CreateDirectory("Library/AtlasAudit");
                File.WriteAllBytes("Library/AtlasAudit/" + name + ".png", readable.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(readable);
            }
        }
    }
}
