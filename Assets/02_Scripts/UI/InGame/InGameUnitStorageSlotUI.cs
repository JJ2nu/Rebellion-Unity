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
    [SerializeField] private TMP_Text countText;

    public PieceType UnitType { get; private set; }
    public int MaxDeployableCount { get; private set; }
    public int RemainingDeployableCount { get; private set; }

    public event Action<InGameUnitStorageSlotUI> Clicked;
    private bool interactionLocked;
    private CanvasGroup canvasGroup;

    #endregion

    #region Public Methods

    public void Bind(PieceType unitType, int deployableCount)
    {
        EnsureCanvasGroup();

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

        UpdateView();
    }

    public bool TryConsumeOne()
    {
        if (RemainingDeployableCount <= 0)
        {
            return false;
        }

        RemainingDeployableCount--;
        UpdateView();
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
        return true;
    }

    public void SetInteractionLocked(bool isLocked)
    {
        EnsureCanvasGroup();

        interactionLocked = isLocked;
        canvasGroup.blocksRaycasts = !isLocked;
        UpdateView();
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

    private void EnsureCanvasGroup()
    {
        if (canvasGroup != null)
        {
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = !interactionLocked;
    }

    private IEnumerator ResetActiveSpriteAfterClick()
    {
        yield return null;

        if (RemainingDeployableCount <= 0)
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
