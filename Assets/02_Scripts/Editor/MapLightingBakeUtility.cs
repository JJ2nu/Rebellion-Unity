using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 인게임 맵 전용 베이크 작업 Scene의 베이크 설정, 실행, 런타임 데이터 캡처를 맵별 메뉴로 처리한다.
/// 맵을 추가할 때는 <see cref="BakeTarget"/> 하나와 메뉴 메서드 2개만 늘리면 된다.
/// </summary>
public static class MapLightingBakeUtility
{
    /// <summary>
    /// 맵 하나의 베이크 대상 경로 묶음. MapRootName은 베이크 Scene 루트 오브젝트 이름과 같아야 한다.
    /// </summary>
    private sealed class BakeTarget
    {
        public string MapRootName;
        public string BakeScenePath;
        public string LightingSettingsPath;
        public string BakedDataPath;
        public string MapPrefabPath;
        public string ReflectionProbeName;
        // 맵별 라이트맵 밀도(texels/m)와 atlas 최대 크기. 창문 광반처럼 또렷한 경계가 필요하면 올린다.
        public float BakeResolution = 15f;
        public int LightmapMaxSize = 1024;
    }

    private static readonly BakeTarget BarTarget = new BakeTarget
    {
        MapRootName = "Map_Bar",
        BakeScenePath = "Assets/Scenes/Map_Bar_LightingBake.unity",
        LightingSettingsPath = "Assets/09_Scriptables/Rendering/Map_Bar_LightingSettings.asset",
        BakedDataPath = "Assets/09_Scriptables/Rendering/Map_Bar_BakedLighting.asset",
        MapPrefabPath = "Assets/03_Prefabs/Maps/InGame/Map_Bar.prefab",
        ReflectionProbeName = "ReflectionProbe_Bar_Baked"
    };

    private static readonly BakeTarget MuseumTarget = new BakeTarget
    {
        MapRootName = "Map_Museum",
        BakeScenePath = "Assets/Scenes/Map_Museum_LightingBake.unity",
        LightingSettingsPath = "Assets/09_Scriptables/Rendering/Map_Museum_LightingSettings.asset",
        BakedDataPath = "Assets/09_Scriptables/Rendering/Map_Museum_BakedLighting.asset",
        MapPrefabPath = "Assets/03_Prefabs/Maps/InGame/Map_Museum.prefab",
        ReflectionProbeName = "ReflectionProbe_Museum_Baked",
        BakeResolution = 25f,
        LightmapMaxSize = 2048
    };

    private static BakeTarget pendingTarget;

    [MenuItem("Tools/Rebellion/Map Lighting/Map_Bar/Configure, Bake And Capture")]
    private static void ConfigureBakeAndCaptureBar() => ConfigureBakeAndCapture(BarTarget);

    [MenuItem("Tools/Rebellion/Map Lighting/Map_Bar/Capture Current Bake")]
    private static void CaptureCurrentBakeBar() => CaptureCurrentBake(BarTarget);

    [MenuItem("Tools/Rebellion/Map Lighting/Map_Museum/Configure, Bake And Capture")]
    private static void ConfigureBakeAndCaptureMuseum() => ConfigureBakeAndCapture(MuseumTarget);

    [MenuItem("Tools/Rebellion/Map Lighting/Map_Museum/Capture Current Bake")]
    private static void CaptureCurrentBakeMuseum() => CaptureCurrentBake(MuseumTarget);

    private static void ConfigureBakeAndCapture(BakeTarget target)
    {
        if (!TryGetBakeContext(target, out Scene scene, out GameObject mapRoot))
        {
            return;
        }

        LightingSettings settings = GetOrCreateLightingSettings(target);
        ConfigureLightingSettings(settings, target);
        Lightmapping.lightingSettings = settings;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (!Lightmapping.BakeAsync())
        {
            Debug.LogError($"[MapLightingBake] 베이크를 시작하지 못했습니다: {target.MapRootName}. Lighting 창과 Console을 확인하세요.");
            return;
        }

        // 베이크가 끝난 뒤 같은 대상의 캡처를 자동으로 이어서 실행한다.
        pendingTarget = target;
        EditorApplication.update -= CaptureWhenBakeFinishes;
        EditorApplication.update += CaptureWhenBakeFinishes;
        Debug.Log($"[MapLightingBake] 베이크를 시작했습니다: {mapRoot.name}");
    }

    private static void CaptureCurrentBake(BakeTarget target)
    {
        if (!TryGetBakeContext(target, out _, out GameObject mapRoot))
        {
            return;
        }

        MapBakedLightingData data = AssetDatabase.LoadAssetAtPath<MapBakedLightingData>(target.BakedDataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<MapBakedLightingData>();
            AssetDatabase.CreateAsset(data, target.BakedDataPath);
        }

        SerializedObject serializedData = new SerializedObject(data);
        WriteLightmapTextures(serializedData);
        // enumValueIndex는 enum 이름 배열 순서를 쓰므로 실제 값이 어긋날 수 있다. 값 자체를 저장한다.
        serializedData.FindProperty("lightmapsMode").intValue = (int)LightmapSettings.lightmapsMode;
        serializedData.FindProperty("lightProbes").objectReferenceValue = LightmapSettings.lightProbes;
        WriteRendererBindings(serializedData.FindProperty("rendererBindings"), mapRoot.transform);
        WriteReflectionProbeBindings(serializedData.FindProperty("reflectionProbeBindings"), mapRoot.transform, target.ReflectionProbeName);
        serializedData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);

        ConnectDataToPrefab(data, target.MapPrefabPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MapLightingBake] 런타임 베이크 데이터를 기록했습니다: {target.BakedDataPath}", data);
    }

    private static void CaptureWhenBakeFinishes()
    {
        if (pendingTarget == null || Lightmapping.isRunning)
        {
            return;
        }

        BakeTarget target = pendingTarget;
        pendingTarget = null;
        EditorApplication.update -= CaptureWhenBakeFinishes;
        CaptureCurrentBake(target);
    }

    private static LightingSettings GetOrCreateLightingSettings(BakeTarget target)
    {
        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(target.LightingSettingsPath);
        if (settings != null)
        {
            return settings;
        }

        settings = new LightingSettings
        {
            name = System.IO.Path.GetFileNameWithoutExtension(target.LightingSettingsPath)
        };
        AssetDatabase.CreateAsset(settings, target.LightingSettingsPath);
        return settings;
    }

    private static void ConfigureLightingSettings(LightingSettings settings, BakeTarget target)
    {
        // Unity 6의 LightingSettings 직렬화 필드를 사용해 버전별 공개 API 차이를 피한다.
        SerializedObject serializedSettings = new SerializedObject(settings);
        SetBoolean(serializedSettings, "m_EnableBakedLightmaps", true);
        SetBoolean(serializedSettings, "m_EnableRealtimeLightmaps", false);
        SetInteger(serializedSettings, "m_MixedBakeMode", 2); // Shadowmask
        SetFloat(serializedSettings, "m_BakeResolution", target.BakeResolution);
        SetInteger(serializedSettings, "m_LightmapMaxSize", target.LightmapMaxSize);
        SetBoolean(serializedSettings, "m_AO", false);
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static void WriteLightmapTextures(SerializedObject serializedData)
    {
        LightmapData[] lightmaps = LightmapSettings.lightmaps ?? Array.Empty<LightmapData>();
        SerializedProperty colors = serializedData.FindProperty("colorLightmaps");
        SerializedProperty directions = serializedData.FindProperty("directionalLightmaps");
        SerializedProperty masks = serializedData.FindProperty("shadowMasks");
        colors.arraySize = lightmaps.Length;
        directions.arraySize = lightmaps.Length;
        masks.arraySize = lightmaps.Length;

        for (int index = 0; index < lightmaps.Length; index++)
        {
            colors.GetArrayElementAtIndex(index).objectReferenceValue = lightmaps[index].lightmapColor;
            directions.GetArrayElementAtIndex(index).objectReferenceValue = lightmaps[index].lightmapDir;
            masks.GetArrayElementAtIndex(index).objectReferenceValue = lightmaps[index].shadowMask;
        }
    }

    private static void WriteRendererBindings(SerializedProperty bindings, Transform mapRoot)
    {
        List<Renderer> bakedRenderers = new List<Renderer>();
        foreach (Renderer renderer in mapRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.lightmapIndex >= 0 && renderer.lightmapIndex < LightmapSettings.lightmaps.Length)
            {
                bakedRenderers.Add(renderer);
            }
        }

        bindings.arraySize = bakedRenderers.Count;
        for (int index = 0; index < bakedRenderers.Count; index++)
        {
            Renderer renderer = bakedRenderers[index];
            SerializedProperty binding = bindings.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("rendererPath").stringValue = AnimationUtility.CalculateTransformPath(renderer.transform, mapRoot);
            binding.FindPropertyRelative("lightmapIndex").intValue = renderer.lightmapIndex;
            binding.FindPropertyRelative("lightmapScaleOffset").vector4Value = renderer.lightmapScaleOffset;
        }
    }

    private static void WriteReflectionProbeBindings(SerializedProperty bindings, Transform mapRoot, string reflectionProbeName)
    {
        ReflectionProbe probe = null;
        foreach (ReflectionProbe candidate in mapRoot.GetComponentsInChildren<ReflectionProbe>(true))
        {
            if (candidate.name == reflectionProbeName)
            {
                probe = candidate;
                break;
            }
        }

        Cubemap bakedCubemap = probe != null ? probe.bakedTexture as Cubemap : null;
        bindings.arraySize = bakedCubemap != null ? 1 : 0;
        if (bakedCubemap == null)
        {
            Debug.LogWarning($"[MapLightingBake] '{reflectionProbeName}'의 베이크 Cubemap을 찾지 못했습니다.", mapRoot);
            return;
        }

        SerializedProperty binding = bindings.GetArrayElementAtIndex(0);
        binding.FindPropertyRelative("probePath").stringValue = AnimationUtility.CalculateTransformPath(probe.transform, mapRoot);
        binding.FindPropertyRelative("bakedCubemap").objectReferenceValue = bakedCubemap;
    }

    private static void ConnectDataToPrefab(MapBakedLightingData data, string mapPrefabPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(mapPrefabPath);
        try
        {
            MapBakedLightingApplier applier = prefabRoot.GetComponent<MapBakedLightingApplier>();
            if (applier == null)
            {
                applier = prefabRoot.AddComponent<MapBakedLightingApplier>();
            }

            SerializedObject serializedApplier = new SerializedObject(applier);
            serializedApplier.FindProperty("lightingData").objectReferenceValue = data;
            serializedApplier.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, mapPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool TryGetBakeContext(BakeTarget target, out Scene scene, out GameObject mapRoot)
    {
        scene = SceneManager.GetActiveScene();
        mapRoot = null;
        if (scene.path != target.BakeScenePath)
        {
            Debug.LogError($"[MapLightingBake] 전용 Scene을 먼저 여세요: {target.BakeScenePath}");
            return false;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == target.MapRootName)
            {
                mapRoot = root;
                break;
            }
        }

        if (mapRoot != null)
        {
            return true;
        }

        Debug.LogError($"[MapLightingBake] Scene 루트에서 '{target.MapRootName}'을 찾지 못했습니다.");
        return false;
    }

    private static void SetBoolean(SerializedObject target, string propertyName, bool value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetInteger(SerializedObject target, string propertyName, int value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetFloat(SerializedObject target, string propertyName, float value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }
}
