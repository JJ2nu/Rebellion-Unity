using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// StageManager가 최종 선택한 맵에만 베이크 조명 데이터를 적용하고 비활성화 시 이전 상태를 복구한다.
/// 모든 맵 프리팹이 먼저 생성되므로 OnEnable에서는 전역 조명 데이터를 변경하지 않는다.
/// </summary>
public sealed class MapBakedLightingApplier : MonoBehaviour
{
    private readonly struct RendererState
    {
        public RendererState(Renderer renderer)
        {
            Renderer = renderer;
            LightmapIndex = renderer.lightmapIndex;
            LightmapScaleOffset = renderer.lightmapScaleOffset;
        }

        public Renderer Renderer { get; }
        public int LightmapIndex { get; }
        public Vector4 LightmapScaleOffset { get; }
    }

    private readonly struct ReflectionProbeState
    {
        public ReflectionProbeState(ReflectionProbe probe)
        {
            Probe = probe;
            Mode = probe.mode;
            CustomBakedTexture = probe.customBakedTexture;
        }

        public ReflectionProbe Probe { get; }
        public ReflectionProbeMode Mode { get; }
        public Texture CustomBakedTexture { get; }
    }

    [SerializeField] private MapBakedLightingData lightingData;

    private static MapBakedLightingApplier activeOwner;

    private readonly List<RendererState> rendererStates = new();
    private readonly List<ReflectionProbeState> reflectionProbeStates = new();
    private LightmapData[] previousLightmaps;
    private LightmapsMode previousLightmapsMode;
    private LightProbes previousLightProbes;
    private bool isApplied;

    /// <summary>
    /// 선택 맵의 전역 라이트맵·Probe와 로컬 Renderer·Reflection Probe 매핑을 한 번 적용한다.
    /// </summary>
    public void Apply()
    {
        if (isApplied || lightingData == null)
        {
            return;
        }

        if (!lightingData.HasLightmaps)
        {
            Debug.LogWarning($"Map baked lighting data has no color lightmaps: {name}", this);
            return;
        }

        if (activeOwner != null && activeOwner != this)
        {
            activeOwner.Restore();
        }

        previousLightmaps = LightmapSettings.lightmaps;
        previousLightmapsMode = LightmapSettings.lightmapsMode;
        previousLightProbes = LightmapSettings.lightProbes;

        LightmapSettings.lightmapsMode = lightingData.LightmapsMode;
        LightmapSettings.lightmaps = CreateLightmaps(lightingData);
        if (lightingData.LightProbes != null)
        {
            LightmapSettings.lightProbes = lightingData.LightProbes;
        }

        ApplyRendererBindings();
        ApplyReflectionProbeBindings();
        activeOwner = this;
        isApplied = true;
    }

    /// <summary>
    /// 맵 전환 또는 Scene 종료 시 이 컴포넌트가 바꾼 조명 상태만 이전 값으로 되돌린다.
    /// </summary>
    public void Restore()
    {
        if (!isApplied)
        {
            return;
        }

        LightmapSettings.lightmaps = previousLightmaps ?? Array.Empty<LightmapData>();
        LightmapSettings.lightmapsMode = previousLightmapsMode;
        LightmapSettings.lightProbes = previousLightProbes;

        foreach (RendererState state in rendererStates)
        {
            if (state.Renderer == null)
            {
                continue;
            }

            state.Renderer.lightmapIndex = state.LightmapIndex;
            state.Renderer.lightmapScaleOffset = state.LightmapScaleOffset;
        }

        foreach (ReflectionProbeState state in reflectionProbeStates)
        {
            if (state.Probe == null)
            {
                continue;
            }

            state.Probe.customBakedTexture = state.CustomBakedTexture;
            state.Probe.mode = state.Mode;
        }

        rendererStates.Clear();
        reflectionProbeStates.Clear();
        previousLightmaps = null;
        previousLightProbes = null;
        isApplied = false;

        if (activeOwner == this)
        {
            activeOwner = null;
        }
    }

    private void OnDisable()
    {
        Restore();
    }

    private void OnDestroy()
    {
        Restore();
    }

    private static LightmapData[] CreateLightmaps(MapBakedLightingData data)
    {
        Texture2D[] colorMaps = data.ColorLightmaps;
        Texture2D[] directionalMaps = data.DirectionalLightmaps;
        Texture2D[] masks = data.ShadowMasks;
        LightmapData[] lightmaps = new LightmapData[colorMaps.Length];

        for (int index = 0; index < colorMaps.Length; index++)
        {
            lightmaps[index] = new LightmapData
            {
                lightmapColor = colorMaps[index],
                lightmapDir = GetTextureAt(directionalMaps, index),
                shadowMask = GetTextureAt(masks, index)
            };
        }

        return lightmaps;
    }

    private void ApplyRendererBindings()
    {
        foreach (MapBakedLightingData.RendererLightmapBinding binding in lightingData.RendererBindings)
        {
            Renderer renderer = ResolveComponent<Renderer>(binding.RendererPath);
            if (renderer == null)
            {
                Debug.LogWarning($"Map lightmap renderer was not found: '{binding.RendererPath}'", this);
                continue;
            }

            if (binding.LightmapIndex < 0 || binding.LightmapIndex >= lightingData.ColorLightmaps.Length)
            {
                Debug.LogWarning($"Map lightmap index is out of range: '{binding.RendererPath}' ({binding.LightmapIndex})", this);
                continue;
            }

            rendererStates.Add(new RendererState(renderer));
            renderer.lightmapIndex = binding.LightmapIndex;
            renderer.lightmapScaleOffset = binding.LightmapScaleOffset;
        }
    }

    private void ApplyReflectionProbeBindings()
    {
        foreach (MapBakedLightingData.ReflectionProbeBinding binding in lightingData.ReflectionProbeBindings)
        {
            ReflectionProbe probe = ResolveComponent<ReflectionProbe>(binding.ProbePath);
            if (probe == null || binding.BakedCubemap == null)
            {
                Debug.LogWarning($"Map reflection probe binding is incomplete: '{binding.ProbePath}'", this);
                continue;
            }

            reflectionProbeStates.Add(new ReflectionProbeState(probe));
            probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = binding.BakedCubemap;
        }
    }

    private T ResolveComponent<T>(string relativePath) where T : Component
    {
        Transform target = string.IsNullOrEmpty(relativePath) ? transform : transform.Find(relativePath);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static Texture2D GetTextureAt(Texture2D[] textures, int index)
    {
        return textures != null && index < textures.Length ? textures[index] : null;
    }
}
