using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

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

    // UI Submit과 분리된 배치 모드 Spacebar Play 전용 Action이다 (StageTutorialController의 Space 선례와 동일한 방식).
    private InputAction spacebarPlayAction;

    // Spacebar를 누른 채 유지 중인지 추적해, 릴리스 실행과 모달 개입 시 pressed 표시 취소를 판단한다.
    private bool isSpacebarPlayHeld;

    // 모달(튜토리얼 등)을 닫은 바로 그 Space 입력이 콜백 순서에 따라 Play로 이어지지 않도록 해제 프레임을 기록한다.
    private int lastModalUnblockFrame = -1;

    private void Awake()
    {
        view = GetComponent<StageSimulationControlsView>();
        if (view == null)
        {
            Debug.LogWarning($"{nameof(StageSimulationControls)} has no Simulation Controls view assigned.", this);
        }

        spacebarPlayAction = new InputAction(
            "StartSimulationBySpacebar",
            InputActionType.Button,
            "<Keyboard>/space");

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

        SubscribeViewEvents();

        // 눌림(pressed 표시)과 릴리스(실행)를 나눠 마우스 클릭과 같은 press-release 감각을 만든다.
        spacebarPlayAction.performed += HandleSpacebarPlayPressed;
        spacebarPlayAction.canceled += HandleSpacebarPlayReleased;
        spacebarPlayAction.Enable();

        // Spacebar를 누른 채 모달(튜토리얼·타이틀 복귀 팝업 등)이 열리면 pressed 표시를 취소하기 위해 구독한다.
        StageInputModalGate.BlockedStateChanged += HandleModalBlockedChanged;

        ApplyState();
    }

    private void OnDisable()
    {
        if (simulationController != null)
        {
            simulationController.RunningStateChanged -= HandleRunningStateChanged;
            simulationController.SimulationFinished -= HandleSimulationFinished;
        }

        if (spacebarPlayAction != null)
        {
            spacebarPlayAction.performed -= HandleSpacebarPlayPressed;
            spacebarPlayAction.canceled -= HandleSpacebarPlayReleased;
            spacebarPlayAction.Disable();
        }

        StageInputModalGate.BlockedStateChanged -= HandleModalBlockedChanged;
        isSpacebarPlayHeld = false;

        UnsubscribeViewEvents();
    }

    private void OnDestroy()
    {
        spacebarPlayAction?.Dispose();
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

    private void HandleSpacebarPlayPressed(InputAction.CallbackContext _)
    {
        // 모달 잠금 중, 모달이 해제된 그 프레임, 시뮬레이션 실행 중, 결과 대기 중에는 Spacebar Play를 받지 않는다.
        if (StageInputModalGate.IsBlocked ||
            lastModalUnblockFrame == Time.frameCount ||
            IsSimulationRunningOrPendingResult())
        {
            return;
        }

        // 실행은 릴리스에서 하고, 누르고 있는 동안은 어떤 버튼이 실행될지 pressed Sprite로 보여준다.
        isSpacebarPlayHeld = true;
        view?.ShowPlayPressedSprite();
    }

    private void HandleSpacebarPlayReleased(InputAction.CallbackContext _)
    {
        if (!isSpacebarPlayHeld)
        {
            return;
        }

        isSpacebarPlayHeld = false;

        // pressed Sprite를 현재 상태 기준 표시로 되돌린 뒤 시작한다.
        ApplyState();

        // 누르고 있는 사이 상태가 바뀌었을 수 있어 릴리스 시점에 다시 확인한다.
        if (StageInputModalGate.IsBlocked || IsSimulationRunningOrPendingResult())
        {
            return;
        }

        StartSimulation();
    }

    private void HandleModalBlockedChanged(bool isBlocked)
    {
        // Spacebar를 누른 채 모달이 열리면 릴리스해도 실행되지 않으므로 pressed 표시를 먼저 되돌린다.
        if (isBlocked && isSpacebarPlayHeld)
        {
            isSpacebarPlayHeld = false;
            ApplyState();
        }

        // 해제 프레임을 기록해 모달을 닫은 Space 입력이 같은 프레임에 Play로 새는 것을 막는다.
        if (!isBlocked)
        {
            lastModalUnblockFrame = Time.frameCount;
        }
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

    private void ApplyState()
    {
        EnsureBindings();

        // 실행 중에는 Play를 숨겨 연출 스킵 버튼이 같은 우상단 영역을 사용하고, 결과가 생긴 뒤 Retry/Confirm으로 교체한다.
        // Retry 되감기 중에도 Play는 즉시 활성 상태다. SpriteSwap disabled 자동 표시 때문에
        // 비상호작용으로 두면 되감기 0.45초 동안 Deact가 보이고, 클릭은 CompleteRetryResetImmediately가 안전하게 처리한다.
        bool isSimulationMode = simulationController != null && simulationController._isRunning;
        bool hasSimulationResult = stageSceneFlowBinder != null && stageSceneFlowBinder.HasPendingSimulationResult;

        // ViewState에는 게임 상태가 아니라 View가 그대로 적용할 최종 표시 값만 담는다.
        StageSimulationControlsViewState state = new(
            isPlayVisible: !hasSimulationResult && !isSimulationMode,
            isPlayInteractable: !isSimulationMode && !hasSimulationResult,
            useInactivePlaySprite: isSimulationMode,
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
        // 되감기 중 시작 요청은 막지 않는다. SimulationController.StartSimulation이
        // CompleteRetryResetImmediately로 되감기를 즉시 완료한 뒤 시작한다.
        bool isSimulationMode = simulationController != null && simulationController._isRunning;
        bool hasSimulationResult = stageSceneFlowBinder != null && stageSceneFlowBinder.HasPendingSimulationResult;
        return isSimulationMode || hasSimulationResult;
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
