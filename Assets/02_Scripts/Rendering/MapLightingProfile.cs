using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 활성화된 맵의 시간대에 맞춰 Stage 공통 조명과 후처리 값을 전환한다.
/// 맵 프리팹에 하나씩 배치하며, 로컬 Point Light는 각 프리팹이 별도로 소유한다.
/// </summary>
public sealed class MapLightingProfile : MonoBehaviour
{
    private const string KeyLightName = "Lighting_Key_Warm";
    private const string FillLightName = "Lighting_Fill_Cool";
    private const string GlobalVolumeName = "Global Volume";

    [Header("Environment")]
    [SerializeField] private Color ambientSkyColor = new(0.22f, 0.25f, 0.32f, 1f);
    [SerializeField] private Color ambientEquatorColor = new(0.12f, 0.14f, 0.18f, 1f);
    [SerializeField] private Color ambientGroundColor = new(0.05f, 0.05f, 0.065f, 1f);
    [SerializeField, Min(0f)] private float ambientIntensity = 0.7f;
    [SerializeField, Min(0f)] private float reflectionIntensity = 0.8f;

    [Header("Key Light")]
    [SerializeField] private Color keyColor = new(1f, 0.92f, 0.82f, 1f);
    [SerializeField, Min(0f)] private float keyIntensity = 0.72f;
    [SerializeField] private Vector3 keyEulerAngles = new(48f, 325f, 0f);
    [SerializeField, Range(0f, 1f)] private float keyShadowStrength = 0.68f;

    [Header("Fill Light")]
    [SerializeField] private Color fillColor = new(0.5f, 0.62f, 0.82f, 1f);
    [SerializeField, Min(0f)] private float fillIntensity = 0.28f;
    [SerializeField] private Vector3 fillEulerAngles = new(32f, 145f, 0f);

    [Header("Color Adjustments")]
    [SerializeField] private float postExposure = 0.1f;
    [SerializeField, Range(-100f, 100f)] private float contrast = 8f;
    [SerializeField, Range(-100f, 100f)] private float saturation = -3f;
    [SerializeField] private Color colorFilter = Color.white;

    private void OnEnable()
    {
        Apply();
    }

    [ContextMenu("Apply Lighting Profile")]
    public void Apply()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSkyColor;
        RenderSettings.ambientEquatorColor = ambientEquatorColor;
        RenderSettings.ambientGroundColor = ambientGroundColor;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.reflectionIntensity = reflectionIntensity;

        ApplyDirectionalLight(KeyLightName, keyColor, keyIntensity, keyEulerAngles, keyShadowStrength);
        ApplyDirectionalLight(FillLightName, fillColor, fillIntensity, fillEulerAngles, 0f);
        ApplyColorAdjustments();
    }

    private static void ApplyDirectionalLight(
        string lightName,
        Color color,
        float intensity,
        Vector3 eulerAngles,
        float shadowStrength)
    {
        GameObject lightObject = GameObject.Find(lightName);
        Light targetLight = lightObject != null ? lightObject.GetComponent<Light>() : null;
        if (targetLight == null)
        {
            return;
        }

        targetLight.color = color;
        targetLight.intensity = intensity;
        targetLight.shadowStrength = shadowStrength;
        targetLight.transform.rotation = Quaternion.Euler(eulerAngles);
    }

    private void ApplyColorAdjustments()
    {
        GameObject volumeObject = GameObject.Find(GlobalVolumeName);
        Volume volume = volumeObject != null ? volumeObject.GetComponent<Volume>() : null;
        if (volume == null || volume.profile == null ||
            !volume.profile.TryGet(out ColorAdjustments adjustments))
        {
            return;
        }

        adjustments.postExposure.Override(postExposure);
        adjustments.contrast.Override(contrast);
        adjustments.saturation.Override(saturation);
        adjustments.colorFilter.Override(colorFilter);
    }
}
