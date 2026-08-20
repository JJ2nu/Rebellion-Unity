using UnityEngine;

/// <summary>
/// 호버 시 외곽선을 표시한다.
/// 마스크 렌더러가 유닛 실루엣을 스텐실에 먼저 기록하고, 헐 렌더러가 실루엣 바깥에만 외곽선을 그려서
/// 메쉬(파츠)별 내부 경계선 없이 유닛 전체 테두리 하나만 보이게 한다.
/// </summary>
public class OutlineEffect : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;
    private const float DefaultOutlineWidth = 0.03f;
    // 모든 마스크가 먼저 그려진 뒤 헐이 그려지도록 렌더 큐를 분리한다.
    private const int MaskRenderQueue = 2001;
    private const int HullRenderQueue = 2002;

    // 겹쳐 보이는 유닛끼리 서로의 외곽선을 지우지 않도록 유닛마다 1~255 스텐실 참조값을 순환 할당한다.
    private static int nextStencilRef = 1;

    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private Transform[] excludedRoots = System.Array.Empty<Transform>();
    private readonly System.Collections.Generic.List<Renderer> duplicatedOutlineRenderers = new();
    // 한 Renderer 안에 여러 서브메시가 있어도 전부 외곽선을 그리도록 기본적으로 복제 렌더러를 사용한다.
    private bool useDuplicatedRenderers = true;
    private float outlineWidth = DefaultOutlineWidth;
    private bool isOutlineVisible;
    private bool isPersistent;
    private int stencilRef;
    private Material maskMaterialInstance;
    private Material hullMaterialInstance;

    private void Awake()
    {
        EnsureCache();
    }

    private void OnDestroy()
    {
        DestroyDuplicatedOutlineRenderers();
        DestroyMaterialInstances();
    }

    public void SetOutlineMaterial(Material material)
    {
        if (isOutlineVisible)
        {
            RemoveAppliedOutline();
            isOutlineVisible = false;
        }

        outlineMaterial = material;
        DestroyMaterialInstances();
        EnsureCache();
    }

    public void SetExcludedRoots(params Transform[] roots)
    {
        if (isOutlineVisible)
        {
            RemoveAppliedOutline();
            isOutlineVisible = false;
        }

        excludedRoots = roots ?? System.Array.Empty<Transform>();
        renderers = null;
        originalMaterials = null;
        EnsureCache();
    }

    public void SetUseDuplicatedRenderers(bool useDuplicates)
    {
        if (isOutlineVisible)
        {
            RemoveAppliedOutline();
            isOutlineVisible = false;
        }

        useDuplicatedRenderers = useDuplicates;
    }

    public void SetOutlineWidth(float width)
    {
        outlineWidth = Mathf.Max(0f, width);
    }

    public void Show()
    {
        ShowWithColor(Color.white);
    }

    public void ShowWithColor(Color color)
    {
        if (outlineMaterial == null) return;
        if (isPersistent) return;

        ApplyOutline(color);
    }

    private void ApplyOutline(Color color)
    {
        if (outlineMaterial == null) return;
        EnsureCache();

        if (isOutlineVisible)
        {
            RemoveAppliedOutline();
        }

        isOutlineVisible = true;

        if (useDuplicatedRenderers)
        {
            EnsureMaterialInstances();
            hullMaterialInstance.SetFloat("_OutlineWidth", outlineWidth);
            hullMaterialInstance.SetColor("_OutlineColor", color);
            CreateDuplicatedOutlineRenderers();
            return;
        }

        var mpb = new MaterialPropertyBlock();
        mpb.SetFloat("_OutlineWidth", outlineWidth);
        mpb.SetColor("_OutlineColor", color);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Material[] original = originalMaterials[i];
            Material[] expanded = new Material[original.Length + 1];
            original.CopyTo(expanded, 0);
            expanded[original.Length] = outlineMaterial;
            renderers[i].materials = expanded;
            renderers[i].SetPropertyBlock(mpb, original.Length);
        }
    }

    public void ShowPersistent(Color color)
    {
        isPersistent = true;
        ApplyOutline(color);
    }

    public void ClearPersistent()
    {
        isPersistent = false;
        Hide();
    }

    public void Hide()
    {
        if (isPersistent) return;
        if (!isOutlineVisible) return;
        isOutlineVisible = false;

        RemoveAppliedOutline();
    }

    private void RemoveAppliedOutline()
    {
        if (useDuplicatedRenderers)
        {
            DestroyDuplicatedOutlineRenderers();
        }
        else
        {
            RestoreOriginalMaterials();
        }
    }

    private void RestoreOriginalMaterials()
    {
        EnsureCache();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].materials = originalMaterials[i];
        }
    }

    private void EnsureCache()
    {
        if (renderers != null && originalMaterials != null && originalMaterials.Length == renderers.Length)
        {
            return;
        }

        Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
        var filteredRenderers = new System.Collections.Generic.List<Renderer>(childRenderers.Length);
        for (int rendererIndex = 0; rendererIndex < childRenderers.Length; rendererIndex++)
        {
            Renderer childRenderer = childRenderers[rendererIndex];
            if (childRenderer == null ||
                childRenderer.gameObject.name.EndsWith("_OutlineRenderer") ||
                childRenderer.gameObject.name.EndsWith("_OutlineMaskRenderer") ||
                IsExcluded(childRenderer.transform))
            {
                continue;
            }

            filteredRenderers.Add(childRenderer);
        }

        renderers = filteredRenderers.ToArray();
        CacheOriginalMaterials();
    }

    private bool IsExcluded(Transform target)
    {
        for (int rootIndex = 0; rootIndex < excludedRoots.Length; rootIndex++)
        {
            Transform excludedRoot = excludedRoots[rootIndex];
            if (excludedRoot != null && (target == excludedRoot || target.IsChildOf(excludedRoot)))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureMaterialInstances()
    {
        if (maskMaterialInstance != null && hullMaterialInstance != null)
        {
            return;
        }

        DestroyMaterialInstances();

        // Awake를 거치지 않고 사용돼도 참조값이 할당되도록 지연 할당한다.
        if (stencilRef == 0)
        {
            stencilRef = nextStencilRef;
            nextStencilRef = nextStencilRef % 255 + 1;
        }

        // 마스크: 색·깊이는 쓰지 않고 유닛 실루엣 픽셀에 스텐실 참조값만 기록한다.
        maskMaterialInstance = new Material(outlineMaterial);
        maskMaterialInstance.SetFloat("_OutlineWidth", 0f);
        maskMaterialInstance.SetFloat("_StencilRef", stencilRef);
        maskMaterialInstance.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.Always);
        maskMaterialInstance.SetFloat("_StencilOp", (float)UnityEngine.Rendering.StencilOp.Replace);
        maskMaterialInstance.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
        maskMaterialInstance.SetFloat("_ZWrite", 0f);
        maskMaterialInstance.SetFloat("_ColorMask", 0f);
        // 원본 메쉬와 동일 깊이에서 ZTest가 확실히 통과하도록 살짝 앞으로 당긴다.
        maskMaterialInstance.SetFloat("_OffsetFactor", -1f);
        maskMaterialInstance.SetFloat("_OffsetUnits", -1f);
        maskMaterialInstance.renderQueue = MaskRenderQueue;

        // 헐: 스텐실이 기록되지 않은 실루엣 바깥 픽셀에만 외곽선을 그린다.
        hullMaterialInstance = new Material(outlineMaterial);
        hullMaterialInstance.SetFloat("_StencilRef", stencilRef);
        hullMaterialInstance.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.NotEqual);
        hullMaterialInstance.SetFloat("_StencilOp", (float)UnityEngine.Rendering.StencilOp.Keep);
        hullMaterialInstance.renderQueue = HullRenderQueue;
    }

    private void DestroyMaterialInstances()
    {
        DestroyRuntimeObject(maskMaterialInstance);
        DestroyRuntimeObject(hullMaterialInstance);
        maskMaterialInstance = null;
        hullMaterialInstance = null;
    }

    private void CreateDuplicatedOutlineRenderers()
    {
        DestroyDuplicatedOutlineRenderers();

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer sourceRenderer = renderers[rendererIndex];
            if (sourceRenderer == null)
            {
                continue;
            }

            Renderer maskRenderer = CreateDuplicatedOutlineRenderer(sourceRenderer, maskMaterialInstance, "_OutlineMaskRenderer");
            if (maskRenderer != null)
            {
                duplicatedOutlineRenderers.Add(maskRenderer);
            }

            Renderer hullRenderer = CreateDuplicatedOutlineRenderer(sourceRenderer, hullMaterialInstance, "_OutlineRenderer");
            if (hullRenderer != null)
            {
                duplicatedOutlineRenderers.Add(hullRenderer);
            }
        }
    }

    private Renderer CreateDuplicatedOutlineRenderer(Renderer sourceRenderer, Material rendererMaterial, string nameSuffix)
    {
        GameObject outlineObject = new GameObject($"{sourceRenderer.gameObject.name}{nameSuffix}");
        outlineObject.transform.SetParent(sourceRenderer.transform, false);
        outlineObject.layer = sourceRenderer.gameObject.layer;

        Renderer outlineRenderer;
        int subMeshCount;

        if (sourceRenderer is SkinnedMeshRenderer sourceSkinnedRenderer)
        {
            if (sourceSkinnedRenderer.sharedMesh == null)
            {
                DestroyOutlineObject(outlineObject);
                return null;
            }

            SkinnedMeshRenderer outlineSkinnedRenderer = outlineObject.AddComponent<SkinnedMeshRenderer>();
            outlineSkinnedRenderer.sharedMesh = sourceSkinnedRenderer.sharedMesh;
            outlineSkinnedRenderer.bones = sourceSkinnedRenderer.bones;
            outlineSkinnedRenderer.rootBone = sourceSkinnedRenderer.rootBone;
            outlineSkinnedRenderer.localBounds = sourceSkinnedRenderer.localBounds;
            outlineSkinnedRenderer.updateWhenOffscreen = sourceSkinnedRenderer.updateWhenOffscreen;
            outlineSkinnedRenderer.quality = sourceSkinnedRenderer.quality;
            outlineRenderer = outlineSkinnedRenderer;
            subMeshCount = sourceSkinnedRenderer.sharedMesh.subMeshCount;
        }
        else if (sourceRenderer is MeshRenderer)
        {
            MeshFilter sourceMeshFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
            {
                DestroyOutlineObject(outlineObject);
                return null;
            }

            MeshFilter outlineMeshFilter = outlineObject.AddComponent<MeshFilter>();
            outlineMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
            outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
            subMeshCount = sourceMeshFilter.sharedMesh.subMeshCount;
        }
        else
        {
            DestroyOutlineObject(outlineObject);
            return null;
        }

        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        outlineRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        Material[] outlineMaterials = new Material[Mathf.Max(1, subMeshCount)];
        for (int materialIndex = 0; materialIndex < outlineMaterials.Length; materialIndex++)
        {
            outlineMaterials[materialIndex] = rendererMaterial;
        }

        outlineRenderer.sharedMaterials = outlineMaterials;
        return outlineRenderer;
    }

    private void DestroyDuplicatedOutlineRenderers()
    {
        for (int rendererIndex = 0; rendererIndex < duplicatedOutlineRenderers.Count; rendererIndex++)
        {
            Renderer outlineRenderer = duplicatedOutlineRenderers[rendererIndex];
            if (outlineRenderer != null)
            {
                DestroyOutlineObject(outlineRenderer.gameObject);
            }
        }

        duplicatedOutlineRenderers.Clear();
    }

    private static void DestroyOutlineObject(GameObject outlineObject)
    {
        if (outlineObject == null)
        {
            return;
        }

        DestroyRuntimeObject(outlineObject);
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void CacheOriginalMaterials()
    {
        originalMaterials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                originalMaterials[i] = System.Array.Empty<Material>();
                continue;
            }
            originalMaterials[i] = renderers[i].sharedMaterials;
        }
    }
}
