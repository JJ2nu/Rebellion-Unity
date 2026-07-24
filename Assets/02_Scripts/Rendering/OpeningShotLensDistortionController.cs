using UnityEngine;

/// <summary>
/// OpeningShot 전용 카메라가 사용할 렌즈 왜곡 값을 보관한다.
/// Renderer Feature가 현재 렌더링 중인 카메라에서 이 컴포넌트를 찾을 때만 효과를 적용한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class OpeningShotLensDistortionController : MonoBehaviour
{
    [Header("Lens Distortion")]
    [SerializeField] private bool effectEnabled = true;
    [SerializeField] private Vector2 center = new Vector2(0.5f, 0.5f);
    [SerializeField, Range(0f, 1f)] private float radius = 0.46f;
    [SerializeField, Range(0.001f, 0.5f)] private float edgeWidth = 0.16f;
    [SerializeField, Range(-0.25f, 0.25f)] private float strength = 0.05f;

    /// <summary>현재 카메라에서 렌즈 왜곡 Pass를 실행할지 결정한다.</summary>
    public bool EffectEnabled => effectEnabled;

    /// <summary>렌즈 중심의 정규화된 화면 좌표다. (0.5, 0.5)는 화면 중앙이다.</summary>
    public Vector2 Center => center;

    /// <summary>렌즈 가장자리로 취급할 정규화 반경이다.</summary>
    public float Radius => radius;

    /// <summary>왜곡이 반경 안쪽으로 퍼지는 폭이다.</summary>
    public float EdgeWidth => edgeWidth;

    /// <summary>가장자리 샘플 UV를 이동시킬 강도와 방향이다.</summary>
    public float Strength => strength;

    private void OnValidate()
    {
        // 잘못된 Inspector 값이 셰이더의 smoothstep 구간을 뒤집지 않도록 직렬화 시점에 보정한다.
        center.x = Mathf.Clamp01(center.x);
        center.y = Mathf.Clamp01(center.y);
        radius = Mathf.Clamp01(radius);
        edgeWidth = Mathf.Clamp(edgeWidth, 0.001f, 0.5f);
        strength = Mathf.Clamp(strength, -0.25f, 0.25f);
    }
}
