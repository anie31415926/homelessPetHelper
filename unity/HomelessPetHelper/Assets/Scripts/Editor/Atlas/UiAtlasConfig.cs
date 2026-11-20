using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Editor.Atlas
{
    [Serializable]
    public sealed class UiAtlasConfig
    {
        public const string ConfigPath = "Assets/Settings/Atlas/UiAtlasGroups.json";
        public const string OutputFolder = "Assets/Art/Atlases";
        public int schema;
        public Group[] groups;

        [Serializable]
        public sealed class Group
        {
            public string id, lifetime;
            public int maxSize, padding;
            public bool includeInBuild;
            public string[] textures;
            public string AtlasPath => OutputFolder + "/Atlas_UI_" + id + ".spriteatlas";
        }

        public static UiAtlasConfig Read()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (asset == null) throw new InvalidDataException("Missing UI atlas config: " + ConfigPath);
            var config = JsonUtility.FromJson<UiAtlasConfig>(asset.text);
            config.Validate();
            return config;
        }

        public void Validate()
        {
            if (schema != 1 || groups == null || groups.Length == 0)
                throw new InvalidDataException("Invalid UI atlas schema/groups.");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var textures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in groups)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.id) ||
                    !Regex.IsMatch(group.id, "^[A-Za-z][A-Za-z0-9_]*$") || !ids.Add(group.id) ||
                    string.IsNullOrWhiteSpace(group.lifetime) ||
                    group.maxSize < 32 || group.maxSize > 2048 || (group.maxSize & (group.maxSize - 1)) != 0 ||
                    group.padding < 4 || group.padding > 32 ||
                    group.textures == null || group.textures.Length == 0)
                    throw new InvalidDataException("Invalid UI atlas group/lifetime/size/padding.");
                foreach (string path in group.textures)
                {
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/Art/", StringComparison.Ordinal) ||
                        path.Contains("..") || path.Contains('\\') || !textures.Add(path) ||
                        AssetDatabase.IsValidFolder(path) ||
                        !(AssetImporter.GetAtPath(path) is TextureImporter importer) ||
                        importer.textureType != TextureImporterType.Sprite || LoadSprites(path).Length == 0)
                        throw new InvalidDataException("Expected unique explicit Sprite texture: " + path);
                    foreach (var sprite in LoadSprites(path))
                        if (Math.Max(sprite.rect.width, sprite.rect.height) + group.padding * 2 > group.maxSize)
                            throw new InvalidDataException("UI Sprite exceeds atlas page size: " + path);
                }
            }
        }

        public static Sprite[] LoadSprites(string path)
        {
            // Keep existing GUID/local IDs, slicing, pivot and nine-slice border untouched.
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
        }
    }
}
