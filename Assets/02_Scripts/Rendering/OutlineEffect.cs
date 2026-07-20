using UnityEngine;

/// <summary>
/// 호버 시 외곽선을 표시한다.
/// 각 Renderer의 materials 배열 끝에 outlineMaterial을 추가하고, 해제 시 원래대로 복원한다.
/// </summary>
public class OutlineEffect : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;
    private const float outlineWidth = 0.03f;

    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private bool isOutlineVisible;
    private bool isPersistent;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        CacheOriginalMaterials();
    }

public void Show()
    {
        ShowWithColor(Color.white);
    }

    public void ShowWithColor(Color color)
    {
        if (outlineMaterial == null) return;

        if (isOutlineVisible)
        {
            RestoreOriginalMaterials();
        }

        isOutlineVisible = true;

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
        ShowWithColor(color);
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

        RestoreOriginalMaterials();
    }

    private void RestoreOriginalMaterials()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].materials = originalMaterials[i];
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
