using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

/// <summary>
/// One-click Android build helper.
/// Usage: Menu -> Build -> Android -> Build APK (Release) or Build AAB (Release)
/// Adjust scene list or keystore settings below as needed.
/// </summary>
public static class AndroidBuild
{
    // List all scenes you want included in the build (in order). If you maintain Build Settings manually,
    // you can instead pull from EditorBuildSettings.scenes.
    private static string[] Scenes => GetEnabledScenes();

    private const string OutputDir = "Builds/Android";

    [MenuItem("Build/Android/Build APK (Release GLES3)")]
    public static void BuildApkReleaseGLES3()
    {
        ConfigureCommonPlayerSettings(gfxOverride: new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        var path = Path.Combine(OutputDir, "Vanguard-GLES3.apk");
        DoBuild(path, BuildOptions.None, false);
    }

    [MenuItem("Build/Android/Build APK (Release Vulkan)")]
    public static void BuildApkReleaseVulkan()
    {
        ConfigureCommonPlayerSettings(gfxOverride: new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        var path = Path.Combine(OutputDir, "Vanguard-Vulkan.apk");
        DoBuild(path, BuildOptions.None, false);
    }

    [MenuItem("Build/Android/Build APK (Development GLES3)")]
    public static void BuildApkDevGLES3()
    {
        ConfigureCommonPlayerSettings(gfxOverride: new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        var path = Path.Combine(OutputDir, "Vanguard-Dev-GLES3.apk");
        DoBuild(path, BuildOptions.Development | BuildOptions.ConnectWithProfiler, false);
    }

    [MenuItem("Build/Android/Build APK (Development Vulkan)")]
    public static void BuildApkDevVulkan()
    {
        ConfigureCommonPlayerSettings(gfxOverride: new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        var path = Path.Combine(OutputDir, "Vanguard-Dev-Vulkan.apk");
        DoBuild(path, BuildOptions.Development | BuildOptions.ConnectWithProfiler, false);
    }

    [MenuItem("Build/Android/Build AAB (Release GLES3)")]
    public static void BuildAabReleaseGLES3()
    {
        ConfigureCommonPlayerSettings(gfxOverride: new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        var path = Path.Combine(OutputDir, "Vanguard-GLES3.aab");
        DoBuild(path, BuildOptions.None, true);
    }

    [MenuItem("Build/Android/Build AAB (Release Vulkan)")]
    public static void BuildAabReleaseVulkan()
    {
        ConfigureCommonPlayerSettings(gfxOverride: new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        var path = Path.Combine(OutputDir, "Vanguard-Vulkan.aab");
        DoBuild(path, BuildOptions.None, true);
    }

    private static void DoBuild(string locationPath, BuildOptions options, bool appBundle)
    {
        if (!Directory.Exists(OutputDir)) Directory.CreateDirectory(OutputDir);

        EditorUserBuildSettings.buildAppBundle = appBundle;
        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = locationPath,
            target = BuildTarget.Android,
            options = options
        };

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        if (report.summary.result == BuildResult.Succeeded)
        {
            UnityEngine.Debug.Log($"[AndroidBuild] SUCCESS {report.summary.outputPath} (size: {report.summary.totalSize / (1024f * 1024f):F1} MB)");
        }
        else
        {
            UnityEngine.Debug.LogError($"[AndroidBuild] FAILED: {report.summary.result}\nErrors: {report.summary.totalErrors} Warnings: {report.summary.totalWarnings}");
        }
    }

    private static string[] GetEnabledScenes()
    {
        var scenes = EditorBuildSettings.scenes;
        var list = new System.Collections.Generic.List<string>();
        foreach (var s in scenes)
        {
            if (s.enabled) list.Add(s.path);
        }
        return list.ToArray();
    }

    private static void ConfigureCommonPlayerSettings(UnityEngine.Rendering.GraphicsDeviceType[] gfxOverride = null)
    {
        // Package & version (adjust as you like)
        PlayerSettings.applicationIdentifier = "com.yourstudio.vanguard";
        if (PlayerSettings.bundleVersion == "0.1.0") { /* keep */ }
        // Increment versionCode automatically if desired:
        PlayerSettings.Android.bundleVersionCode = System.Math.Max(1, PlayerSettings.Android.bundleVersionCode);

        // Backend & architectures
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; // add ARMv7 if you want wider support
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23; // Redmi Note 12 Pro is fine
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

    // Graphics APIs: allow override (default remains GLES3 only)
    var apis = gfxOverride ?? new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 };
    PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
    PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, apis);

    // Managed stripping level (avoid obsolete API). The old PlayerSettings.strippingLevel is deprecated.
    // Use the newer Managed Stripping Level setter when available for this Unity version.
#if UNITY_2021_2_OR_NEWER
    PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
#endif
    // (If you need a higher stripping level for release, change to ManagedStrippingLevel.Medium/High.)

    // Omit IL2CPP code generation optimization API for 2021 LTS (added in later Unity versions).
    // When upgrading to a newer Unity (e.g. 2022.2+), you can enable:
    // #if UNITY_2022_2_OR_NEWER
    // PlayerSettings.SetIl2CppCodeGeneration(BuildTargetGroup.Android, Il2CppCodeGeneration.OptimizeSpeed);
    // #endif

        // Orientation
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        // Disable legacy splash (optional)
        PlayerSettings.SplashScreen.show = false;
    }
}
