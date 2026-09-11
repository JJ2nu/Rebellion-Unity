using System;
using UnityEngine;

/// <summary>
/// 프리팹으로 생성되는 맵에 전달할 베이크 조명 데이터와 Renderer 매핑을 보관한다.
/// Renderer와 Reflection Probe 경로는 이 데이터를 적용하는 맵 루트를 기준으로 한다.
/// </summary>
[CreateAssetMenu(fileName = "MapBakedLightingData", menuName = "Rebellion/Rendering/Map Baked Lighting Data")]
public sealed class MapBakedLightingData : ScriptableObject
{
    [Serializable]
    public struct RendererLightmapBinding
    {
        [SerializeField] private string rendererPath;
        [SerializeField] private int lightmapIndex;
        [SerializeField] private Vector4 lightmapScaleOffset;

        public string RendererPath => rendererPath;
        public int LightmapIndex => lightmapIndex;
        public Vector4 LightmapScaleOffset => lightmapScaleOffset;
    }

    [Serializable]
    public struct ReflectionProbeBinding
    {
        [SerializeField] private string probePath;
        [SerializeField] private Cubemap bakedCubemap;

        public string ProbePath => probePath;
        public Cubemap BakedCubemap => bakedCubemap;
    }

    [Header("Lightmaps")]
    [SerializeField] private LightmapsMode lightmapsMode = LightmapsMode.CombinedDirectional;
    [SerializeField] private Texture2D[] colorLightmaps = Array.Empty<Texture2D>();
    [SerializeField] private Texture2D[] directionalLightmaps = Array.Empty<Texture2D>();
    [SerializeField] private Texture2D[] shadowMasks = Array.Empty<Texture2D>();
    [SerializeField] private LightProbes lightProbes;

    [Header("Prefab Bindings")]
    [SerializeField] private RendererLightmapBinding[] rendererBindings = Array.Empty<RendererLightmapBinding>();
    [SerializeField] private ReflectionProbeBinding[] reflectionProbeBindings = Array.Empty<ReflectionProbeBinding>();

    public LightmapsMode LightmapsMode => lightmapsMode;
    public Texture2D[] ColorLightmaps => colorLightmaps;
    public Texture2D[] DirectionalLightmaps => directionalLightmaps;
    public Texture2D[] ShadowMasks => shadowMasks;
    public LightProbes LightProbes => lightProbes;
    public RendererLightmapBinding[] RendererBindings => rendererBindings;
    public ReflectionProbeBinding[] ReflectionProbeBindings => reflectionProbeBindings;
    public bool HasLightmaps => colorLightmaps != null && colorLightmaps.Length > 0;
}
