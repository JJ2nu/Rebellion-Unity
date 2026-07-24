using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 시뮬레이션 실행 상태와 보관된 결과를 Simulation Controls View 상태로 변환한다.
/// 결과 확정 전까지 배치 모드를 잠그고, 완전 승리 외 결과는 판정 다이얼로그를 거쳐 기존 확정/Retry 흐름으로 보낸다.
/// </summary>
[RequireComponent(typeof(StageSimulationControlsView))]
public sealed class StageSimulationControls : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private SimulationController simulationController;
    [SerializeField] private StageSceneFlowBinder stageSceneFlowBinder;
    [SerializeField] private ResultDialogController resultDialogController;

    [Header("Confirm Override")]
    [SerializeField] private bool useConfirmOverride;
    [SerializeField] private UnityEvent confirmOverride;

    private StageSimulationControlsView view;

    private void Awake()
    {
        view = GetComponent<StageSimulationControlsView>();
        if (view == null)
        {
            Debug.LogWarning($"{nameof(StageSimulationControls)} has no Simulation Controls view assigned.", this);
        }

        EnsureBindings();
        MatchConfirmButtonToPlayButton();
        ApplyState();
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
        }

        if (StageManager.Instance != null)
        {
            StageManager.Instance.RetryResetStateChanged -= HandleRetryResetStateChanged;
            StageManager.Instance.RetryResetStateChanged += HandleRetryResetStateChanged;
        }

        SubscribeViewEvents();
        ApplyState();
    }

    private void OnDisable()
    {
        if (simulationController != null)
        {
            simulationController.RunningStateChanged -= HandleRunningStateChanged;
            simulationController.SimulationFinished -= HandleSimulationFinished;
        }

        if (StageManager.Instance != null)
        {
            StageManager.Instance.RetryResetStateChanged -= HandleRetryResetStateChanged;
        }

        UnsubscribeViewEvents();
    }

    public void StartSimulation()
    {
        EnsureBindings();

        if (IsSimulationRunningOrPendingResult())
        {
            ApplyState();
            return;
        }

        simulationController?.StartSimulation();
        ApplyState();
    }

    public void RetrySimulation()
    {
        EnsureBindings();
        stageSceneFlowBinder?.ClearPendingSimulationResult();
        simulationController?.RetrySimulation();
        ApplyState();
    }

    public void ConfirmSimulation()
    {
        EnsureBindings();

        if (useConfirmOverride)
        {
            confirmOverride?.Invoke();
            return;
        }

        if (stageSceneFlowBinder == null ||
            !stageSceneFlowBinder.TryGetPendingSimulationResult(out SimulationController.SimulationResult result))
        {
            Debug.LogWarning("[StageSimulationControls] Confirm requested before a simulation result is ready.", this);
            ApplyState();
            return;
        }

        // 완전 승리는 판정 패널을 생략하고, 나머지 결과만 플레이어 선택을 위해 패널에 전달한다.
        if (result == SimulationController.SimulationResult.PerfectWin)
        {
            stageSceneFlowBinder.ConfirmSimulationResult();
            ApplyState();
            return;
        }

        if (resultDialogController == null)
        {
            Debug.LogWarning("[StageSimulationControls] ResultDialogController is not assigned.", this);
            return;
        }

        if (!resultDialogController.IsVisible)
        {
            string stageId = StageManager.Instance != null ? StageManager.Instance.CurrentStageId : string.Empty;
            resultDialogController.Show(stageId, result);
        }

        ApplyState();
    }

    public void MatchConfirmButtonToPlayButton()
    {
        view?.MatchConfirmButtonToPlayButton();
    }

    private void HandleRunningStateChanged(bool _)
    {
        ApplyState();
    }

    private void HandleSimulationFinished(SimulationController.SimulationResult _)
    {
        EnsureBindings();

        // Binder의 이벤트 구독 순서와 관계없이 버튼 갱신 전에 pending 결과가 존재하도록 보장한다.
        stageSceneFlowBinder?.StoreSimulationResult(_);
        ApplyState();
    }

    private void HandleRetryResetStateChanged(bool _)
    {
        ApplyState();
    }

    private void ApplyState()
    {
        EnsureBindings();

        // 실행 중에는 Play를 비활성 스프라이트로 남기고, 결과가 생긴 뒤에만 Retry/Confirm으로 교체한다.
        bool isSimulationMode = simulationController != null && simulationController._isRunning;
        bool isRetryResetting = StageManager.Instance != null && StageManager.Instance.IsRetryResetting;
        bool hasSimulationResult = stageSceneFlowBinder != null && stageSceneFlowBinder.HasPendingSimulationResult;

        // ViewState에는 게임 상태가 아니라 View가 그대로 적용할 최종 표시 값만 담는다.
        StageSimulationControlsViewState state = new(
            isPlayVisible: !hasSimulationResult,
            isPlayInteractable: !isSimulationMode && !isRetryResetting && !hasSimulationResult,
            useInactivePlaySprite: isSimulationMode || isRetryResetting,
            areResultActionsVisible: hasSimulationResult);
        view?.Apply(state);
    }

    private void EnsureBindings()
    {
        if (simulationController == null)
        {
            simulationController = SimulationController.Instance;
        }

    }

    private bool IsSimulationRunningOrPendingResult()
    {
        bool isSimulationMode = simulationController != null && simulationController._isRunning;
        bool isRetryResetting = StageManager.Instance != null && StageManager.Instance.IsRetryResetting;
        bool hasSimulationResult = stageSceneFlowBinder != null && stageSceneFlowBinder.HasPendingSimulationResult;
        return isSimulationMode || isRetryResetting || hasSimulationResult;
    }

    private void SubscribeViewEvents()
    {
        if (view == null)
        {
            return;
        }

        view.PlayRequested -= StartSimulation;
        view.PlayRequested += StartSimulation;
        view.RetryRequested -= RetrySimulation;
        view.RetryRequested += RetrySimulation;
        view.ConfirmRequested -= ConfirmSimulation;
        view.ConfirmRequested += ConfirmSimulation;
    }

    private void UnsubscribeViewEvents()
    {
        if (view == null)
        {
            return;
        }

        view.PlayRequested -= StartSimulation;
        view.RetryRequested -= RetrySimulation;
        view.ConfirmRequested -= ConfirmSimulation;
    }
}
