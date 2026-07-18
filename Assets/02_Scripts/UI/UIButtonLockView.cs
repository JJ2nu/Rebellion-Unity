using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼의 공통 잠금 상태를 CanvasGroup과 Sprite 전환으로 관리하는 재사용 UI 컴포넌트다.
/// </summary>
public enum UIButtonLockMode
{
    // 잠금이 없는 기본 상태다.
    None,

    // 현재 Act/Deact 이미지는 유지하고 hover/click 입력과 사운드만 막는 상태다.
    InteractionOnly,

    // 사용할 수 있는 버튼은 전용 Lock Sprite로 바꾸고 hover/click 입력과 사운드를 막는 상태다.
    VisualDisabled
}

/// <summary>
/// Storage 버튼, OrderSkillButton, 이후 추가될 스킬 버튼의 잠금 방식을 한 곳에서 통일한다.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class UIButtonLockView : MonoBehaviour
{
    #region Fields

    // 잠금 상태를 적용할 실제 Unity Button이다. 비어 있으면 같은 오브젝트의 Button을 자동으로 사용한다.
    [SerializeField] private Button button;
    // Sprite를 직접 교체할 Image다. 보통 Button.targetGraphic에 연결된 Image를 사용한다.
    [SerializeField] private Image targetImage;
    // 버튼이 게임 규칙상 사용할 수 있는 상태일 때 표시할 Sprite다.
    [SerializeField] private Sprite activeSprite;
    // Button SpriteState의 hover/pressed 상태에 사용할 Sprite다.
    [SerializeField] private Sprite hoverSprite;
    // 버튼이 게임 규칙상 사용할 수 없는 상태일 때 표시할 Sprite다. Button 컴포넌트의 Disabled Sprite와 별개다.
    [SerializeField] private Sprite unavailableSprite;
    // 시뮬레이션처럼 사용할 수 있는 버튼이 일시 잠길 때 표시할 전용 Sprite다.
    [SerializeField] private Sprite lockSprite;

    [SerializeField] private CanvasGroup canvasGroup;
    private UIButtonLockMode currentMode = UIButtonLockMode.None;
    private bool isAvailableVisual = true;

    #endregion

    #region Unity Events

    private void Reset()
    {
        // Inspector에서 컴포넌트를 새로 붙였을 때 기본 참조를 자동으로 채운다.
        CacheDefaultReferences();
    }

    private void Awake()
    {
        // 런타임 생성 버튼에도 동작하도록 참조와 Sprite 기본값을 시작 시점에 보충한다.
        CacheDefaultReferences();
        CacheDefaultSprites();
        ApplyButtonSpriteState();
        ApplyVisualState();
        ApplyInputLock();
    }

    #endregion

    #region Public Methods

    public void Configure(
        Button targetButton,
        Image image,
        Sprite active,
        Sprite hover,
        Sprite unavailable,
        Sprite locked)
    {
        // Prefab에 직접 연결하기 어려운 런타임 생성 버튼은 이 함수로 필요한 참조를 주입한다.
        button = targetButton;
        targetImage = image;
        activeSprite = active;
        hoverSprite = hover;
        unavailableSprite = unavailable;
        lockSprite = locked;

        CacheDefaultReferences();
        CacheDefaultSprites();
        ApplyButtonSpriteState();
        ApplyVisualState();
        ApplyInputLock();
    }

    public void SetAvailableVisual(bool isAvailable)
    {
        // 스킬 사용 여부나 Storage 수량처럼 버튼의 Act/Deact를 정하는 게임 규칙은 lock 모드와 분리해서 적용한다.
        CacheDefaultReferences();
        CacheDefaultSprites();

        isAvailableVisual = isAvailable;
        ApplyVisualState();
    }

    public void SetLockMode(UIButtonLockMode mode)
    {
        CacheDefaultReferences();
        CacheDefaultSprites();
        ApplyButtonSpriteState();
        ApplyLockMode(mode);
    }

    public void SetVisualLocked(bool isLocked)
    {
        // 기존 호출부 호환용 함수다. 새 코드는 SetLockMode를 우선 사용한다.
        SetLockMode(isLocked ? UIButtonLockMode.VisualDisabled : UIButtonLockMode.None);
    }

    public void SetClickBlocked(bool isBlocked)
    {
        // 기존 호출부 호환용 함수다. 이미지를 유지한 채 입력만 막을 때 사용한다.
        SetLockMode(isBlocked ? UIButtonLockMode.InteractionOnly : UIButtonLockMode.None);
    }

    #endregion

    #region State

    private void ApplyLockMode(UIButtonLockMode mode)
    {
        currentMode = mode;
        ApplyVisualState();
        ApplyInputLock();

        if (mode != UIButtonLockMode.None)
        {
            ClearButtonPointerState();
        }
    }

    private void ApplyVisualState()
    {
        if (targetImage == null)
        {
            return;
        }

        Sprite nextSprite = ResolveSpriteForCurrentState();
        if (nextSprite != null)
        {
            targetImage.sprite = nextSprite;
        }

        targetImage.color = Color.white;
    }

    private Sprite ResolveSpriteForCurrentState()
    {
        // PDF 기준: 이미 사용할 수 없는 버튼은 시뮬레이션 중에도 Deact를 유지하고, 사용 가능한 버튼만 Lock Sprite로 바꾼다.
        if (currentMode == UIButtonLockMode.VisualDisabled && isAvailableVisual && lockSprite != null)
        {
            return lockSprite;
        }

        return isAvailableVisual ? activeSprite : unavailableSprite;
    }

    private void ApplyInputLock()
    {
        EnsureCanvasGroup();

        if (canvasGroup == null)
        {
            return;
        }

        // Button.interactable은 수량 0 같은 각 버튼의 고유 규칙에 맡기고, 공통 잠금은 Raycast만 차단한다.
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = currentMode == UIButtonLockMode.None;
    }

    private void ApplyButtonSpriteState()
    {
        if (button == null)
        {
            return;
        }

        // Button의 Disabled Sprite는 Unity Button 고유 설정으로 남겨 두고, 게임 규칙상 불가 이미지는 unavailableSprite로만 처리한다.
        SpriteState spriteState = button.spriteState;
        spriteState.highlightedSprite = hoverSprite;
        spriteState.pressedSprite = hoverSprite;
        spriteState.selectedSprite = activeSprite;
        button.spriteState = spriteState;
    }

    private void CacheDefaultReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (targetImage == null && button != null)
        {
            targetImage = button.targetGraphic as Image;
        }
    }

    private void CacheDefaultSprites()
    {
        if (targetImage != null && activeSprite == null)
        {
            activeSprite = targetImage.sprite;
        }

        if (button == null)
        {
            return;
        }

        SpriteState spriteState = button.spriteState;
        if (hoverSprite == null)
        {
            hoverSprite = spriteState.highlightedSprite;
        }

        // unavailableSprite와 lockSprite는 의도적으로 Button.disabledSprite에서 자동 복사하지 않는다.
        // Button 컴포넌트의 Disabled Sprite, 게임 규칙상 Deact, 시뮬레이션 Lock 이미지를 서로 분리하기 위해서다.
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup != null)
        {
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            Debug.LogWarning($"{nameof(UIButtonLockView)} has no CanvasGroup assigned.", this);
        }
    }

    private void ClearButtonPointerState()
    {
        // 잠금 직전에 마우스가 올라가 있던 버튼은 Hover/Selected 상태가 남을 수 있어서 즉시 초기화한다.
        if (button == null || EventSystem.current == null)
        {
            return;
        }

        if (EventSystem.current.currentSelectedGameObject == button.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        button.OnPointerExit(new PointerEventData(EventSystem.current));
    }

    #endregion
}
