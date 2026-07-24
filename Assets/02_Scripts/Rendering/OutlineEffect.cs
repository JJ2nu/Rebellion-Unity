using UnityEngine;

/// <summary>
/// 호버 시 외곽선을 표시한다.
/// 각 Renderer의 materials 배열 끝에 outlineMaterial을 추가하고, 해제 시 원래대로 복원한다.
/// </summary>
public class OutlineEffect : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;
    private const float DefaultOutlineWidth = 0.03f;

    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private Transform[] excludedRoots = System.Array.Empty<Transform>();
    private readonly System.Collections.Generic.List<Renderer> duplicatedOutlineRenderers = new();
    // 한 Renderer 안에 여러 서브메시가 있어도 전부 외곽선을 그리도록 기본적으로 복제 렌더러를 사용한다.
    private bool useDuplicatedRenderers = true;
    private float outlineWidth = DefaultOutlineWidth;
    private bool isOutlineVisible;
    private bool isPersistent;

    private void Awake()
    {
        EnsureCache();
    }

    public void SetOutlineMaterial(Material material)
    {
        outlineMaterial = material;
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

        var mpb = new MaterialPropertyBlock();
        mpb.SetFloat("_OutlineWidth", outlineWidth);
        mpb.SetColor("_OutlineColor", color);

        if (useDuplicatedRenderers)
        {
            CreateDuplicatedOutlineRenderers(mpb);
            return;
        }

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

    private void CreateDuplicatedOutlineRenderers(MaterialPropertyBlock propertyBlock)
    {
        DestroyDuplicatedOutlineRenderers();

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer sourceRenderer = renderers[rendererIndex];
            if (sourceRenderer == null)
            {
                continue;
            }

            Renderer outlineRenderer = CreateDuplicatedOutlineRenderer(sourceRenderer);
            if (outlineRenderer == null)
            {
                continue;
            }

            outlineRenderer.SetPropertyBlock(propertyBlock);
            duplicatedOutlineRenderers.Add(outlineRenderer);
        }
    }

    private Renderer CreateDuplicatedOutlineRenderer(Renderer sourceRenderer)
    {
        GameObject outlineObject = new GameObject($"{sourceRenderer.gameObject.name}_OutlineRenderer");
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
            outlineMaterials[materialIndex] = outlineMaterial;
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

        if (Application.isPlaying)
        {
            Destroy(outlineObject);
        }
        else
        {
            DestroyImmediate(outlineObject);
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
