using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeChatWASM;

public static class WeChatEnvironmentProbe
{
    [Serializable]
    private sealed class InstallationReport
    {
        public string UnityVersion;
        public bool WebGlSupported;
        public string SdkPackage;
        public string SdkSource;
        public bool RuntimeAssemblyLoaded;
        public bool EditorAssemblyLoaded;
        public bool AssetBundleModuleLoaded;
        public string PluginVersion;
        public bool AppIdConfigured;
    }

    public static void Verify()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var sdk = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
            .Single(item => item.name == "com.qq.weixin.minigame");
        var plugin = assemblies.Single(item => item.GetName().Name == "WxEditor")
            .GetType("WeChatWASM.WXPluginVersion", true);
        var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
            "Packages/com.qq.weixin.minigame/Editor/MiniGameConfig.asset");
        var appId = config == null ? null : new SerializedObject(config).FindProperty("ProjectConf.Appid");
        var report = new InstallationReport
        {
            UnityVersion = Application.unityVersion,
            WebGlSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL),
            SdkPackage = sdk.packageId,
            SdkSource = sdk.source.ToString(),
            RuntimeAssemblyLoaded = assemblies.Any(item => item.GetName().Name == "Wx"),
            EditorAssemblyLoaded = assemblies.Any(item => item.GetName().Name == "WxEditor"),
            AssetBundleModuleLoaded = typeof(AssetBundle).Assembly.GetName().Name == "UnityEngine.AssetBundleModule",
            PluginVersion = (string)plugin.GetField("pluginVersion").GetValue(null),
            AppIdConfigured = !string.IsNullOrWhiteSpace(appId?.stringValue)
        };
        Directory.CreateDirectory("ProbeReports");
        File.WriteAllText("ProbeReports/installation.json", JsonUtility.ToJson(report, true) + "\n");
        if (!report.WebGlSupported || !report.RuntimeAssemblyLoaded || !report.EditorAssemblyLoaded ||
            !report.AssetBundleModuleLoaded)
        {
            throw new InvalidOperationException("WeChat environment verification failed.");
        }

        Debug.Log("WECHAT_ENVIRONMENT_VERIFIED: " + JsonUtility.ToJson(report));
    }

    public static void BuildWebGl()
    {
        Verify();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("ProbeCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.25f, 0.55f, 0.42f);
        Directory.CreateDirectory("Assets/ProbeScenes");
        const string scenePath = "Assets/ProbeScenes/EnvironmentProbe.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        BuildPlayer(new[] { scenePath });
    }

    public static void BuildGameWebGl()
    {
        Verify();
        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
            .Select(scene => scene.path).ToArray();
        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled game scenes in Editor Build Settings.");
        }

        BuildPlayer(scenes);
    }

    public static void ExportGame()
    {
        Verify();
        string appId = Environment.GetEnvironmentVariable("HPH_WECHAT_APP_ID");
        if (!Regex.IsMatch(appId ?? string.Empty, @"^wx[0-9a-f]{16}$"))
        {
            throw new InvalidOperationException("A registered mini game AppID is required.");
        }

        const string output = "WeChatCandidate";
        var config = WXConvertCore.config;
        config.ProjectConf.Appid = appId;
        config.ProjectConf.projectName = "Homeless Pet Helper";
        config.ProjectConf.bgImageSrc = "Assets/Art/Loading/T_WeChat_Cover.jpg";
        config.ProjectConf.HideAfterCallMain = true;
        config.ProjectConf.Orientation = (WXScreenOritation)0;
        config.ProjectConf.relativeDST = output;
        config.ProjectConf.DST = Path.GetFullPath(output);
        config.ProjectConf.CDN = string.Empty;
        config.ProjectConf.assetLoadType = 1;
        config.ProjectConf.compressDataPackage = true;
        config.CompileOptions.DevelopBuild = false;
        config.CompileOptions.Il2CppOptimizeSize = true;
        config.CompileOptions.Webgl2 = true;
        config.CompileOptions.profilingFuncs = false;
        config.CompileOptions.fbslim = false;
        config.CompileOptions.enablePerfAnalysis = false;

        var result = WXConvertCore.DoExport();
        string miniGame = Path.Combine(output, "minigame");
        bool hasProject = File.Exists(Path.Combine(miniGame, "project.config.json"));
        File.WriteAllText("ProbeReports/wechat-export.json", JsonUtility.ToJson(new ExportResult
        {
            Result = result.ToString(),
            HasMiniGameProject = hasProject,
            EffectiveResourceMode = config.ProjectConf.assetLoadType == 1 ? "Package" : "CDN",
            DataCompressed = config.ProjectConf.compressDataPackage
        }, true) + "\n");
        if (result != WXConvertCore.WXExportError.SUCCEED || !hasProject)
        {
            throw new InvalidOperationException("WeChat SDK export did not produce a mini game project.");
        }
    }

    [Serializable]
    private sealed class ExportResult
    {
        public string Result;
        public bool HasMiniGameProject;
        public string EffectiveResourceMode;
        public bool DataCompressed;
    }

    private static void BuildPlayer(string[] scenes)
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.threadsSupport = false;
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "ProbeBuild/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.Development
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        File.WriteAllText("ProbeReports/webgl-build.json", JsonUtility.ToJson(new BuildResult
        {
            Scenes = scenes,
            DefaultOrientation = PlayerSettings.defaultInterfaceOrientation.ToString(),
            Result = report.summary.result.ToString(),
            TotalBytes = report.summary.totalSize,
            Errors = report.summary.totalErrors,
            Warnings = report.summary.totalWarnings,
            WarningMessages = report.steps.SelectMany(step => step.messages)
                .Where(message => message.type == LogType.Warning)
                .Select(message => message.content).Distinct().ToArray()
        }, true) + "\n");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new InvalidOperationException("WebGL probe build failed.");
        }

        Debug.Log("WECHAT_WEBGL_PROBE_SUCCEEDED: " + report.summary.totalSize + " bytes. Not a WeChat export.");
    }

    [Serializable]
    private sealed class BuildResult
    {
        public string[] Scenes;
        public string DefaultOrientation;
        public string Result;
        public ulong TotalBytes;
        public int Errors;
        public int Warnings;
        public string[] WarningMessages;
    }
}
