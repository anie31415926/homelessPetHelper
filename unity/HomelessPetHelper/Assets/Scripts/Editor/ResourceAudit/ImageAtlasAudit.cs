using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace HomelessPetHelper.Editor.ResourceAudit
{
    internal static class ImageAtlasAudit
    {
        public static void ReadAtlases(string[] paths, ImageAuditReport report)
        {
            foreach (string path in paths.Where(IsAtlasPath))
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (atlas == null)
                {
                    throw new InvalidOperationException("Cannot read SpriteAtlas; image audit incomplete: " + path);
                }

                SpriteAtlas master = atlas.isVariant ? atlas.GetMasterAtlas() : atlas;
                string[] packables = master == null ? Array.Empty<string>() : master.GetPackables()
                    .Where(asset => asset != null).Select(AssetDatabase.GetAssetPath)
                    .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
                string[] textures = report.Textures.Where(texture => texture.IsSprite &&
                    packables.Any(packable => ContainsTexture(packable, texture.Path)))
                    .Select(texture => texture.Path).ToArray();
                report.Atlases.Add(new ImageAuditReport.AtlasEntry
                {
                    Path = path,
                    IsVariant = atlas.isVariant,
                    MasterPath = master == null ? "" : AssetDatabase.GetAssetPath(master),
                    IncludeInBuild = atlas.IsIncludeInBuild(),
                    PackablePaths = packables,
                    TexturePaths = textures
                });
                if (master == null)
                {
                    report.AddFinding("Review", "AtlasMissingMaster", path,
                        "Variant atlas has no master; inherited membership could not be indexed.");
                }
            }

            IndexMembership(report);
        }

        private static bool IsAtlasPath(string path)
        {
            return path.EndsWith(".spriteatlas", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".spriteatlasv2", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsTexture(string packable, string texture)
        {
            return packable == texture || (AssetDatabase.IsValidFolder(packable) &&
                texture.StartsWith(packable + "/", StringComparison.Ordinal));
        }

        private static void IndexMembership(ImageAuditReport report)
        {
            foreach (ImageAuditReport.TextureEntry texture in report.Textures)
            {
                ImageAuditReport.AtlasEntry[] atlases = report.Atlases
                    .Where(atlas => atlas.TexturePaths.Contains(texture.Path)).ToArray();
                texture.AtlasPaths = atlases.Select(atlas => atlas.Path).ToArray();
                string[] masters = atlases.Where(atlas => !atlas.IsVariant).Select(atlas => atlas.Path).ToArray();
                if (masters.Length > 1)
                {
                    report.AddFinding("Review", "MultipleMasterAtlases", texture.Path,
                        "Declared in multiple master atlases: " + string.Join(", ", masters) +
                        ". Check intended usage; this is not proof of duplicate build bytes.");
                }
            }
        }
    }
}
