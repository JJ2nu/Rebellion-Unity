using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Opening Shot 연출의 카메라, 화면 오버레이, UI 표시와 SFX만 적용하는 Passive View다.
/// 타겟 사망, 스킬 실행 기록과 Simulation 흐름은 알지 않는다.
/// </summary>
public sealed class OpeningShotPresentationView : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Camera cinematicCamera;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private CanvasGroup gameplayUiCanvasGroup;

    [Header("Presentation Overlay")]
    [SerializeField] private Image scopeOverlay;
    [SerializeField, Range(0.1f, 1f)] private float scopeViewportSize = 0.78f;
    [SerializeField, Range(0.01f, 1f)] private float scopeGraphicDiameterRatio = 0.2283f;

    [Header("Audio")]
    [SerializeField] private AudioClip shotSfx;

    [Header("Input")]
    [SerializeField] private InputActionReference cancelAction;

    private bool isPresenting;
    private bool cancelActionEnabledByView;
    private bool initialCameraEnabled;
    private float initialGameplayUiAlpha;
    private bool initialGameplayUiInteractable;
    private bool initialGameplayUiBlocksRaycasts;
    private Vector3 initialCameraPosition;
    private Quaternion initialCameraRotation;
    private float initialCameraFieldOfView;
    private Vector3 targetCameraPosition;
    private Quaternion targetCameraRotation;
    private readonly List<VisibilityState> hiddenWorldIndicators = new();

    public event Action SkipRequested;

    public bool IsPresenting => isPresenting;
    public bool IsCinematicCameraActive =>
        cinematicCamera != null &&
        cinematicCamera.gameObject.activeInHierarchy &&
        cinematicCamera.enabled;

    public void RequestSkip()
    {
        if (isPresenting)
        {
            SkipRequested?.Invoke();
        }
    }

    private void OnEnable()
    {
        SubscribeCancelAction();
    }

    private void OnDisable()
    {
        UnsubscribeCancelAction();

        if (isPresenting)
        {
            RestoreVisualState();
        }
    }

    /// <summary>
    /// 연출 전 UI와 카메라 상태를 보관하고 첫 프레임 표시 상태를 준비한다.
    /// </summary>
    public bool BeginPresentation(IReadOnlyList<PieceBase> pieces)
    {
        if (cinematicCamera == null ||
            gameplayCamera == null ||
            gameplayUiCanvasGroup == null ||
            scopeOverlay == null)
        {
            Debug.LogWarning("Opening Shot Presentation View references are incomplete.", this);
            return false;
        }

        SyncCinematicCameraToGameplayCamera();
        CaptureInitialState();
        FitScopeToViewport();

        isPresenting = true;
        HideWorldIndicators(pieces);
        gameplayUiCanvasGroup.interactable = false;
        gameplayUiCanvasGroup.blocksRaycasts = false;
        cinematicCamera.enabled = false;
        SetEntryProgress(0f);

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        SubscribeCancelAction();
        return true;
    }

    public void SetEntryProgress(float progress)
    {
        float normalized = Mathf.Clamp01(progress);
        SetGameplayUiAlpha(Mathf.Lerp(initialGameplayUiAlpha, 0f, normalized));
        SetScopeAlpha(normalized);
    }

    public void ActivateCinematicCamera()
    {
        if (cinematicCamera != null)
        {
            cinematicCamera.enabled = true;
        }
    }

    /// <summary>
    /// 현재 Stage Camera FOV를 유지하면서 머리 쪽을 중앙에 두고 전신이 원형 스코프 안에 들어오는 카메라 종점 구도를 계산한다.
    /// </summary>
    public bool PrepareTargetFraming(
        Transform target,
        float headAimHeight,
        float framingPadding,
        float cinematicVisibleHeight,
        float minimumCameraDistance)
    {
        if (cinematicCamera == null || target == null)
        {
            return false;
        }

        Bounds bounds = CalculateTargetBounds(target);
        float clampedHeadHeight = Mathf.Clamp01(headAimHeight);
        Vector3 aimPoint = bounds.center +
            Vector3.up * bounds.extents.y * clampedHeadHeight;

        Vector3 viewDirection = aimPoint - initialCameraPosition;
        if (viewDirection.sqrMagnitude < 0.0001f)
        {
            viewDirection = cinematicCamera.transform.forward;
        }
        viewDirection.Normalize();

        targetCameraRotation = Quaternion.LookRotation(viewDirection, Vector3.up);

        Vector3[] corners = BuildBoundsCorners(bounds);
        float maximumHorizontalExtent = 0f;
        float maximumVerticalExtent = 0f;
        Quaternion inverseRotation = Quaternion.Inverse(targetCameraRotation);

        for (int index = 0; index < corners.Length; index++)
        {
            Vector3 localOffset = inverseRotation * (corners[index] - aimPoint);
            maximumHorizontalExtent = Mathf.Max(maximumHorizontalExtent, Mathf.Abs(localOffset.x));
            maximumVerticalExtent = Mathf.Max(maximumVerticalExtent, Mathf.Abs(localOffset.y));
        }

        float verticalTangent = Mathf.Tan(initialCameraFieldOfView * 0.5f * Mathf.Deg2Rad);
        float visibleHeight = Mathf.Clamp(cinematicVisibleHeight, 0.1f, 1f);
        float aspect = Mathf.Max(0.1f, cinematicCamera.aspect);
        float padding = Mathf.Max(1f, framingPadding);
        float verticalDistance = maximumVerticalExtent * padding / (verticalTangent * visibleHeight);
        float horizontalDistance = maximumHorizontalExtent * padding / (verticalTangent * aspect);
        float distance = Mathf.Max(minimumCameraDistance, verticalDistance, horizontalDistance);

        targetCameraPosition = aimPoint - viewDirection * distance;
        return true;
    }

    public void SetZoomProgress(float progress)
    {
        if (cinematicCamera == null)
        {
            return;
        }

        float normalized = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        cinematicCamera.transform.position =
            Vector3.Lerp(initialCameraPosition, targetCameraPosition, normalized);
        cinematicCamera.transform.rotation =
            Quaternion.Slerp(initialCameraRotation, targetCameraRotation, normalized);
        cinematicCamera.fieldOfView = initialCameraFieldOfView;
    }

    public void PlayShotSfx()
    {
        PlayDetachedSfx(shotSfx);
    }

    /// <summary>
    /// 총격 종점 구도를 기준으로 반동과 잔진동을 적용한다.
    /// </summary>
    public void SetShotReaction(
        float progress,
        float positionStrength,
        float rotationStrength,
        float frequency)
    {
        if (cinematicCamera == null)
        {
            return;
        }

        float normalized = Mathf.Clamp01(progress);
        float envelope = Mathf.Sin(normalized * Mathf.PI);
        float oscillation = normalized * Mathf.Max(1f, frequency) * Mathf.PI * 2f;
        float horizontalNoise = Mathf.Sin(oscillation);
        float verticalNoise = Mathf.Cos(oscillation * 0.83f);

        Vector3 localOffset = new(
            horizontalNoise * positionStrength * envelope,
            verticalNoise * positionStrength * envelope,
            -positionStrength * 0.35f * envelope);
        Vector3 rotationOffset = new(
            -rotationStrength * envelope + verticalNoise * rotationStrength * 0.25f * envelope,
            horizontalNoise * rotationStrength * 0.35f * envelope,
            horizontalNoise * rotationStrength * 0.15f * envelope);

        cinematicCamera.transform.position =
            targetCameraPosition + targetCameraRotation * localOffset;
        cinematicCamera.transform.rotation =
            targetCameraRotation * Quaternion.Euler(rotationOffset);
        cinematicCamera.fieldOfView = initialCameraFieldOfView;
    }

    public void ResetShotReaction()
    {
        if (cinematicCamera == null)
        {
            return;
        }

        cinematicCamera.transform.SetPositionAndRotation(
            targetCameraPosition,
            targetCameraRotation);
        cinematicCamera.fieldOfView = initialCameraFieldOfView;
    }

    /// <summary>
    /// 작은 원형 스코프를 걷어내면서 전용 카메라를 끄고 기존 화면과 UI를 복구한다.
    /// </summary>
    public void SetReturnProgress(float progress)
    {
        float normalized = Mathf.Clamp01(progress);
        SetScopeAlpha(1f - normalized);
        SetGameplayUiAlpha(Mathf.Lerp(0f, initialGameplayUiAlpha, normalized));

        if (cinematicCamera != null)
        {
            cinematicCamera.enabled = false;
        }
    }

    /// <summary>
    /// 정상 완료, ESC 스킵과 Reset 모두에서 연출 전 화면 상태를 즉시 복구한다.
    /// </summary>
    public void RestoreImmediate()
    {
        RestoreVisualState();

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void CaptureInitialState()
    {
        initialGameplayUiAlpha = gameplayUiCanvasGroup.alpha;
        initialGameplayUiInteractable = gameplayUiCanvasGroup.interactable;
        initialGameplayUiBlocksRaycasts = gameplayUiCanvasGroup.blocksRaycasts;
        initialCameraEnabled = cinematicCamera.enabled;
        initialCameraPosition = cinematicCamera.transform.position;
        initialCameraRotation = cinematicCamera.transform.rotation;
        initialCameraFieldOfView = cinematicCamera.fieldOfView;
        targetCameraPosition = initialCameraPosition;
        targetCameraRotation = initialCameraRotation;
    }

    private void RestoreVisualState()
    {
        if (!isPresenting)
        {
            return;
        }

        isPresenting = false;
        UnsubscribeCancelAction();

        if (cinematicCamera != null)
        {
            cinematicCamera.transform.SetPositionAndRotation(
                initialCameraPosition,
                initialCameraRotation);
            cinematicCamera.fieldOfView = initialCameraFieldOfView;
            cinematicCamera.enabled = initialCameraEnabled;
        }

        if (gameplayUiCanvasGroup != null)
        {
            gameplayUiCanvasGroup.alpha = initialGameplayUiAlpha;
            gameplayUiCanvasGroup.interactable = initialGameplayUiInteractable;
            gameplayUiCanvasGroup.blocksRaycasts = initialGameplayUiBlocksRaycasts;
        }

        RestoreWorldIndicators();
        SetScopeAlpha(0f);
    }

    private void SyncCinematicCameraToGameplayCamera()
    {
        cinematicCamera.transform.SetPositionAndRotation(
            gameplayCamera.transform.position,
            gameplayCamera.transform.rotation);
        cinematicCamera.orthographic = gameplayCamera.orthographic;
        cinematicCamera.orthographicSize = gameplayCamera.orthographicSize;
        cinematicCamera.fieldOfView = gameplayCamera.fieldOfView;
        cinematicCamera.nearClipPlane = gameplayCamera.nearClipPlane;
        cinematicCamera.farClipPlane = gameplayCamera.farClipPlane;
    }

    private void FitScopeToViewport()
    {
        Canvas canvas = scopeOverlay.canvas;
        float viewportWidth = canvas != null ? canvas.pixelRect.width : Screen.width;
        float viewportHeight = canvas != null ? canvas.pixelRect.height : Screen.height;
        float canvasScaleFactor = canvas != null
            ? Mathf.Max(0.01f, canvas.scaleFactor)
            : 1f;
        float desiredDiameter =
            Mathf.Min(viewportWidth, viewportHeight) * Mathf.Clamp01(scopeViewportSize);
        float graphicRatio = Mathf.Max(0.01f, scopeGraphicDiameterRatio);
        float rectSize = desiredDiameter / graphicRatio / canvasScaleFactor;

        scopeOverlay.rectTransform.sizeDelta = Vector2.one * rectSize;
        scopeOverlay.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void SubscribeCancelAction()
    {
        if (!isPresenting || cancelAction == null || cancelAction.action == null)
        {
            return;
        }

        InputAction action = cancelAction.action;
        action.performed -= HandleCancelPerformed;
        action.performed += HandleCancelPerformed;

        if (!action.enabled)
        {
            action.Enable();
            cancelActionEnabledByView = true;
        }
    }

    private void UnsubscribeCancelAction()
    {
        if (cancelAction == null || cancelAction.action == null)
        {
            cancelActionEnabledByView = false;
            return;
        }

        InputAction action = cancelAction.action;
        action.performed -= HandleCancelPerformed;

        if (cancelActionEnabledByView && action.enabled)
        {
            action.Disable();
        }

        cancelActionEnabledByView = false;
    }

    private void HandleCancelPerformed(InputAction.CallbackContext _)
    {
        RequestSkip();
    }

    /// <summary>
    /// 연출 루트가 복귀 시 비활성화돼도 총성이 끝까지 재생되도록 독립된 2D AudioSource를 만든다.
    /// </summary>
    private static void PlayDetachedSfx(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        GameObject playbackObject = new("OpeningShotShotSfx");
        AudioSource playbackSource = playbackObject.AddComponent<AudioSource>();
        playbackSource.playOnAwake = false;
        playbackSource.loop = false;
        playbackSource.spatialBlend = 0f;
        playbackSource.clip = clip;
        playbackSource.Play();

        Destroy(playbackObject, clip.length + 0.1f);
    }

    private void SetGameplayUiAlpha(float alpha)
    {
        if (gameplayUiCanvasGroup != null)
        {
            gameplayUiCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    private void SetScopeAlpha(float alpha)
    {
        SetImageAlpha(scopeOverlay, alpha);
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private static Bounds CalculateTargetBounds(Transform target)
    {
        SkinnedMeshRenderer[] characterRenderers =
            target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Renderer[] renderers = characterRenderers.Length > 0
            ? characterRenderers
            : target.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds combinedBounds = new(target.position, Vector3.zero);

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds || combinedBounds.size.sqrMagnitude < 0.0001f)
        {
            combinedBounds = new Bounds(
                target.position + Vector3.up * 0.9f,
                new Vector3(0.8f, 1.8f, 0.8f));
        }

        return combinedBounds;
    }

    private static Vector3[] BuildBoundsCorners(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        return new[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
        };
    }

    private void HideWorldIndicators(IReadOnlyList<PieceBase> pieces)
    {
        hiddenWorldIndicators.Clear();
        if (pieces == null)
        {
            return;
        }

        for (int index = 0; index < pieces.Count; index++)
        {
            PieceBase piece = pieces[index];
            if (piece == null)
            {
                continue;
            }

            AddHiddenWorldIndicator(piece._HUD);

            Transform directionIndicator = piece.transform.Find("DirectionIndicator");
            AddHiddenWorldIndicator(
                directionIndicator != null ? directionIndicator.gameObject : null);
        }
    }

    private void AddHiddenWorldIndicator(GameObject indicator)
    {
        if (indicator == null)
        {
            return;
        }

        hiddenWorldIndicators.Add(new VisibilityState(indicator, indicator.activeSelf));
        indicator.SetActive(false);
    }

    private void RestoreWorldIndicators()
    {
        for (int index = 0; index < hiddenWorldIndicators.Count; index++)
        {
            VisibilityState state = hiddenWorldIndicators[index];
            if (state.GameObject != null)
            {
                state.GameObject.SetActive(state.WasActive);
            }
        }

        hiddenWorldIndicators.Clear();
    }

    private readonly struct VisibilityState
    {
        public VisibilityState(GameObject gameObject, bool wasActive)
        {
            GameObject = gameObject;
            WasActive = wasActive;
        }

        public GameObject GameObject { get; }
        public bool WasActive { get; }
    }

}
