using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum HitImpactColorMode
{
    Red,
    White
}

public enum HitImpactAttackType
{
    Slash,
    Blunt,
    Projectile
}

public class HitImpactVfxPool : MonoBehaviour
{
    [SerializeField] private GameObject redImpactPrefab;
    [SerializeField] private GameObject whiteImpactPrefab;
    [SerializeField, Min(0)] private int maxExtraInstances = 4;

    private readonly Queue<GameObject> redPool = new();
    private readonly Queue<GameObject> whitePool = new();
    private int redCreatedCount;
    private int whiteCreatedCount;

    public void Configure(GameObject redPrefab, GameObject whitePrefab, int poolSize, HitImpactColorMode colorMode)
    {
        redImpactPrefab = redPrefab;
        whiteImpactPrefab = whitePrefab;
        Prewarm(poolSize, colorMode);
    }

    public void Prewarm(int poolSize, HitImpactColorMode colorMode)
    {
        int targetSize = Mathf.Max(0, poolSize);
        if (colorMode == HitImpactColorMode.White)
        {
            PrewarmPool(whiteImpactPrefab, whitePool, ref whiteCreatedCount, targetSize);
        }
        else
        {
            PrewarmPool(redImpactPrefab, redPool, ref redCreatedCount, targetSize);
        }
    }

    public void Play(Vector3 position, Vector3 direction, HitImpactColorMode colorMode, HitImpactAttackType attackType)
    {
        GameObject instance = Get(colorMode);
        if (instance == null)
        {
            return;
        }

        Quaternion rotation = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : Quaternion.identity;

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        ApplyAttackType(instance, attackType);
        ApplyColorMode(instance, colorMode);

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        float releaseDelay = 0f;
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(true);

            ParticleSystem.MainModule main = particleSystem.main;
            releaseDelay = Mathf.Max(releaseDelay, main.duration + main.startLifetime.constantMax);
        }

        StartCoroutine(ReleaseAfter(instance, colorMode, Mathf.Max(0.2f, releaseDelay)));
    }

    public void Clear()
    {
        ClearPool(redPool);
        ClearPool(whitePool);
        redCreatedCount = 0;
        whiteCreatedCount = 0;
    }

    private void PrewarmPool(GameObject prefab, Queue<GameObject> pool, ref int createdCount, int targetSize)
    {
        if (prefab == null)
        {
            return;
        }

        while (createdCount < targetSize)
        {
            pool.Enqueue(CreateInstance(prefab, ref createdCount));
        }
    }

    private GameObject Get(HitImpactColorMode colorMode)
    {
        Queue<GameObject> pool = colorMode == HitImpactColorMode.White ? whitePool : redPool;
        GameObject prefab = colorMode == HitImpactColorMode.White ? whiteImpactPrefab : redImpactPrefab;
        int createdCount = colorMode == HitImpactColorMode.White ? whiteCreatedCount : redCreatedCount;

        while (pool.Count > 0)
        {
            GameObject pooled = pool.Dequeue();
            if (pooled != null)
            {
                return pooled;
            }
        }

        int maxCount = Mathf.Max(0, StageManager.Instance != null ? StageManager.Instance.CurrentSpawnedPieceCount : 0) + maxExtraInstances;
        if (createdCount >= maxCount && maxCount > 0)
        {
            return null;
        }

        if (prefab == null)
        {
            return null;
        }

        if (colorMode == HitImpactColorMode.White)
        {
            return CreateInstance(prefab, ref whiteCreatedCount);
        }

        return CreateInstance(prefab, ref redCreatedCount);
    }

    private GameObject CreateInstance(GameObject prefab, ref int createdCount)
    {
        GameObject instance = Instantiate(prefab, transform);
        instance.SetActive(false);
        createdCount++;
        return instance;
    }

    private IEnumerator ReleaseAfter(GameObject instance, HitImpactColorMode colorMode, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (instance == null)
        {
            yield break;
        }

        instance.SetActive(false);
        if (colorMode == HitImpactColorMode.White)
        {
            whitePool.Enqueue(instance);
        }
        else
        {
            redPool.Enqueue(instance);
        }
    }

    private static void ApplyAttackType(GameObject instance, HitImpactAttackType attackType)
    {
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            ParticleSystem.ShapeModule shape = particleSystem.shape;

            if (particleSystem.name == "Burst_Streaks")
            {
                switch (attackType)
                {
                    case HitImpactAttackType.Projectile:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(3.6f, 5.4f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.48f);
                        shape.angle = 6f;
                        SetBurstCount(particleSystem, 3);
                        break;
                    case HitImpactAttackType.Blunt:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
                        shape.angle = 22f;
                        SetBurstCount(particleSystem, 2);
                        break;
                    default:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.42f);
                        shape.angle = 12f;
                        SetBurstCount(particleSystem, 4);
                        break;
                }
            }
            else if (particleSystem.name == "Burst_MeshShards")
            {
                switch (attackType)
                {
                    case HitImpactAttackType.Projectile:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 5.2f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.65f);
                        shape.angle = 24f;
                        SetBurstCount(particleSystem, 18);
                        break;
                    case HitImpactAttackType.Blunt:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.4f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.7f);
                        shape.angle = 58f;
                        SetBurstCount(particleSystem, 18);
                        break;
                    default:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.8f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.68f);
                        shape.angle = 42f;
                        SetBurstCount(particleSystem, 20);
                        break;
                }
            }
            else if (particleSystem.name == "Burst_DustMist")
            {
                switch (attackType)
                {
                    case HitImpactAttackType.Projectile:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.55f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.18f);
                        shape.angle = 42f;
                        SetBurstCount(particleSystem, 28);
                        break;
                    case HitImpactAttackType.Blunt:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.16f);
                        shape.angle = 78f;
                        SetBurstCount(particleSystem, 30);
                        break;
                    default:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.25f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.17f);
                        shape.angle = 65f;
                        SetBurstCount(particleSystem, 28);
                        break;
                }
            }
            else if (particleSystem.name == "Decal_SplashMark")
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.22f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.72f, 1.18f);
                shape.angle = 0f;
                SetBurstCount(particleSystem, 1);

                ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                }
            }
        }
    }

    private static void ApplyColorMode(GameObject instance, HitImpactColorMode colorMode)
    {
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            if (colorMode == HitImpactColorMode.White)
            {
                if (particleSystem.name == "Burst_Streaks")
                {
                    main.startColor = new Color(0.72f, 0.88f, 1f, 0.9f);
                }
                else if (particleSystem.name == "Burst_DustMist")
                {
                    main.startColor = new Color(0.85f, 0.88f, 0.92f, 0.38f);
                }
                else if (particleSystem.name == "Decal_SplashMark")
                {
                    main.startColor = new Color(0.92f, 0.96f, 1f, 0.62f);
                }
                else
                {
                    main.startColor = new Color(0.9f, 0.95f, 1f, 0.95f);
                }

                continue;
            }

            if (particleSystem.name == "Burst_Streaks")
            {
                main.startColor = new Color(0.56f, 0.012f, 0.01f, 0.72f);
            }
            else if (particleSystem.name == "Burst_DustMist")
            {
                main.startColor = new Color(0.16f, 0f, 0.005f, 0.45f);
            }
            else if (particleSystem.name == "Decal_SplashMark")
            {
                main.startColor = new Color(0.32f, 0f, 0.01f, 0.72f);
            }
            else
            {
                main.startColor = new Color(0.38f, 0.01f, 0.015f, 1f);
            }
        }
    }

    private static void SetBurstCount(ParticleSystem particleSystem, short count)
    {
        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
    }

    private static void ClearPool(Queue<GameObject> pool)
    {
        while (pool.Count > 0)
        {
            GameObject pooled = pool.Dequeue();
            if (pooled != null)
            {
                Destroy(pooled);
            }
        }
    }
}
