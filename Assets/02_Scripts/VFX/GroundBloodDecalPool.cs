using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class GroundBloodDecalPool : MonoBehaviour
{
    private const string MaterialResourcePath = "VFX/MAT_GroundBloodDecal";
    private const int MaxDecalCount = 48;
    private const float RaycastHeight = 1.5f;
    private const float RaycastDistance = 4f;
    private const float SurfaceOffset = 0.012f;

    private readonly Queue<Renderer> available = new();
    private readonly Queue<Renderer> active = new();
    private Material decalMaterial;

    private void Awake()
    {
        decalMaterial = Resources.Load<Material>(MaterialResourcePath);
        if (decalMaterial == null)
        {
            Debug.LogWarning($"[GroundBloodDecalPool] Material not found at Resources/{MaterialResourcePath}.", this);
        }
    }

    public void Play(Vector3 hitPosition)
    {
        if (decalMaterial == null)
        {
            return;
        }

        int satelliteCount = Random.Range(3, 7);
        SpawnDecal(hitPosition, true);

        for (int index = 0; index < satelliteCount; index++)
        {
            Vector2 offset = Random.insideUnitCircle * Random.Range(0.18f, 0.7f);
            SpawnDecal(hitPosition + new Vector3(offset.x, 0f, offset.y), false);
        }
    }

    private void SpawnDecal(Vector3 hitPosition, bool isPrimary)
    {
        if (!TryFindGround(hitPosition, out RaycastHit hit))
        {
            return;
        }

        Renderer decal = GetOrCreate();
        if (decal == null)
        {
            return;
        }

        Transform decalTransform = decal.transform;
        decalTransform.SetPositionAndRotation(
            hit.point + hit.normal * (SurfaceOffset + Random.Range(0f, 0.006f)),
            Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));

        float width = isPrimary ? Random.Range(1.05f, 1.55f) : Random.Range(0.3f, 0.78f);
        float height = width * Random.Range(0.72f, 1.18f);
        decalTransform.localScale = new Vector3(width, height, 1f);
        decal.gameObject.SetActive(true);
        active.Enqueue(decal);

        Color color = isPrimary
            ? new Color(Random.Range(0.2f, 0.28f), 0.002f, 0.006f, Random.Range(0.86f, 0.96f))
            : new Color(Random.Range(0.16f, 0.24f), 0.001f, 0.004f, Random.Range(0.72f, 0.9f));

        StartCoroutine(FadeAndRelease(decal, Random.Range(4f, 6f), color));
    }

    public void Clear()
    {
        StopAllCoroutines();
        while (active.Count > 0)
        {
            Release(active.Dequeue());
        }
    }

    private Renderer GetOrCreate()
    {
        while (available.Count > 0)
        {
            Renderer pooled = available.Dequeue();
            if (pooled != null)
            {
                return pooled;
            }
        }

        if (active.Count >= MaxDecalCount)
        {
            return null;
        }

        GameObject decalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        decalObject.name = "GroundBloodDecal";
        decalObject.transform.SetParent(transform, false);

        Collider collider = decalObject.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = decalObject.GetComponent<Renderer>();
        renderer.sharedMaterial = decalMaterial;
        decalObject.SetActive(false);
        return renderer;
    }

    private IEnumerator FadeAndRelease(Renderer decal, float holdDuration, Color baseColor)
    {
        MaterialPropertyBlock properties = new();
        ApplyColor(decal, properties, baseColor);

        yield return new WaitForSeconds(holdDuration);

        const float FadeDuration = 1.25f;
        float elapsed = 0f;
        while (elapsed < FadeDuration && decal != null && decal.gameObject.activeSelf)
        {
            elapsed += Time.deltaTime;
            Color color = baseColor;
            color.a *= 1f - Mathf.Clamp01(elapsed / FadeDuration);
            ApplyColor(decal, properties, color);
            yield return null;
        }

        if (decal != null)
        {
            RemoveFromActive(decal);
            Release(decal);
        }
    }

    private static bool TryFindGround(Vector3 hitPosition, out RaycastHit groundHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            hitPosition + Vector3.up * RaycastHeight,
            Vector3.down,
            RaycastDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.normal.y < 0.5f || hit.collider.GetComponentInParent<PieceBase>() != null)
            {
                continue;
            }

            groundHit = hit;
            return true;
        }

        groundHit = default;
        return false;
    }

    private static void ApplyColor(Renderer renderer, MaterialPropertyBlock properties, Color color)
    {
        renderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
    }

    private void Release(Renderer decal)
    {
        if (decal == null)
        {
            return;
        }

        decal.gameObject.SetActive(false);
        available.Enqueue(decal);
    }

    private void RemoveFromActive(Renderer decal)
    {
        int count = active.Count;
        for (int index = 0; index < count; index++)
        {
            Renderer current = active.Dequeue();
            if (current != decal)
            {
                active.Enqueue(current);
            }
        }
    }
}
