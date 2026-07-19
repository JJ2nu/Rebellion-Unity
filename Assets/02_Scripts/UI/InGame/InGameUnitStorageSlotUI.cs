// 대원 배치 버튼의 남은 배치 수를 표시하고 클릭에 따라 수량과 이미지를 갱신한다.

using System.Collections;
using System;
using Rebellion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class InGameUnitStorageSlotUI : MonoBehaviour, IPointerUpHandler
{
    #region Fields

    [SerializeField] private Button button;
    [SerializeField] private Image storageImage;
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private Sprite deactiveSprite;
    [SerializeField] private Sprite lockedSprite;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private UIButtonLockView buttonLockView;

    public PieceType UnitType { get; private set; }
    public int MaxDeployableCount { get; private set; }
    public int RemainingDeployableCount { get; private set; }

    public event Action<InGameUnitStorageSlotUI> Clicked;
    private bool interactionLocked;

    #endregion

    #region Public Methods

    public void Bind(PieceType unitType, int deployableCount)
    {
        UnitType = unitType;
        MaxDeployableCount = Mathf.Max(0, deployableCount);
        RemainingDeployableCount = MaxDeployableCount;

        if (button != null)
        {
            button.targetGraphic = storageImage;
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
            ApplyButtonSpriteState();
        }

        EnsureButtonLockView();
        UpdateView();
        ApplyInteractionLockState();
    }

    public bool TryConsumeOne()
    {
        if (RemainingDeployableCount <= 0)
        {
            return false;
        }

        RemainingDeployableCount--;
        UpdateView();
        ApplyInteractionLockState();
        return true;
    }

    public bool TryRestoreOne()
    {
        if (RemainingDeployableCount >= MaxDeployableCount)
        {
            return false;
        }

        RemainingDeployableCount++;
        UpdateView();
        ApplyInteractionLockState();
        return true;
    }

    public void SetInteractionLocked(bool isLocked)
    {
        interactionLocked = isLocked;

        // 실행 중 잠금은 공용 버튼 잠금 컴포넌트에 위임하되, 수량별 이미지는 UpdateView가 계속 관리한다.
        UpdateView();
        ApplyInteractionLockState();
    }

    #endregion

    #region Button Events

    private void HandleClick()
    {
        if (interactionLocked)
        {
            return;
        }

        if (RemainingDeployableCount <= 0)
        {
            return;
        }

        Clicked?.Invoke(this);

        if (RemainingDeployableCount > 0)
        {
            StartCoroutine(ResetActiveSpriteAfterClick());
        }
    }

    #endregion

    #region Pointer Events

    public void OnPointerUp(PointerEventData eventData)
    {
        if (interactionLocked)
        {
            return;
        }

        if (RemainingDeployableCount > 0)
        {
            StartCoroutine(ResetActiveSpriteAfterClick());
        }
    }

    #endregion

    #region View

    private void UpdateView()
    {
        bool hasRemainingCount = RemainingDeployableCount > 0;

        if (countText == null)
        {
            Debug.LogWarning($"{nameof(InGameUnitStorageSlotUI)} has no count text assigned.", this);
        }
        else
        {
            countText.text = RemainingDeployableCount.ToString();
        }

        if (button != null)
        {
            button.interactable = hasRemainingCount;
        }

        if (storageImage != null)
        {
            SetStorageSprite(hasRemainingCount ? activeSprite : deactiveSprite);
        }
    }

    private void SetStorageSprite(Sprite sprite)
    {
        if (storageImage != null && sprite != null)
        {
            storageImage.sprite = sprite;
        }
    }

    #endregion

    #region Button State

    private void ApplyButtonSpriteState()
    {
        if (button == null)
        {
            return;
        }

        SpriteState spriteState = button.spriteState;
        spriteState.disabledSprite = deactiveSprite;
        button.spriteState = spriteState;
    }

    private void EnsureButtonLockView()
    {
        if (buttonLockView == null)
        {
            buttonLockView = GetComponent<UIButtonLockView>();
        }

        if (buttonLockView == null)
        {
            Debug.LogWarning($"{nameof(InGameUnitStorageSlotUI)} has no button lock view assigned.", this);
            return;
        }

        buttonLockView.Configure(
            button,
            storageImage,
            activeSprite,
            null,
            deactiveSprite,
            lockedSprite
        );
    }

    private void ApplyInteractionLockState()
    {
        EnsureButtonLockView();

        UIButtonLockMode lockMode = interactionLocked
            ? UIButtonLockMode.VisualDisabled
            : UIButtonLockMode.None;

        if (buttonLockView != null)
        {
            buttonLockView.SetAvailableVisual(RemainingDeployableCount > 0);
            buttonLockView.SetLockMode(lockMode);
        }
    }

    private IEnumerator ResetActiveSpriteAfterClick()
    {
        yield return null;

        if (interactionLocked || RemainingDeployableCount <= 0)
        {
            yield break;
        }

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        SetStorageSprite(activeSprite);
    }

    #endregion
}
