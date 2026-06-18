using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼의 비활성 이미지를 직접 관리하거나, 이미지 유지 상태로 클릭만 막아야 할 때 사용하는 공용 UI 컴포넌트다.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class UIButtonLockView : MonoBehaviour
{
    #region Fields

    // 잠금 상태를 적용할 실제 Unity Button이다. 비어 있으면 Awake에서 같은 오브젝트의 Button을 자동으로 찾는다.
    [SerializeField] private Button button;
    // Sprite를 직접 교체할 Image다. 보통 Button.targetGraphic에 연결된 Image를 사용한다.
    [SerializeField] private Image targetImage;
    // 버튼이 사용 가능한 상태일 때 표시할 기본 이미지다.
    [SerializeField] private Sprite activeSprite;
    // 마우스가 올라가거나 누르고 있을 때 Button SpriteState에 넣을 이미지다.
    [SerializeField] private Sprite hoverSprite;
    // 타겟 확정처럼 버튼 자체가 비활성 상태로 보여야 할 때 표시할 이미지다.
    [SerializeField] private Sprite disabledSprite;

        // 이미지는 유지하면서 hover/click 입력과 사운드만 막기 위해 버튼 위에 올리는 투명 Raycast 오브젝트다.
        private GameObject clickBlocker;

    #endregion

    #region Unity Events

    private void Reset()
    {
        // 컴포넌트를 Inspector에서 새로 붙였을 때 기본 참조가 자동으로 채워지게 한다.
        CacheDefaultReferences();
    }

    private void Awake()
    {
        // 런타임 생성 버튼에도 동작하도록 필수 참조와 Sprite 기본값을 시작 시점에 보충한다.
        CacheDefaultReferences();
        CacheDefaultSprites();
        ApplySpriteState(false);
    }

    #endregion

    #region Public Methods

    public void SetVisualLocked(bool isLocked)
    {
        // Visual Locked는 Button.interactable까지 바꾸는 잠금이다.
        // Disabled 이미지가 보여도 되는 상태에서 사용한다.
        CacheDefaultReferences();
        CacheDefaultSprites();

        ApplySpriteState(isLocked);

        if (button != null)
        {
            // 실제 비활성 상태는 Button.interactable로 처리해 hover/click SFX와 onClick까지 함께 막는다.
            button.interactable = !isLocked;
        }

        SetClickBlocked(false);
    }

    public void SetClickBlocked(bool isBlocked)
    {
        // Click Blocked는 Button.interactable을 건드리지 않는 잠금이다.
        // 이미지는 Act로 유지해야 하지만 클릭 기능/클릭음은 막아야 하는 상태에서 사용한다.
        CacheDefaultReferences();

        if (!isBlocked && clickBlocker == null)
        {
            return;
        }

        // 이미지는 현재 상태 그대로 두고 투명 Raycast Image만 올려 hover/click 입력과 사운드를 막는다.
        if (clickBlocker == null)
        {
            RectTransform buttonRect = button != null ? button.transform as RectTransform : null;
            if (buttonRect == null)
            {
                return;
            }

            clickBlocker = CreateClickBlocker(buttonRect);
        }

        clickBlocker.SetActive(isBlocked);
        if (isBlocked)
        {
            clickBlocker.transform.SetAsLastSibling();
            ClearButtonPointerState();
        }
    }

    #endregion

    #region State

    private void ApplySpriteState(bool isLocked)
    {
        // Button의 SpriteState와 실제 Image.sprite를 함께 맞춰 hover/pressed/disabled 표시가 어긋나지 않게 한다.
        if (button == null)
        {
            return;
        }

        SpriteState spriteState = button.spriteState;
        spriteState.highlightedSprite = hoverSprite;
        spriteState.pressedSprite = isLocked ? disabledSprite : hoverSprite;
        spriteState.selectedSprite = isLocked ? disabledSprite : activeSprite;
        spriteState.disabledSprite = disabledSprite;
        button.spriteState = spriteState;

        if (targetImage != null)
        {
            targetImage.sprite = isLocked && disabledSprite != null
                ? disabledSprite
                : activeSprite;
        }
    }

    private void CacheDefaultReferences()
    {
        // Prefab에 직접 연결하지 않아도 같은 오브젝트의 Button과 targetGraphic을 기본값으로 사용한다.
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
        // Inspector에 Sprite를 넣지 않은 버튼도 기존 Button/Image 설정을 그대로 재사용할 수 있게 한다.
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

        if (disabledSprite == null)
        {
            disabledSprite = spriteState.disabledSprite != null
                ? spriteState.disabledSprite
                : spriteState.pressedSprite != null
                    ? spriteState.pressedSprite
                    : spriteState.selectedSprite;
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

    private static GameObject CreateClickBlocker(RectTransform buttonRect)
    {
        // 버튼 자식으로 만들면 이벤트가 부모 Button까지 올라갈 수 있으므로, 같은 부모 아래 형제 오브젝트로 덮는다.
        Transform blockerParent = buttonRect.parent;
        if (blockerParent == null)
        {
            blockerParent = buttonRect;
        }

        GameObject blocker = new("UIButtonClickBlocker", typeof(RectTransform), typeof(Image));
        blocker.transform.SetParent(blockerParent, false);

        RectTransform rectTransform = blocker.GetComponent<RectTransform>();
        rectTransform.anchorMin = buttonRect.anchorMin;
        rectTransform.anchorMax = buttonRect.anchorMax;
        rectTransform.anchoredPosition = buttonRect.anchoredPosition;
        rectTransform.sizeDelta = buttonRect.sizeDelta;
        rectTransform.pivot = buttonRect.pivot;
        rectTransform.localRotation = buttonRect.localRotation;
        rectTransform.localScale = buttonRect.localScale;

        Image image = blocker.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        blocker.SetActive(false);
        return blocker;
    }

    #endregion
}
