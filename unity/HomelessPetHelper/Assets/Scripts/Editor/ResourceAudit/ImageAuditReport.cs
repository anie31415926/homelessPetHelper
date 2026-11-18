using System;
using System.Collections.Generic;
using System.Text;

namespace HomelessPetHelper.Editor.ResourceAudit
{
    // Detached export data; this is not a second source of asset dependencies.
    [Serializable]
    public sealed class ImageAuditReport
    {
        public int SchemaVersion = 2;
        public string ActiveBuildTarget;
        public int LargeDimensionReviewThreshold = 2048;
        public List<TextureEntry> Textures = new List<TextureEntry>();
        public List<AtlasEntry> Atlases = new List<AtlasEntry>();
        public List<Finding> Findings = new List<Finding>();
        public string[] Limitations =
        {
            "On-demand, read-only scan of imported Assets; Packages and unsaved scene edits are not indexed.",
            "Only TextureImporter-backed Texture2D assets are reviewed; custom image importers are not covered.",
            "References are Unity serialized asset dependencies, not code/string-based loads or a build manifest.",
            "Imported dimensions/format describe the active editor build target, not a WeChat device.",
            "WebGL fields are importer settings only; they do not verify WeChat SDK output or GPU support.",
            "Atlas membership is declared input, not measured packed pages or duplicate bundle bytes.",
            "Source file bytes are not exported package or first-download bytes.",
            "RGBA32 reference bytes are width * height * 4, not actual compressed GPU or resident memory.",
            "Review findings are candidates, not confirmed defects; no references does not mean unused.",
            "No frame time, draw calls, download size or device memory has been measured."
        };

        [Serializable]
        public sealed class TextureEntry
        {
            public string Path;
            public string Guid;
            public bool IsSprite;
            public int SourceWidth;
            public int SourceHeight;
            public long SourceFileBytes;
            public int ImportedWidth;
            public int ImportedHeight;
            public string ImportedFormat;
            public long ImportedRgba32ReferenceBytes;
            public bool ReadWrite;
            public bool Mipmaps;
            public int DefaultMaxSize;
            public string DefaultCompression;
            public bool WebGlOverride;
            public int WebGlMaxSize;
            public string WebGlFormat;
            public string WebGlCompression;
            public bool InEnabledBuildSceneDependencies;
            public string[] ReferencedBy = Array.Empty<string>();
            public string[] AtlasPaths = Array.Empty<string>();
        }

        [Serializable]
        public sealed class AtlasEntry
        {
            public string Path;
            public bool IsVariant;
            public string MasterPath;
            public bool IncludeInBuild;
            public string[] PackablePaths;
            public string[] TexturePaths;
        }

        [Serializable]
        public sealed class Finding
        {
            public string Level;
            public string Code;
            public string Path;
            public string Detail;
        }

        public void AddFinding(string level, string code, string path, string detail)
        {
            Findings.Add(new Finding { Level = level, Code = code, Path = path, Detail = detail });
        }

        public string ToMarkdown()
        {
            var text = new StringBuilder("# Image Resource Audit\n\n");
            text.AppendLine($"Active target: {ActiveBuildTarget}. " +
                $"Textures: {Textures.Count}. Atlases: {Atlases.Count}.");
            text.AppendLine($"Large imported dimension review threshold: > {LargeDimensionReviewThreshold} px.");
            long sourceBytes = 0;
            long rgba32Bytes = 0;
            foreach (TextureEntry texture in Textures)
            {
                sourceBytes += texture.SourceFileBytes;
                rgba32Bytes += texture.ImportedRgba32ReferenceBytes;
            }

            text.AppendLine($"All scanned source files: {sourceBytes} bytes. " +
                $"RGBA32 reference sum: {rgba32Bytes} bytes; not a package size or a resident set.");
            foreach (string limitation in Limitations)
            {
                text.AppendLine("- " + limitation);
            }

            text.AppendLine("\n## Findings\n");
            if (Findings.Count == 0)
            {
                text.AppendLine("No candidates from these checks. This is not a performance pass.");
            }

            foreach (Finding finding in Findings)
            {
                text.AppendLine($"- [{finding.Level}] {finding.Code}: {finding.Path} - {finding.Detail}");
            }

            text.AppendLine("\n## Textures And Direct References\n");
            foreach (TextureEntry texture in Textures)
            {
                text.AppendLine($"### {texture.Path}\n");
                text.AppendLine($"Source: {texture.SourceWidth} x {texture.SourceHeight} px. " +
                    $"Imported: {texture.ImportedWidth} x {texture.ImportedHeight} px ({texture.ImportedFormat}).");
                text.AppendLine($"Source file: {texture.SourceFileBytes} bytes (not exported/download size). " +
                    $"RGBA32 reference: {texture.ImportedRgba32ReferenceBytes} bytes (not measured GPU memory).");
                text.AppendLine($"Read/write: {texture.ReadWrite}. Mipmaps: {texture.Mipmaps}. " +
                    $"Enabled build scene dependency: {texture.InEnabledBuildSceneDependencies}.");
                text.AppendLine($"Default max: {texture.DefaultMaxSize}. Compression: {texture.DefaultCompression}.");
                text.AppendLine($"WebGL override: {texture.WebGlOverride}. Max: {texture.WebGlMaxSize}. " +
                    $"Format: {texture.WebGlFormat}. Compression: {texture.WebGlCompression}.\n");
                text.AppendLine("References: " + string.Join(", ", texture.ReferencedBy));
                text.AppendLine("Atlases: " + string.Join(", ", texture.AtlasPaths) + "\n");
            }

            text.AppendLine("## Declared Atlases\n");
            foreach (AtlasEntry atlas in Atlases)
            {
                text.AppendLine($"- {atlas.Path}: variant={atlas.IsVariant}, master={atlas.MasterPath}, " +
                    $"includeInBuild={atlas.IncludeInBuild}, textures={atlas.TexturePaths.Length}");
            }

            return text.ToString().Replace("\r\n", "\n");
        }
    }
}
