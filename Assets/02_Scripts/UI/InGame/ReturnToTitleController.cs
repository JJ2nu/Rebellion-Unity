using System;
using UnityEngine;

/// <summary>
/// 타이틀 복귀 버튼의 활성 상태와 확인 팝업 흐름을 관리하고, 확정 시 캠페인을 정리하며 Title로 복귀한다.
/// 팝업이 열려 있는 동안에는 StageInputModalGate 잠금으로 배치·월드 입력을 막는다.
/// </summary>
[RequireComponent(typeof(ReturnToTitleView))]
public sealed class ReturnToTitleController : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private SimulationController simulationController;

    private ReturnToTitleView view;
    private IDisposable inputBlockLease;
    // SimulationFinished 이후 결과 확정 대기 동안 RunningStateChanged(false)가 늦게 오므로
    // SimulationSkipController와 같은 방식으로 실행 구간을 이벤트로 직접 추적한다.
    private bool isSimulationExecuting;
    // 복귀가 확정되면 씬이 내려갈 때까지 버튼과 팝업 입력을 다시 받지 않는다.
    private bool isReturning;

    private void Awake()
    {
        view = GetComponent<ReturnToTitleView>();
    }

    private void OnEnable()
    {
        EnsureBindings();

        if (simulationController != null)
        {
            simulationController.RunningStateChanged -= HandleRunningStateChanged;
            simulationController.RunningStateChanged += HandleRunningStateChanged;
            simulationController.SimulationFinished -= HandleSimulationFinished;
            simulationController.SimulationFinished += HandleSimulationFinished;
            simulationController.SimulationReset -= HandleSimulationReset;
            simulationController.SimulationReset += HandleSimulationReset;
        }

        if (view != null)
        {
            view.ReturnRequested -= HandleReturnRequested;
            view.ReturnRequested += HandleReturnRequested;
            view.ConfirmRequested -= HandleConfirmRequested;
            view.ConfirmRequested += HandleConfirmRequested;
            view.CancelRequested -= HandleCancelRequested;
            view.CancelRequested += HandleCancelRequested;
        }

        // 튜토리얼 같은 다른 모달이 열리고 닫힐 때 버튼 활성 상태를 함께 갱신한다.
        StageInputModalGate.BlockedStateChanged -= HandleModalBlockedChanged;
        StageInputModalGate.BlockedStateChanged += HandleModalBlockedChanged;

        ApplyButtonState();
    }

    private void OnDisable()
    {
        if (simulationController != null)
        {
            simulationController.RunningStateChanged -= HandleRunningStateChanged;
            simulationController.SimulationFinished -= HandleSimulationFinished;
            simulationController.SimulationReset -= HandleSimulationReset;
        }

        if (view != null)
        {
            view.ReturnRequested -= HandleReturnRequested;
            view.ConfirmRequested -= HandleConfirmRequested;
            view.CancelRequested -= HandleCancelRequested;
        }

        StageInputModalGate.BlockedStateChanged -= HandleModalBlockedChanged;

        // 씬 전환이나 비활성화가 정적 모달 잠금을 남기지 않게 팝업과 Lease를 함께 정리한다.
        CloseConfirmPopup();
    }

    private void HandleReturnRequested()
    {
        // 시뮬레이션 진행 중이나 다른 모달 표시 중에는 버튼이 비활성이지만,
        // 이벤트 경로로 늦게 들어온 클릭도 같은 조건으로 한 번 더 거른다.
        if (isReturning || view.IsPopupVisible || isSimulationExecuting || StageInputModalGate.IsBlocked)
        {
            return;
        }

        // 팝업이 떠 있는 동안 배치 미리보기 취소와 월드 입력 차단이 함께 적용된다.
        inputBlockLease = StageInputModalGate.Acquire();
        view.SetPopupVisible(true);
    }

    private void HandleConfirmRequested()
    {
        if (isReturning || !view.IsPopupVisible)
        {
            return;
        }

        isReturning = true;

        // 복귀 전환(페이드·Stage 정리) 동안 월드 hover가 되살아나 파괴된 기물을 참조하지 않도록
        // Lease는 유지하고 팝업만 닫는다. Stage 씬 언로드 시 OnDisable의 CloseConfirmPopup이 해제하므로
        // 정적 게이트 잠금이 다음 세션까지 남지 않는다.
        view.SetPopupVisible(false);
        ApplyButtonState();

#if UNITY_EDITOR || REBELLION_DEMO_BUILD
        // 시연 빌드는 Title 로드 완료 시 세션 타이머가 초기화되도록 복귀 상태를 먼저 표시한다.
        DemoSessionController.NotifyPlayerReturnToTitleRequested();
#endif

        GameFlowManager.ReturnToTitleFromInGameButton();
    }

    private void HandleCancelRequested()
    {
        if (isReturning)
        {
            return;
        }

        CloseConfirmPopup();
    }

    private void HandleRunningStateChanged(bool isRunning)
    {
        isSimulationExecuting = isRunning;
        ApplyButtonState();
    }

    private void HandleSimulationFinished(SimulationController.SimulationResult _)
    {
        isSimulationExecuting = false;
        ApplyButtonState();
    }

    private void HandleSimulationReset()
    {
        isSimulationExecuting = false;
        ApplyButtonState();
    }

    private void HandleModalBlockedChanged(bool _)
    {
        ApplyButtonState();
    }

    private void CloseConfirmPopup()
    {
        view?.SetPopupVisible(false);
        inputBlockLease?.Dispose();
        inputBlockLease = null;
        ApplyButtonState();
    }

    private void ApplyButtonState()
    {
        // 자기 팝업이 잡은 잠금도 IsBlocked에 포함되므로 팝업 표시 중에는 뒤쪽 버튼이 disabled로 보인다.
        view?.SetReturnButtonInteractable(
            !isReturning && !isSimulationExecuting && !StageInputModalGate.IsBlocked);
    }

    private void EnsureBindings()
    {
        if (simulationController == null)
        {
            simulationController = SimulationController.Instance;
        }
    }
}
