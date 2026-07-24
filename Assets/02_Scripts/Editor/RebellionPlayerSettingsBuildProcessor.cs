using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Linq;

public sealed class RebellionPlayerSettingsBuildProcessor : IPreprocessBuildWithReport
{
    private const string IconPath = "Assets/04_Images/Cursor/UI_Cursor_Basic.png";
    private const string BuildVersion = "1.0.1";
    private const string BuildRoot = "Builds";
    private const string ExecutableName = "Rebellion.exe";

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

    [MenuItem("Rebellion/Build Windows/Version 1.0.1")]
    private static void BuildWindowsVersioned()
    {
        ApplyPlayerSettings();

        string outputDirectory = Path.Combine(BuildRoot, BuildVersion);
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
            options = BuildOptions.None
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
        PlayerSettings.bundleVersion = BuildVersion;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.resizableWindow = false;
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
}
