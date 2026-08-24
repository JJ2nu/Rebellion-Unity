using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Linq;

internal enum RebellionWindowsBuildKind
{
    Release,
    Debug,
    Demo,
}

internal enum RebellionWebBuildKind
{
    Development,
    Release,
}

public sealed class RebellionPlayerSettingsBuildProcessor : IPreprocessBuildWithReport
{
    private const string IconPath = "Assets/04_Images/UI/icon.png";
    private const string DefaultBuildVersion = "1.0.1";
    private const string BuildRoot = "Builds";
    private const string WebBuildRoot = "Builds/Web";
    private const string ExecutableName = "ReBellion.exe";
    private const string DebugBuildFolderSuffix = "-Debug";
    private const string DemoBuildFolderSuffix = "-Demo";
    private const string WebDevelopmentFolderSuffix = "-Development";
    private const string DemoBuildDefine = "REBELLION_DEMO_BUILD";
    private const string DemoAssetRoot = "Assets/10_Demo/";
    private const string DemoBootstrapScenePath = "Assets/10_Demo/Scenes/DemoBootstrap.unity";
    private const string DemoTimeOverScenePath = "Assets/10_Demo/Scenes/DemoTimeOver.unity";

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
        RebellionBuildVersionWindow.Open(RebellionWindowsBuildKind.Release, GetBuildVersion());
    }

    [MenuItem("Rebellion/Build Windows/Debug")]
    private static void BuildWindowsDebug()
    {
        RebellionBuildVersionWindow.Open(RebellionWindowsBuildKind.Debug, GetBuildVersion());
    }

    [MenuItem("Rebellion/Build Windows/Demo")]
    private static void BuildWindowsDemo()
    {
        RebellionBuildVersionWindow.Open(RebellionWindowsBuildKind.Demo, GetBuildVersion());
    }

    [MenuItem("Rebellion/Build Web/Development")]
    public static void BuildWebDevelopment()
    {
        BuildWeb(RebellionWebBuildKind.Development, GetBuildVersion());
    }

    [MenuItem("Rebellion/Build Web/Release")]
    public static void BuildWebRelease()
    {
        BuildWeb(RebellionWebBuildKind.Release, GetBuildVersion());
    }

    internal static void BuildWindows(RebellionWindowsBuildKind buildKind, string buildVersion)
    {
        buildVersion = buildVersion.Trim();
        PlayerSettings.bundleVersion = buildVersion;
        ApplyPlayerSettings();

        string outputFolderSuffix = buildKind switch
        {
            RebellionWindowsBuildKind.Debug => DebugBuildFolderSuffix,
            RebellionWindowsBuildKind.Demo => DemoBuildFolderSuffix,
            _ => string.Empty,
        };
        BuildOptions buildOptions = buildKind == RebellionWindowsBuildKind.Debug
            ? BuildOptions.Development | BuildOptions.AllowDebugging
            : BuildOptions.None;
        bool includeDemoContent = buildKind == RebellionWindowsBuildKind.Demo;

        string outputFolderName = buildVersion + outputFolderSuffix;
        string outputDirectory = Path.Combine(BuildRoot, SanitizePathSegment(outputFolderName));
        Directory.CreateDirectory(outputDirectory);

        string[] regularScenes = GetRegularScenes();

        string[] scenes = includeDemoContent
            ? new[] { DemoBootstrapScenePath }
                .Concat(regularScenes)
                .Concat(new[] { DemoTimeOverScenePath })
                .ToArray()
            : regularScenes;

        BuildPlayerOptions playerOptions = new()
        {
            scenes = scenes,
            locationPathName = Path.Combine(outputDirectory, ExecutableName),
            target = BuildTarget.StandaloneWindows64,
            options = buildOptions
        };

        if (includeDemoContent)
        {
            // 전역 Player Settings를 오염시키지 않고 이번 Demo 빌드 컴파일에만 시연 코드를 포함한다.
            playerOptions.extraScriptingDefines = new[] { DemoBuildDefine };
        }

        BuildPipeline.BuildPlayer(playerOptions);
    }

    internal static void BuildWeb(RebellionWebBuildKind buildKind, string buildVersion)
    {
        buildVersion = buildVersion.Trim();
        PlayerSettings.bundleVersion = buildVersion;
        ApplyPlayerSettings();

        // itch.io는 Unity의 .br 산출물에 Brotli Content-Encoding을 적용한다.
        // Compression Format은 Development 빌드에는 적용되지 않지만 Release 빌드에는 반드시 사용한다.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;

        bool isDevelopment = buildKind == RebellionWebBuildKind.Development;
        if (!isDevelopment)
        {
            // 제출 Release는 최신 원본 GLB와 대형 텍스처를 Web 전용 압축 상태로 먼저 갱신한다.
            GlbExternalTextureConverter.OptimizeWebAssets();
        }

        string outputFolderName = buildVersion
            + (isDevelopment ? WebDevelopmentFolderSuffix : string.Empty);
        string outputDirectory = Path.Combine(
            WebBuildRoot,
            SanitizePathSegment(outputFolderName));
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, true);
        }

        Directory.CreateDirectory(outputDirectory);

        // Web 제출 빌드도 Windows Release와 같은 일반 Scene만 사용해 Demo 전용 코드와 콘텐츠를 제외한다.
        BuildPlayerOptions playerOptions = new()
        {
            scenes = GetRegularScenes(),
            locationPathName = outputDirectory,
            target = BuildTarget.WebGL,
            options = BuildOptions.DetailedBuildReport
                | (isDevelopment ? BuildOptions.Development : BuildOptions.None)
        };

        BuildReport report = BuildPipeline.BuildPlayer(playerOptions);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"Web {buildKind} build failed with result {report.summary.result}.");
        }

        if (!isDevelopment)
        {
            WebBuildPackageUtility.ValidateAndPackage(outputDirectory, buildVersion);
        }
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

    private static string[] GetRegularScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .Where(path => !path.StartsWith(DemoAssetRoot))
            .ToArray();
    }

    private static string SanitizePathSegment(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        return new string(value.Select(character =>
            invalidChars.Contains(character) ? '_' : character).ToArray());
    }
}

internal sealed class RebellionBuildVersionWindow : EditorWindow
{
    private const float WindowWidth = 360f;
    private const float WindowHeight = 116f;

    [SerializeField] private RebellionWindowsBuildKind buildKind;
    [SerializeField] private string buildVersion;

    internal static void Open(RebellionWindowsBuildKind buildKind, string currentVersion)
    {
        RebellionBuildVersionWindow window = CreateInstance<RebellionBuildVersionWindow>();
        window.buildKind = buildKind;
        window.buildVersion = currentVersion;
        window.titleContent = new GUIContent($"{buildKind} Build");
        window.minSize = new Vector2(WindowWidth, WindowHeight);
        window.maxSize = window.minSize;
        window.ShowUtility();
        window.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10f);
        GUI.SetNextControlName("BuildVersionField");
        buildVersion = EditorGUILayout.TextField("Build Version", buildVersion);
        EditorGUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(90f)))
            {
                Close();
            }

            using (new EditorGUI.DisabledScope(!IsVersionValid(buildVersion)))
            {
                if (GUILayout.Button("Build", GUILayout.Width(90f)))
                {
                    string requestedVersion = buildVersion.Trim();
                    Close();
                    RebellionPlayerSettingsBuildProcessor.BuildWindows(buildKind, requestedVersion);
                    GUIUtility.ExitGUI();
                }
            }
        }

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.FocusTextInControl("BuildVersionField");
        }
    }

    private static bool IsVersionValid(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        string trimmedVersion = version.Trim();
        return trimmedVersion.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && !trimmedVersion.Contains('/')
            && !trimmedVersion.Contains('\\');
    }
}
