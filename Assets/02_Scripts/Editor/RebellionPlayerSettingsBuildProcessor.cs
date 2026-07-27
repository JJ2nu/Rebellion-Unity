using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Linq;

public sealed class RebellionPlayerSettingsBuildProcessor : IPreprocessBuildWithReport
{
    private const string IconPath = "Assets/04_Images/Cursor/UI_Cursor_Basic.png";
    private const string DefaultBuildVersion = "1.0.1";
    private const string BuildRoot = "Builds";
    private const string ExecutableName = "Rebellion.exe";
    private const string DebugBuildFolderSuffix = "-Debug";

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        ApplyPlayerSettings();
    }

    [MenuItem("Rebellion/Apply Player Settings")]
    private static void ApplyPlayerSettingsMenu()
    {
        ApplyPlayerSettings();
    }

    [MenuItem("Rebellion/Build Windows/Release")]
    private static void BuildWindowsRelease()
    {
        BuildWindows(string.Empty, BuildOptions.None);
    }

    [MenuItem("Rebellion/Build Windows/Debug")]
    private static void BuildWindowsDebug()
    {
        BuildWindows(
            DebugBuildFolderSuffix,
            BuildOptions.Development | BuildOptions.AllowDebugging);
    }

    private static void BuildWindows(string outputFolderSuffix, BuildOptions buildOptions)
    {
        ApplyPlayerSettings();

        string outputFolderName = GetBuildVersion() + outputFolderSuffix;
        string outputDirectory = Path.Combine(BuildRoot, SanitizePathSegment(outputFolderName));
        Directory.CreateDirectory(outputDirectory);

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outputDirectory, ExecutableName),
            target = BuildTarget.StandaloneWindows64,
            options = buildOptions
        });
    }

    private static void ApplyPlayerSettings()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null)
        {
            Debug.LogWarning($"Build icon texture not found at {IconPath}");
            return;
        }

        PlayerSettings.defaultCursor = null;
        PlayerSettings.cursorHotspot = Vector2.zero;
        if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
        {
            PlayerSettings.bundleVersion = DefaultBuildVersion;
        }

        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.resizableWindow = true;
        int[] iconSizes = PlayerSettings.GetIconSizesForTargetGroup(
            BuildTargetGroup.Standalone,
            IconKind.Application);
        Texture2D[] icons = iconSizes.Select(_ => icon).ToArray();
        PlayerSettings.SetIconsForTargetGroup(
            BuildTargetGroup.Standalone,
            icons,
            IconKind.Application);

        AssetDatabase.SaveAssets();
    }

    private static string GetBuildVersion()
    {
        return string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)
            ? DefaultBuildVersion
            : PlayerSettings.bundleVersion.Trim();
    }

    private static string SanitizePathSegment(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        return new string(value.Select(character =>
            invalidChars.Contains(character) ? '_' : character).ToArray());
    }
}
