using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simulation Controls의 표시 규칙을 한 번에 전달하는 불변 View 상태다.
/// </summary>
public readonly struct StageSimulationControlsViewState
{
    public bool IsPlayVisible { get; }
    public bool IsPlayInteractable { get; }
    public bool UseInactivePlaySprite { get; }
    public bool AreResultActionsVisible { get; }

    public StageSimulationControlsViewState(
        bool isSimulationRunning,
        bool hasSimulationResult)
    {
        IsPlayVisible = !hasSimulationResult;
        IsPlayInteractable = !isSimulationRunning && !hasSimulationResult;
        UseInactivePlaySprite = isSimulationRunning;
        AreResultActionsVisible = hasSimulationResult;
    }
}

/// <summary>
/// Play, Retry, Confirm 버튼의 입력 전달과 표시, Sprite 적용만 담당하는 Passive View다.
/// Simulation 상태나 결과 흐름은 판단하지 않고 Controller가 전달한 View 상태만 반영한다.
/// </summary>
public sealed class StageSimulationControlsView : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button confirmButton;

    private Image playButtonImage;
    private Sprite playActiveSprite;
    private Sprite playInactiveSprite;

    public event Action PlayRequested;
    public event Action RetryRequested;
    public event Action ConfirmRequested;

    private void Awake()
    {
        WarnIfButtonIsMissing(playButton, "Play");
        WarnIfButtonIsMissing(retryButton, "Retry");
        WarnIfButtonIsMissing(confirmButton, "Confirm");
        EnsurePlayButtonPresentation();
        MatchConfirmButtonToPlayButton();
    }

    public void Apply(StageSimulationControlsViewState state)
    {
        EnsurePlayButtonPresentation();

        if (playButton != null)
        {
            playButton.gameObject.SetActive(state.IsPlayVisible);
            playButton.interactable = state.IsPlayInteractable;
            SetPlayButtonSprite(state.UseInactivePlaySprite ? playInactiveSprite : playActiveSprite);
        }

        ApplyResultActionState(retryButton, state.AreResultActionsVisible);
        ApplyResultActionState(confirmButton, state.AreResultActionsVisible);
    }

    // Scene의 기존 Button persistent onClick은 이 View 메서드를 호출하고 Controller는 C# 이벤트만 구독한다.
    public void RequestPlay()
    {
        PlayRequested?.Invoke();
    }

    public void RequestRetry()
    {
        RetryRequested?.Invoke();
    }

    public void RequestConfirm()
    {
        ConfirmRequested?.Invoke();
    }

    public void MatchConfirmButtonToPlayButton()
    {
        if (playButton == null || confirmButton == null)
        {
            return;
        }

        RectTransform playRect = playButton.transform as RectTransform;
        RectTransform confirmRect = confirmButton.transform as RectTransform;
        if (playRect == null || confirmRect == null)
        {
            return;
        }

        confirmRect.anchorMin = playRect.anchorMin;
        confirmRect.anchorMax = playRect.anchorMax;
        confirmRect.pivot = playRect.pivot;
        confirmRect.anchoredPosition = playRect.anchoredPosition;
        confirmRect.sizeDelta = playRect.sizeDelta;
        confirmRect.localRotation = playRect.localRotation;
        confirmRect.localScale = playRect.localScale;
    }

    private void ApplyResultActionState(Button button, bool isVisible)
    {
        if (button == null)
        {
            return;
        }

        button.gameObject.SetActive(isVisible);
        button.interactable = isVisible;
    }

    private void EnsurePlayButtonPresentation()
    {
        if (playButton == null)
        {
            return;
        }

        if (playButtonImage == null)
        {
            playButtonImage = playButton.targetGraphic as Image;
        }

        if (playActiveSprite == null && playButtonImage != null)
        {
            playActiveSprite = playButtonImage.sprite;
        }

        SpriteState spriteState = playButton.spriteState;
        if (playInactiveSprite == null)
        {
            playInactiveSprite = spriteState.selectedSprite != null
                ? spriteState.selectedSprite
                : spriteState.pressedSprite;
        }

        if (playInactiveSprite != null && spriteState.disabledSprite == null)
        {
            spriteState.disabledSprite = playInactiveSprite;
            playButton.spriteState = spriteState;
        }

        ColorBlock colors = playButton.colors;
        if (colors.disabledColor.a < 1f ||
            colors.disabledColor.r < 1f ||
            colors.disabledColor.g < 1f ||
            colors.disabledColor.b < 1f)
        {
            colors.disabledColor = Color.white;
            playButton.colors = colors;
        }
    }

    private void SetPlayButtonSprite(Sprite sprite)
    {
        if (playButtonImage != null && sprite != null)
        {
            playButtonImage.sprite = sprite;
        }
    }

    private void WarnIfButtonIsMissing(Button button, string buttonName)
    {
        if (button == null)
        {
            Debug.LogWarning(
                $"{nameof(StageSimulationControlsView)} has no {buttonName} button assigned.",
                this);
        }
    }
}
