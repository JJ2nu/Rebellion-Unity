using System;
using UnityEngine;
using UnityEngine.UI;

public readonly struct OrderSkillViewState
{
    public static OrderSkillViewState Hidden => new(
        false,
        true,
        UIButtonLockMode.None,
        false,
        false);

    public bool IsVisible { get; }
    public bool IsAvailable { get; }
    public UIButtonLockMode LockMode { get; }
    public bool IsScopeVisible { get; }
    public bool IsTargetingUiBlocked { get; }

    public OrderSkillViewState(
        bool isVisible,
        bool isAvailable,
        UIButtonLockMode lockMode,
        bool isScopeVisible,
        bool isTargetingUiBlocked)
    {
        IsVisible = isVisible;
        IsAvailable = isAvailable;
        LockMode = lockMode;
        IsScopeVisible = isScopeVisible;
        IsTargetingUiBlocked = isTargetingUiBlocked;
    }
}

/// <summary>
/// Order 버튼 생성과 Sprite, 잠금, Scope, 전체 UI 차단 표시만 담당하는 Passive View다.
/// </summary>
public sealed class OrderSkillView : MonoBehaviour
{
    [SerializeField] private GameObject orderSkillButtonPrefab;
    [SerializeField] private OpeningShotScopePreview openingShotScopePreview;
    [SerializeField] private GameObject targetingUiClickBlocker;

    private Button orderSkillButton;
    private UIButtonLockView orderSkillButtonLockView;

    public event Action OrderRequested;
    public event Action CancelRequested;

    private void OnEnable()
    {
        SubscribeScopeCancelRequest();
    }

    private void OnDisable()
    {
        UnsubscribeScopeCancelRequest();
        Apply(OrderSkillViewState.Hidden);
    }

    private void OnDestroy()
    {
        UnsubscribeScopeCancelRequest();
        ClearRenderedButton();
    }

    public void Render(bool isVisible)
    {
        ClearRenderedButton();

        if (!isVisible)
        {
            Apply(OrderSkillViewState.Hidden);
            return;
        }

        if (orderSkillButtonPrefab == null)
        {
            Debug.LogWarning($"{nameof(OrderSkillView)} has no Order Skill button prefab assigned.", this);
            return;
        }

        GameObject buttonObject = Instantiate(orderSkillButtonPrefab, transform, false);
        orderSkillButton = buttonObject.GetComponent<Button>();
        if (orderSkillButton == null)
        {
            Debug.LogWarning("Order skill button prefab has no Button component.", this);
            buttonObject.SetActive(false);
            Destroy(buttonObject);
            return;
        }

        orderSkillButtonLockView = buttonObject.GetComponent<UIButtonLockView>();
        if (orderSkillButtonLockView == null)
        {
            Debug.LogWarning("Order skill button prefab has no UIButtonLockView component.", this);
        }

        orderSkillButton.onClick.AddListener(HandleOrderRequested);
    }

    public void Apply(OrderSkillViewState state)
    {
        if (orderSkillButton != null)
        {
            orderSkillButton.gameObject.SetActive(state.IsVisible);

            if (orderSkillButtonLockView != null)
            {
                orderSkillButtonLockView.SetAvailableVisual(state.IsAvailable);
                orderSkillButtonLockView.SetLockMode(state.LockMode);
            }
            else
            {
                orderSkillButton.interactable = state.LockMode == UIButtonLockMode.None;
            }
        }

        if (state.IsScopeVisible)
        {
            openingShotScopePreview?.ShowScope();
        }
        else
        {
            openingShotScopePreview?.HideScope();
        }

        SetTargetingUiBlocked(state.IsTargetingUiBlocked);
    }

    private void HandleOrderRequested()
    {
        OrderRequested?.Invoke();
    }

    private void SubscribeScopeCancelRequest()
    {
        if (openingShotScopePreview == null)
        {
            return;
        }

        openingShotScopePreview.CancelRequested -= HandleScopeCancelRequested;
        openingShotScopePreview.CancelRequested += HandleScopeCancelRequested;
    }

    private void UnsubscribeScopeCancelRequest()
    {
        if (openingShotScopePreview != null)
        {
            openingShotScopePreview.CancelRequested -= HandleScopeCancelRequested;
        }
    }

    private void HandleScopeCancelRequested()
    {
        CancelRequested?.Invoke();
    }

    private void SetTargetingUiBlocked(bool isBlocked)
    {
        if (targetingUiClickBlocker == null)
        {
            return;
        }

        targetingUiClickBlocker.SetActive(isBlocked);
        if (isBlocked)
        {
            targetingUiClickBlocker.transform.SetAsLastSibling();
        }
    }

    private void ClearRenderedButton()
    {
        if (orderSkillButton != null)
        {
            orderSkillButton.onClick.RemoveListener(HandleOrderRequested);
        }

        orderSkillButton = null;
        orderSkillButtonLockView = null;

        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            GameObject child = transform.GetChild(index).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }
}
