using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Editor.ResourceAudit
{
    public static class ImageResourceAudit
    {
        public const string ReportDirectory = "Library/ResourceAudit";
        public const string ReportName = "image-resources";

        [MenuItem("Homeless Pet Helper/Tools/Check Image Resources")]
        public static void WriteReport()
        {
            ImageAuditReport report = Scan();
            Directory.CreateDirectory(ReportDirectory);
            var encoding = new UTF8Encoding(false);
            string stem = System.IO.Path.Combine(ReportDirectory, ReportName);
            File.WriteAllText(stem + ".json", JsonUtility.ToJson(report, true) + "\n", encoding);
            File.WriteAllText(stem + ".md", report.ToMarkdown(), encoding);
            Debug.Log($"Image audit: {report.Textures.Count} textures, {report.Atlases.Count} atlases, " +
                $"{report.Findings.Count} review/info candidates. No assets changed. Report: {stem}.md");
        }

        [MenuItem("Homeless Pet Helper/Tools/Check Image Resources", true)]
        private static bool CanScan()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                !EditorApplication.isCompiling && !EditorApplication.isUpdating;
        }

        [MenuItem("Homeless Pet Helper/Tools/Reveal Image Resource Report")]
        private static void RevealReport()
        {
            EditorUtility.RevealInFinder(System.IO.Path.GetFullPath(
                System.IO.Path.Combine(ReportDirectory, ReportName + ".md")));
        }

        [MenuItem("Homeless Pet Helper/Tools/Reveal Image Resource Report", true)]
        private static bool HasReport()
        {
            return File.Exists(System.IO.Path.Combine(ReportDirectory, ReportName + ".md"));
        }

        // Call on the editor main thread. Does not save, refresh, pack, or modify assets.
        public static ImageAuditReport Scan()
        {
            if (!CanScan())
            {
                throw new InvalidOperationException("Image audit requires idle Edit Mode.");
            }

            string[] paths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) &&
                    !AssetDatabase.IsValidFolder(path))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var report = new ImageAuditReport
            {
                ActiveBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString()
            };
            HashSet<string> buildDependencies = GetBuildSceneDependencies();
            foreach (string path in paths)
            {
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    ReadTexture(path, importer, report, buildDependencies);
                }
            }

            IndexReferences(paths, report);
            ImageAtlasAudit.ReadAtlases(paths, report);
            foreach (ImageAuditReport.TextureEntry texture in report.Textures)
            {
                CheckTexture(texture, report);
            }

            report.Findings = report.Findings.OrderBy(finding => finding.Path, StringComparer.Ordinal)
                .ThenBy(finding => finding.Code, StringComparer.Ordinal).ToList();
            return report;
        }

        private static HashSet<string> GetBuildSceneDependencies()
        {
            var dependencies = new HashSet<string>(StringComparer.Ordinal);
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes.Where(scene => scene.enabled))
            {
                dependencies.UnionWith(AssetDatabase.GetDependencies(scene.path, true));
            }

            return dependencies;
        }

        private static void ReadTexture(string path, TextureImporter importer, ImageAuditReport report,
            HashSet<string> buildDependencies)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                report.AddFinding("Info", "NonTexture2DImporter", path,
                    "TextureImporter does not provide a Texture2D; no dimensions or import review performed.");
                return;
            }

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            TextureImporterPlatformSettings defaults = importer.GetDefaultPlatformTextureSettings();
            TextureImporterPlatformSettings webGl = importer.GetPlatformTextureSettings("WebGL");
            report.Textures.Add(new ImageAuditReport.TextureEntry
            {
                Path = path,
                Guid = AssetDatabase.AssetPathToGUID(path),
                IsSprite = importer.textureType == TextureImporterType.Sprite,
                SourceWidth = width,
                SourceHeight = height,
                SourceFileBytes = new FileInfo(path).Length,
                ImportedWidth = texture.width,
                ImportedHeight = texture.height,
                ImportedFormat = texture.format.ToString(),
                ImportedRgba32ReferenceBytes = (long)texture.width * texture.height * 4,
                ReadWrite = importer.isReadable,
                Mipmaps = importer.mipmapEnabled,
                DefaultMaxSize = defaults.maxTextureSize,
                DefaultCompression = defaults.textureCompression.ToString(),
                WebGlOverride = webGl.overridden,
                WebGlMaxSize = webGl.overridden ? webGl.maxTextureSize : defaults.maxTextureSize,
                WebGlFormat = (webGl.overridden ? webGl.format : defaults.format).ToString(),
                WebGlCompression =
                    (webGl.overridden ? webGl.textureCompression : defaults.textureCompression).ToString(),
                InEnabledBuildSceneDependencies = buildDependencies.Contains(path)
            });
        }

        private static void IndexReferences(string[] paths, ImageAuditReport report)
        {
            var references = report.Textures.ToDictionary(texture => texture.Path,
                texture => new SortedSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
            foreach (string path in paths)
            {
                foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                {
                    if (path != dependency && references.TryGetValue(dependency, out SortedSet<string> owners))
                    {
                        owners.Add(path);
                    }
                }
            }

            foreach (ImageAuditReport.TextureEntry texture in report.Textures)
            {
                texture.ReferencedBy = references[texture.Path].ToArray();
            }
        }

        private static void CheckTexture(ImageAuditReport.TextureEntry texture, ImageAuditReport report)
        {
            if (texture.ReadWrite)
            {
                report.AddFinding("Review", "TextureReadWrite", texture.Path,
                    "CPU-readable texture. Disable only if no pixel access is needed; never auto-fix masks.");
            }

            if (texture.IsSprite && texture.Mipmaps)
            {
                report.AddFinding("Review", "SpriteMipmaps", texture.Path,
                    "Sprite mipmaps enabled. Screen-space UI usually does not need them; world sprites may.");
            }

            if (Math.Max(texture.ImportedWidth, texture.ImportedHeight) > report.LargeDimensionReviewThreshold)
            {
                report.AddFinding("Review", "LargeImportedTexture", texture.Path,
                    "Imported dimension exceeds the review threshold; compare with actual display size.");
            }

            if (texture.DefaultCompression == TextureImporterCompression.Uncompressed.ToString())
            {
                report.AddFinding("Review", "UncompressedDefault", texture.Path,
                    "Default compression mode is uncompressed. Check explicit formats and WebGL overrides first.");
            }

            if (texture.ReferencedBy.Length == 0 && texture.AtlasPaths.Length == 0)
            {
                report.AddFinding("Info", "NoIndexedReference", texture.Path,
                    "No direct Assets reference or declared atlas input found. Dynamic loads are not indexed; " +
                    "this is not proof of an unused asset and is not a deletion recommendation.");
            }
        }
    }
}
