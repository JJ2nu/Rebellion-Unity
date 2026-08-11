using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼 가이드 기물의 자리 표시자 연출을 담당한다.
/// 반투명 몸체의 알파를 주기적으로 오르내리게 하고, 머리 위 가이드 표식을 같은 주기로 위아래로 띄운다.
/// 가이드 Prefab(TutorialGuide_*) 루트에 붙이며, 가이드 오브젝트가 비활성화되면 연출도 함께 멈춘다.
/// </summary>
public sealed class TutorialGuidePresentation : MonoBehaviour
{
    // 가이드 전용 반투명 셰이더를 쓰는 머티리얼만 펄스 대상으로 골라내기 위한 이름이다.
    private const string GuideShaderName = "Custom/TutorialGuideTransparent";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Header("Body Alpha Pulse")]
    // 알파가 최소~최대를 한 번 왕복하는 시간(초)이다.
    [SerializeField, Min(0.1f)] private float pulseCycleSeconds = 1.8f;
    // 머티리얼 원본 알파에 곱할 최소/최대 배율이다.
    [SerializeField, Range(0f, 1f)] private float minAlphaScale = 0.4f;
    [SerializeField, Range(0f, 2f)] private float maxAlphaScale = 1.2f;

    [Header("Guide Marker")]
    // Inspector 연결 지점: HUD 아래 가이드 표식 Transform. 비어 있으면 표식 연출은 생략한다.
    [SerializeField] private Transform guideMarker;
    // 표식이 위아래로 움직이는 거리(HUD 로컬 기준)다.
    [SerializeField, Min(0f)] private float markerBobDistance = 0.12f;

    private readonly List<Material> pulseMaterials = new();
    private readonly List<Color> pulseBaseColors = new();
    private Vector3 markerBasePosition;
    private bool hasMarkerBasePosition;

    private void Awake()
    {
        CachePulseMaterials();

        if (guideMarker != null)
        {
            markerBasePosition = guideMarker.localPosition;
            hasMarkerBasePosition = true;
        }
    }

    private void Update()
    {
        // sin 곡선 하나로 몸체 알파와 표식 위치를 함께 움직여 펄스 주기를 일치시킨다.
        // Storage 슬롯 하이라이트도 같은 Time.time 기반 곡선을 쓰므로 화면 전체의 안내 펄스가 비슷한 박자로 보인다.
        float wave = (Mathf.Sin(Time.time * (Mathf.PI * 2f) / pulseCycleSeconds) + 1f) * 0.5f;

        float alphaScale = Mathf.Lerp(minAlphaScale, maxAlphaScale, wave);
        for (int index = 0; index < pulseMaterials.Count; index++)
        {
            Color baseColor = pulseBaseColors[index];
            baseColor.a = Mathf.Clamp01(baseColor.a * alphaScale);
            pulseMaterials[index].SetColor(BaseColorId, baseColor);
        }

        if (guideMarker != null && hasMarkerBasePosition)
        {
            guideMarker.localPosition = markerBasePosition + Vector3.up * (markerBobDistance * wave);
        }
    }

    private void OnDestroy()
    {
        // renderer.materials로 만든 인스턴스는 Unity가 자동 파괴하지 않으므로 직접 정리해 누수를 막는다.
        for (int index = 0; index < pulseMaterials.Count; index++)
        {
            if (pulseMaterials[index] != null)
            {
                Destroy(pulseMaterials[index]);
            }
        }

        pulseMaterials.Clear();
        pulseBaseColors.Clear();
    }

    private void CachePulseMaterials()
    {
        // 공유 머티리얼 asset을 바꾸면 다른 가이드 인스턴스와 asset 원본까지 함께 바뀌므로,
        // 가이드 셰이더를 쓰는 renderer에만 인스턴스 머티리얼을 만들어 펄스에 사용한다.
        foreach (Renderer targetRenderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!HasGuideMaterial(targetRenderer.sharedMaterials))
            {
                continue;
            }

            Material[] instancedMaterials = targetRenderer.materials;
            for (int index = 0; index < instancedMaterials.Length; index++)
            {
                Material material = instancedMaterials[index];
                if (material != null &&
                    material.shader != null &&
                    material.shader.name == GuideShaderName &&
                    material.HasProperty(BaseColorId))
                {
                    pulseMaterials.Add(material);
                    pulseBaseColors.Add(material.GetColor(BaseColorId));
                }
            }
        }
    }

    private static bool HasGuideMaterial(Material[] sharedMaterials)
    {
        for (int index = 0; index < sharedMaterials.Length; index++)
        {
            Material material = sharedMaterials[index];
            if (material != null && material.shader != null && material.shader.name == GuideShaderName)
            {
                return true;
            }
        }

        return false;
    }
}
