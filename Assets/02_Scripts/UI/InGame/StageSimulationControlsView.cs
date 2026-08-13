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
        bool isPlayVisible,
        bool isPlayInteractable,
        bool useInactivePlaySprite,
        bool areResultActionsVisible)
    {
        IsPlayVisible = isPlayVisible;
        IsPlayInteractable = isPlayInteractable;
        UseInactivePlaySprite = useInactivePlaySprite;
        AreResultActionsVisible = areResultActionsVisible;
    }
}

/// <summary>
/// 키보드 조작이 Retry/Confirm 버튼에 표시할 시각 상태다.
/// Normal은 기본, Hover는 하이라이트(선택 대상), Pressed는 확정 입력 중을 뜻한다.
/// </summary>
public enum SimulationResultButtonKeyboardVisual
{
    Normal,
    Hover,
    Pressed,
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

    // 키보드 하이라이트 해제 시 되돌릴 기본 Sprite 캐시다. 마우스 SpriteSwap은 overrideSprite를 쓰므로 충돌하지 않는다.
    private Sprite retryBaseSprite;
    private Sprite confirmBaseSprite;

    // 키보드 조작이 마우스와 같은 hover/클릭 SFX를 내도록 버튼의 UIButtonSfx를 캐시한다.
    private UIButtonSfx retryButtonSfx;
    private UIButtonSfx confirmButtonSfx;

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

        // 버튼이 비활성일 때도 GetComponent는 동작하므로 키보드 SFX 위임 대상을 미리 캐시한다.
        retryButtonSfx = retryButton != null ? retryButton.GetComponent<UIButtonSfx>() : null;
        confirmButtonSfx = confirmButton != null ? confirmButton.GetComponent<UIButtonSfx>() : null;
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

    // 키보드 조작의 Retry/Confirm 하이라이트·확정 표시를 한 번에 적용한다.
    // 어떤 버튼이 어떤 상태여야 하는지는 Controller가 결정한다.
    public void ApplyResultKeyboardVisuals(
        SimulationResultButtonKeyboardVisual retryVisual,
        SimulationResultButtonKeyboardVisual confirmVisual)
    {
        ApplyResultKeyboardVisual(retryButton, ref retryBaseSprite, retryVisual);
        ApplyResultKeyboardVisual(confirmButton, ref confirmBaseSprite, confirmVisual);
    }

    public void PlayResultKeyboardHoverSfx(bool onRetryButton)
    {
        UIButtonSfx sfx = onRetryButton ? retryButtonSfx : confirmButtonSfx;
        sfx?.PlayHoverSfxForKeyboard();
    }

    public void PlayResultKeyboardClickSfx(bool onRetryButton)
    {
        UIButtonSfx sfx = onRetryButton ? retryButtonSfx : confirmButtonSfx;
        sfx?.PlayClickSfxForKeyboard();
    }

    // Spacebar Play 입력이 유지되는 동안 마우스 press-hold와 같은 pressed Sprite를 표시한다.
    // 복원은 Controller가 ApplyState 재적용으로 처리하므로 여기서는 표시만 담당한다.
    public void ShowPlayPressedSprite()
    {
        EnsurePlayButtonPresentation();
        if (playButton == null)
        {
            return;
        }

        Sprite pressedSprite = playButton.spriteState.pressedSprite;
        SetPlayButtonSprite(pressedSprite != null ? pressedSprite : playInactiveSprite);
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

    // 키보드 시각 상태를 Image.sprite로 직접 적용한다. 마우스 SpriteSwap은 overrideSprite 위에 얹히므로
    // 마우스 hover/press가 끝나면 여기서 정한 키보드 상태 Sprite로 자연스럽게 돌아온다.
    private static void ApplyResultKeyboardVisual(
        Button button,
        ref Sprite baseSprite,
        SimulationResultButtonKeyboardVisual visual)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.targetGraphic as Image;
        if (image == null)
        {
            return;
        }

        if (baseSprite == null)
        {
            baseSprite = image.sprite;
        }

        Sprite target = baseSprite;
        if (visual == SimulationResultButtonKeyboardVisual.Hover && button.spriteState.highlightedSprite != null)
        {
            target = button.spriteState.highlightedSprite;
        }
        else if (visual == SimulationResultButtonKeyboardVisual.Pressed && button.spriteState.pressedSprite != null)
        {
            target = button.spriteState.pressedSprite;
        }

        if (target != null)
        {
            image.sprite = target;
        }
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
