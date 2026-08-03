using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 명시적으로 연결된 Overlay Canvas의 AspectContentRoot와 CanvasScaler를
/// 공용 16:9 viewport에 맞춰 갱신한다.
/// </summary>
[DefaultExecutionOrder(-9000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class FixedAspectRatioCanvas : MonoBehaviour
{
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);

    private RectTransform contentRoot;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private bool isApplying;

    private void Awake()
    {
        contentRoot = (RectTransform)transform;
        ApplyLayout();
    }

    private void OnEnable()
    {
        ApplyLayout();
    }

    private void Update()
    {
        if (lastScreenWidth == Screen.width && lastScreenHeight == Screen.height)
        {
            return;
        }

        ApplyLayout();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (Application.isPlaying)
        {
            ApplyLayout();
        }
    }

    private void Reset()
    {
        canvasScaler = GetComponentInParent<CanvasScaler>();
    }

    private void OnValidate()
    {
        referenceResolution.x = Mathf.Max(1f, referenceResolution.x);
        referenceResolution.y = Mathf.Max(1f, referenceResolution.y);
    }

    private void ApplyLayout()
    {
        if (isApplying)
        {
            return;
        }

        if (contentRoot == null)
        {
            contentRoot = transform as RectTransform;
        }

        if (contentRoot == null || canvasScaler == null)
        {
            Debug.LogError(
                "FixedAspectRatioCanvas requires an AspectContentRoot RectTransform and an explicitly assigned CanvasScaler.",
                this);
            enabled = false;
            return;
        }

        isApplying = true;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        Rect viewportRect =
            FixedAspectRatioController.GetViewportRect(lastScreenWidth, lastScreenHeight);

        // 넓은 화면은 높이, 좁은 화면은 너비를 기준으로 배율을 잡으면
        // AspectContentRoot의 논리 크기가 항상 referenceResolution과 일치한다.
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = referenceResolution;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = viewportRect.width < 0.9999f
            ? 1f
            : viewportRect.height < 0.9999f
                ? 0f
                : 0.5f;

        contentRoot.anchorMin = viewportRect.min;
        contentRoot.anchorMax = viewportRect.max;
        contentRoot.offsetMin = Vector2.zero;
        contentRoot.offsetMax = Vector2.zero;
        isApplying = false;
    }
}
