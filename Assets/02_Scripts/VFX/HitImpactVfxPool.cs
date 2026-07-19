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
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.78f);
                switch (attackType)
                {
                    case HitImpactAttackType.Projectile:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(3.6f, 5.4f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 1.05f);
                        shape.angle = 6f;
                        SetBurstCount(particleSystem, 8);
                        break;
                    case HitImpactAttackType.Blunt:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.72f);
                        shape.angle = 22f;
                        SetBurstCount(particleSystem, 7);
                        break;
                    default:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.98f);
                        shape.angle = 12f;
                        SetBurstCount(particleSystem, 9);
                        break;
                }
            }
            else if (particleSystem.name == "Burst_MeshShards")
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.72f, 1.12f);
                switch (attackType)
                {
                    case HitImpactAttackType.Projectile:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 5.2f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 1.28f);
                        shape.angle = 24f;
                        SetBurstCount(particleSystem, 18);
                        break;
                    case HitImpactAttackType.Blunt:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.4f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 1.35f);
                        shape.angle = 58f;
                        SetBurstCount(particleSystem, 18);
                        break;
                    default:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.8f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.31f, 1.32f);
                        shape.angle = 42f;
                        SetBurstCount(particleSystem, 20);
                        break;
                }
            }
            else if (particleSystem.name == "Burst_DustMist")
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.62f, 0.95f);
                switch (attackType)
                {
                    case HitImpactAttackType.Projectile:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.55f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.46f);
                        shape.angle = 42f;
                        SetBurstCount(particleSystem, 28);
                        break;
                    case HitImpactAttackType.Blunt:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
                        shape.angle = 78f;
                        SetBurstCount(particleSystem, 30);
                        break;
                    default:
                        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.25f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.44f);
                        shape.angle = 65f;
                        SetBurstCount(particleSystem, 28);
                        break;
                }
            }
            else if (particleSystem.name == "Decal_SplashMark")
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.42f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.3f);
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
                // 빠른 궤적은 가장 잘 보이되 원색 빨강으로 번쩍이지 않게 짙은 적색을 사용한다.
                main.startColor = new Color(0.52f, 0.012f, 0.008f, 1f);
            }
            else if (particleSystem.name == "Burst_DustMist")
            {
                main.startColor = new Color(0.28f, 0.004f, 0.007f, 0.82f);
            }
            else if (particleSystem.name == "Decal_SplashMark")
            {
                main.startColor = new Color(0.22f, 0.003f, 0.008f, 0.96f);
            }
            else
            {
                // 메인 파편은 톤을 낮추고 알파를 유지해 어두운 배경에서도 형태가 남도록 한다.
                main.startColor = new Color(0.44f, 0.008f, 0.012f, 1f);
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
