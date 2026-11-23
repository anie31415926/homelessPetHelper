using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace HomelessPetHelper.Editor
{
    public static class DesktopBuild
    {
        public static void Build()
        {
            BuildPlayer(BuildOptions.None, "Windows", "win64_player_nondevelopment_mono");
        }

        public static void BuildDevelopment()
        {
            BuildPlayer(BuildOptions.Development, "WindowsDevelopment", "win64_player_development_mono");
        }

        private static void BuildPlayer(BuildOptions buildOptions, string folder, string variation)
        {
            if (!UnityEngine.Application.isBatchMode)
            {
                throw new InvalidOperationException("Use the repository batch build command.");
            }

            string playerPath = Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines", "WindowsStandaloneSupport", "Variations", variation, "WindowsPlayer.exe");
            if (!File.Exists(playerPath))
            {
                throw new FileNotFoundException("Windows player support is incomplete. Repair it in Unity Hub.",
                    playerPath);
            }

            string destination = Path.GetFullPath($"Builds/{folder}/HomelessPetHelper.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Game.Loading.LoadingSceneRoot.ScenePath, ProjectSetup.MainScenePath },
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = buildOptions
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Desktop build failed: {report.summary.result}");
            }

            File.Copy("Assets/Art/Fonts/OFL.txt", Path.Combine(Path.GetDirectoryName(destination),
                "NotoSansSC-OFL.txt"), true);

            UnityEngine.Debug.Log($"Desktop build succeeded. Size: {report.summary.totalSize} bytes.");
        }
    }
}
