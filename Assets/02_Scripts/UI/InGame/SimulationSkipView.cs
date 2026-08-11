using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 시뮬레이션 연출 스킵 버튼의 표시와 입력 전달만 담당하는 Passive View다.
/// 스킵 가능 조건과 실행 흐름은 판단하지 않고 Controller가 전달한 표시 상태만 반영한다.
/// </summary>
public sealed class SimulationSkipView : MonoBehaviour
{
    [SerializeField] private Button skipButton;

    [Header("Input")]
    [SerializeField] private InputActionReference cancelAction;

    private bool isVisible;
    private bool cancelActionEnabledByView;

    public event Action SkipRequested;

    public bool IsVisible => isVisible;

    // Scene의 Button persistent onClick은 이 메서드를 호출하고 Controller는 C# 이벤트만 구독한다.
    public void RequestSkip()
    {
        if (isVisible)
        {
            SkipRequested?.Invoke();
        }
    }

    /// <summary>
    /// 버튼 표시와 ESC 입력 구독을 함께 전환한다.
    /// ESC 스킵은 버튼이 보이는 동안만 받아 배치 모드 등 다른 상태의 Cancel 입력과 겹치지 않게 한다.
    /// </summary>
    public void SetVisible(bool visible)
    {
        isVisible = visible;

        if (skipButton != null && skipButton.gameObject.activeSelf != visible)
        {
            skipButton.gameObject.SetActive(visible);
        }

        if (visible)
        {
            SubscribeCancelAction();
        }
        else
        {
            UnsubscribeCancelAction();
        }
    }

    private void OnDisable()
    {
        UnsubscribeCancelAction();
    }

    private void SubscribeCancelAction()
    {
        if (cancelAction == null || cancelAction.action == null)
        {
            return;
        }

        InputAction action = cancelAction.action;
        action.performed -= HandleCancelPerformed;
        action.performed += HandleCancelPerformed;

        // 다른 시스템이 켠 액션을 끄지 않도록 View가 직접 켠 경우만 기억한다.
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
}
