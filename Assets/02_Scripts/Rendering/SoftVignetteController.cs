using UnityEngine;

/// <summary>
/// URP Full Screen Pass가 사용하는 비네트 머티리얼 값을 카메라별 연출 값으로 제어한다.
/// Stage Camera에 연결해 Inspector 조정과 게임 흐름 코드의 런타임 변경을 같은 경로로 처리한다.
/// </summary>
[DisallowMultipleComponent]
public class SoftVignetteController : MonoBehaviour
{
    private static readonly int OpacityProperty = Shader.PropertyToID("_SoftVignetteOpacity");
    private static readonly int RoundnessProperty = Shader.PropertyToID("_SoftVignetteRoundness");
    private static readonly int FeatherProperty = Shader.PropertyToID("_SoftVignetteFeather");
    private static readonly int SizeProperty = Shader.PropertyToID("_SoftVignetteSize");
    private static readonly int CenterProperty = Shader.PropertyToID("_SoftVignetteCenter");
    private static readonly int AspectRatioProperty = Shader.PropertyToID("_SoftVignetteAspectRatio");
    private static readonly int ColorProperty = Shader.PropertyToID("_SoftVignetteColor");

    [Header("Material")]
    [SerializeField] private Material vignetteMaterial;

    [Header("Vignette")]
    [SerializeField] private bool effectEnabled = true;
    [SerializeField, Range(0f, 1f)] private float opacity = 0.65f;
    [SerializeField, Range(0f, 1f)] private float roundness = 0.75f;
    [SerializeField, Range(0.001f, 2f)] private float feather = 1.1f;
    [SerializeField, Range(0.1f, 2f)] private float size = 1.35f;
    [SerializeField] private Vector2 center = new Vector2(0.5f, 0.5f);
    [SerializeField, Range(0.25f, 4f)] private float aspectRatio = 1f;
    [SerializeField] private Color color = Color.black;

    /// <summary>현재 이 카메라가 사용하는 비네트 효과의 표시 상태다.</summary>
    public bool EffectEnabled => effectEnabled;

    private void OnEnable()
    {
        // 씬 전환으로 카메라가 다시 활성화될 때 현재 Inspector 값을 머티리얼에 재적용한다.
        Apply();
    }

    private void OnValidate()
    {
        opacity = Mathf.Clamp01(opacity);
        roundness = Mathf.Clamp01(roundness);
        feather = Mathf.Clamp(feather, 0.001f, 2f);
        size = Mathf.Clamp(size, 0.1f, 2f);
        aspectRatio = Mathf.Clamp(aspectRatio, 0.25f, 4f);

        Apply();
    }

    /// <summary>게임 흐름에서 비네트를 즉시 켜거나 끈다.</summary>
    public void SetEffectEnabled(bool isEnabled)
    {
        effectEnabled = isEnabled;
        Apply();
    }

    /// <summary>가장자리 어두움의 강도를 0~1 범위로 변경한다.</summary>
    public void SetOpacity(float value)
    {
        opacity = Mathf.Clamp01(value);
        Apply();
    }

    /// <summary>0은 사각형에 가깝고 1은 원형/타원형에 가까운 외곽선을 만든다.</summary>
    public void SetRoundness(float value)
    {
        roundness = Mathf.Clamp01(value);
        Apply();
    }

    /// <summary>밝은 중심에서 어두운 가장자리로 넘어가는 폭을 변경한다.</summary>
    public void SetFeather(float value)
    {
        feather = Mathf.Clamp(value, 0.001f, 2f);
        Apply();
    }

    /// <summary>밝은 영역의 크기를 변경한다. 작을수록 화면 가장자리 어두움이 안쪽으로 들어온다.</summary>
    public void SetSize(float value)
    {
        size = Mathf.Clamp(value, 0.1f, 2f);
        Apply();
    }

    /// <summary>밝은 중심의 화면 좌표를 변경한다. (0.5, 0.5)는 화면 정중앙이다.</summary>
    public void SetCenter(Vector2 value)
    {
        center = value;
        Apply();
    }

    /// <summary>가로축 비율을 조절해 원형과 가로로 긴 타원형을 전환한다.</summary>
    public void SetAspectRatio(float value)
    {
        aspectRatio = Mathf.Clamp(value, 0.25f, 4f);
        Apply();
    }

    /// <summary>가장자리 감쇠에 사용할 색을 변경한다.</summary>
    public void SetColor(Color value)
    {
        color = value;
        Apply();
    }

    /// <summary>게임 흐름에서 비네트 모양 전체를 한 번에 변경할 때 사용한다.</summary>
    public void SetShape(float newOpacity, float newRoundness, float newFeather, float newSize, Vector2 newCenter)
    {
        opacity = Mathf.Clamp01(newOpacity);
        roundness = Mathf.Clamp01(newRoundness);
        feather = Mathf.Clamp(newFeather, 0.001f, 2f);
        size = Mathf.Clamp(newSize, 0.1f, 2f);
        center = newCenter;
        Apply();
    }

    private void Apply()
    {
        if (vignetteMaterial == null)
        {
            return;
        }

        // Renderer Feature가 참조하는 공용 머티리얼을 갱신해 해당 프레임의 Full Screen Pass에 바로 반영한다.
        vignetteMaterial.SetFloat(OpacityProperty, effectEnabled ? opacity : 0f);
        vignetteMaterial.SetFloat(RoundnessProperty, roundness);
        vignetteMaterial.SetFloat(FeatherProperty, feather);
        vignetteMaterial.SetFloat(SizeProperty, size);
        vignetteMaterial.SetVector(CenterProperty, center);
        vignetteMaterial.SetFloat(AspectRatioProperty, aspectRatio);
        vignetteMaterial.SetColor(ColorProperty, color);
    }
}
