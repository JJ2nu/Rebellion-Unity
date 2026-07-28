#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 시연 전용 Prefab과 두 Scene을 같은 참조 구조로 다시 만들 수 있게 하는 Editor 제작 도구다.
/// 출시 빌드에는 Editor 폴더와 Demo Scene이 포함되지 않는다.
/// </summary>
public static class DemoSceneBuilder
{
    private const string DemoRoot = "Assets/10_Demo";
    private const string ScenesFolder = DemoRoot + "/Scenes";
    private const string PrefabsFolder = DemoRoot + "/Prefabs";
    private const string ContentFolder = DemoRoot + "/Content";
    private const string DemoSessionPrefabPath = PrefabsFolder + "/DemoSessionRoot.prefab";
    private const string CancelActionReferencePath = PrefabsFolder + "/DemoCancelAction.asset";
    private const string BootstrapScenePath = ScenesFolder + "/DemoBootstrap.unity";
    private const string TimeOverScenePath = ScenesFolder + "/DemoTimeOver.unity";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

    [MenuItem("Rebellion/Demo/Rebuild Demo Scenes")]
    public static void RebuildDemoScenes()
    {
        EnsureFolders();
        InputActionReference cancelActionReference = CreateOrUpdateCancelActionReference();
        GameObject sessionPrefab = CreateSessionPrefab(cancelActionReference);
        CreateBootstrapScene(sessionPrefab);
        CreateTimeOverScene();
        EnsureDemoScenesInEditorBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene("Assets/01_Scenes/Title.unity", OpenSceneMode.Single);
        Debug.Log("[DemoSceneBuilder] DemoSessionRoot, DemoBootstrap and DemoTimeOver rebuilt.");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "10_Demo");
        EnsureFolder(DemoRoot, "Scenes");
        EnsureFolder(DemoRoot, "Prefabs");
        EnsureFolder(DemoRoot, "Content");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static InputActionReference CreateOrUpdateCancelActionReference()
    {
        InputActionAsset inputActions =
            AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        InputAction cancelAction = inputActions?.FindAction("UI/Cancel", false);
        if (cancelAction == null)
        {
            throw new UnityException(
                $"Demo UI/Cancel action was not found in {InputActionsPath}.");
        }

        InputActionReference reference =
            AssetDatabase.LoadAssetAtPath<InputActionReference>(CancelActionReferencePath);
        if (reference == null)
        {
            reference = InputActionReference.Create(cancelAction);
            AssetDatabase.CreateAsset(reference, CancelActionReferencePath);
        }
        else
        {
            reference.Set(cancelAction);
            EditorUtility.SetDirty(reference);
        }

        return reference;
    }

    private static GameObject CreateSessionPrefab(InputActionReference cancelActionReference)
    {
        GameObject root = new("DemoSessionRoot");
        DemoSessionController controller = root.AddComponent<DemoSessionController>();

        GameObject canvasObject = new("TimerCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(root.transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject timerObject = new("Timer", typeof(RectTransform));
        timerObject.transform.SetParent(canvasObject.transform, false);
        RectTransform timerTransform = timerObject.GetComponent<RectTransform>();
        timerTransform.anchorMin = new Vector2(0.5f, 1f);
        timerTransform.anchorMax = new Vector2(0.5f, 1f);
        timerTransform.pivot = new Vector2(0.5f, 1f);
        timerTransform.anchoredPosition = new Vector2(0f, -24f);
        timerTransform.sizeDelta = new Vector2(340f, 76f);

        Image timerBackground = timerObject.AddComponent<Image>();
        timerBackground.color = new Color(0f, 0f, 0f, 0.65f);
        timerBackground.raycastTarget = false;
        DemoTimerView timerView = timerObject.AddComponent<DemoTimerView>();

        GameObject textObject = new("TimerText", typeof(RectTransform));
        textObject.transform.SetParent(timerObject.transform, false);
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI timerText = textObject.AddComponent<TextMeshProUGUI>();
        timerText.text = "30:00";
        timerText.font = TMP_Settings.defaultFontAsset;
        timerText.fontSize = 44f;
        timerText.fontStyle = FontStyles.Bold;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.color = Color.white;
        timerText.raycastTarget = false;

        SerializedObject viewObject = new(timerView);
        viewObject.FindProperty("timerText").objectReferenceValue = timerText;
        viewObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject controllerObject = new(controller);
        controllerObject.FindProperty("allowEditorPreview").boolValue = true;
        controllerObject.FindProperty("enableChallengeMode").boolValue = false;
        controllerObject.FindProperty("sessionDurationMinutes").floatValue = 30f;
        controllerObject.FindProperty("timeOverDurationSeconds").floatValue = 10f;
        controllerObject.FindProperty("warningThresholdMinutes").floatValue = 5f;
        controllerObject.FindProperty("criticalThresholdMinutes").floatValue = 1f;
        controllerObject.FindProperty("criticalBlinkFrequency").floatValue = 2f;
        controllerObject.FindProperty("criticalBlinkMinimumAlpha").floatValue = 0.2f;
        controllerObject.FindProperty("timerView").objectReferenceValue = timerView;
        controllerObject.FindProperty("cancelAction").objectReferenceValue = cancelActionReference;
        controllerObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DemoSessionPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CreateBootstrapScene(GameObject sessionPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCameraAndLight();
        PrefabUtility.InstantiatePrefab(sessionPrefab, scene);
        EditorSceneManager.SaveScene(scene, BootstrapScenePath);
    }

    private static void CreateTimeOverScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCameraAndLight();

        GameObject eventSystemObject = new(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));

        GameObject canvasObject = new("Canvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new("PromotionPanel", typeof(RectTransform));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelTransform = panelObject.GetComponent<RectTransform>();
        panelTransform.anchorMin = Vector2.zero;
        panelTransform.anchorMax = Vector2.one;
        panelTransform.offsetMin = Vector2.zero;
        panelTransform.offsetMax = Vector2.zero;
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.035f, 0.035f, 0.045f, 1f);
        panelImage.raycastTarget = true;

        GameObject contentRoot = new("PromotionContentRoot", typeof(RectTransform));
        contentRoot.transform.SetParent(panelObject.transform, false);
        RectTransform contentTransform = contentRoot.GetComponent<RectTransform>();
        contentTransform.anchorMin = Vector2.zero;
        contentTransform.anchorMax = Vector2.one;
        contentTransform.offsetMin = Vector2.zero;
        contentTransform.offsetMax = Vector2.zero;

        Selection.activeGameObject = contentRoot;
        EditorSceneManager.SaveScene(scene, TimeOverScenePath);
    }

    private static void EnsureDemoScenesInEditorBuildSettings()
    {
        // Editor Play Mode에서도 이름 기반 Scene 로드를 검증할 수 있게 공유 목록에는 등록한다.
        // Release/Debug 빌드 함수는 DemoAssetRoot를 다시 필터링하므로 Player에는 포함되지 않는다.
        List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
        EnsureEnabledBuildScene(scenes, BootstrapScenePath);
        EnsureEnabledBuildScene(scenes, TimeOverScenePath);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureEnabledBuildScene(
        List<EditorBuildSettingsScene> scenes,
        string scenePath)
    {
        for (int index = 0; index < scenes.Count; index++)
        {
            if (scenes[index].path != scenePath)
            {
                continue;
            }

            scenes[index] = new EditorBuildSettingsScene(scenePath, true);
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
    }

    private static void CreateCameraAndLight()
    {
        GameObject cameraObject = new("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.01f, 0.01f, 0.015f, 1f);
        cameraObject.AddComponent<AudioListener>();

        GameObject lightObject = new("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }
}
#endif
