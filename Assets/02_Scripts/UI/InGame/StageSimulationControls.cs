using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 시뮬레이션 실행 상태와 보관된 결과를 Simulation Controls View 상태로 변환한다.
/// 결과 확정 전까지 배치 모드를 잠그고, 완전 승리 외 결과는 판정 다이얼로그를 거쳐 기존 확정/Retry 흐름으로 보낸다.
/// </summary>
[RequireComponent(typeof(StageSimulationControlsView))]
public sealed class StageSimulationControls : MonoBehaviour
{
    // 결과 버튼(Retry/Confirm) 키보드 하이라이트 대상이다. None이면 아직 아무 버튼도 선택되지 않았다.
    private enum ResultKeyboardFocus
    {
        None,
        Retry,
        Confirm,
    }

    [Header("Bindings")]
    [SerializeField] private SimulationController simulationController;
    [SerializeField] private StageSceneFlowBinder stageSceneFlowBinder;
    [SerializeField] private ResultDialogController resultDialogController;

    [Header("Confirm Override")]
    [SerializeField] private bool useConfirmOverride;
    [SerializeField] private UnityEvent confirmOverride;

    private StageSimulationControlsView view;

    // UI Submit과 분리된 Spacebar 전용 Action이다 (StageTutorialController의 Space 선례와 동일한 방식).
    // 배치 모드에서는 Play, 결과 대기 중에는 Retry/Confirm 하이라이트·확정을 담당한다.
    private InputAction spacebarAction;

    // 결과 버튼 키보드 하이라이트 이동용 좌/우 방향키 Action이다.
    private InputAction resultFocusLeftAction;
    private InputAction resultFocusRightAction;

    // Spacebar를 누른 채 유지 중인지 추적해, 릴리스 실행과 모달 개입 시 pressed 표시 취소를 판단한다.
    private bool isSpacebarPlayHeld;

    // 결과 버튼 확정 Spacebar를 누른 채 유지 중인지 추적한다. 릴리스 시점에 하이라이트된 버튼을 실행한다.
    private bool isSpacebarConfirmHeld;

    // 현재 키보드 하이라이트가 어느 결과 버튼 위에 있는지 나타낸다.
    private ResultKeyboardFocus resultKeyboardFocus = ResultKeyboardFocus.None;

    // 모달(튜토리얼 등)을 닫은 바로 그 Space 입력이 콜백 순서에 따라 Play로 이어지지 않도록 해제 프레임을 기록한다.
    private int lastModalUnblockFrame = -1;

    private void Awake()
    {
        view = GetComponent<StageSimulationControlsView>();
        if (view == null)
        {
            Debug.LogWarning($"{nameof(StageSimulationControls)} has no Simulation Controls view assigned.", this);
        }

        spacebarAction = new InputAction(
            "SimulationControlsSpacebar",
            InputActionType.Button,
            "<Keyboard>/space");
        resultFocusLeftAction = new InputAction(
            "SimulationResultFocusLeft",
            InputActionType.Button,
            "<Keyboard>/leftArrow");
        resultFocusRightAction = new InputAction(
            "SimulationResultFocusRight",
            InputActionType.Button,
            "<Keyboard>/rightArrow");

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
        spacebarAction.performed += HandleSpacebarPressed;
        spacebarAction.canceled += HandleSpacebarReleased;
        spacebarAction.Enable();

        resultFocusLeftAction.performed += HandleResultFocusLeft;
        resultFocusLeftAction.Enable();
        resultFocusRightAction.performed += HandleResultFocusRight;
        resultFocusRightAction.Enable();

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

        if (spacebarAction != null)
        {
            spacebarAction.performed -= HandleSpacebarPressed;
            spacebarAction.canceled -= HandleSpacebarReleased;
            spacebarAction.Disable();
        }

        if (resultFocusLeftAction != null)
        {
            resultFocusLeftAction.performed -= HandleResultFocusLeft;
            resultFocusLeftAction.Disable();
        }

        if (resultFocusRightAction != null)
        {
            resultFocusRightAction.performed -= HandleResultFocusRight;
            resultFocusRightAction.Disable();
        }

        StageInputModalGate.BlockedStateChanged -= HandleModalBlockedChanged;
        isSpacebarPlayHeld = false;
        ResetResultKeyboardFocus();

        UnsubscribeViewEvents();
    }

    private void OnDestroy()
    {
        spacebarAction?.Dispose();
        resultFocusLeftAction?.Dispose();
        resultFocusRightAction?.Dispose();
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

    private void HandleSpacebarPressed(InputAction.CallbackContext _)
    {
        // 모달 잠금 중이거나 모달이 해제된 그 프레임에는 어떤 Spacebar 조작도 받지 않는다.
        if (StageInputModalGate.IsBlocked || lastModalUnblockFrame == Time.frameCount)
        {
            return;
        }

        // 결과 대기 중에는 Spacebar가 Retry/Confirm 키보드 조작으로 동작한다.
        if (IsResultKeyboardNavActive())
        {
            if (resultKeyboardFocus == ResultKeyboardFocus.None)
            {
                // 첫 Spacebar는 실행이 아니라 Retry 하이라이트만 표시해 연타 오확정을 막는다.
                SetResultKeyboardFocus(ResultKeyboardFocus.Retry);
                return;
            }

            // 하이라이트 상태의 Spacebar는 확정 입력이다. pressed Sprite와 클릭 SFX를 마우스 PointerDown처럼
            // 누르는 순간 표시·재생하고, 실행은 릴리스에서 한다.
            isSpacebarConfirmHeld = true;
            ApplyResultKeyboardFocusVisuals(isPressed: true);
            view?.PlayResultKeyboardClickSfx(resultKeyboardFocus == ResultKeyboardFocus.Retry);
            return;
        }

        // 배치 모드 Spacebar Play. 시뮬레이션 실행 중·결과 대기 중에는 받지 않는다.
        if (IsSimulationRunningOrPendingResult())
        {
            return;
        }

        // 실행은 릴리스에서 하고, 누르고 있는 동안은 어떤 버튼이 실행될지 pressed Sprite로 보여준다.
        isSpacebarPlayHeld = true;
        view?.ShowPlayPressedSprite();
    }

    private void HandleSpacebarReleased(InputAction.CallbackContext _)
    {
        // 결과 버튼 확정 릴리스: 하이라이트된 버튼을 기존 버튼 클릭과 같은 경로로 실행한다.
        if (isSpacebarConfirmHeld)
        {
            isSpacebarConfirmHeld = false;
            ResultKeyboardFocus executedFocus = resultKeyboardFocus;

            if (!IsResultKeyboardNavActive() || executedFocus == ResultKeyboardFocus.None)
            {
                return;
            }

            if (executedFocus == ResultKeyboardFocus.Retry)
            {
                RetrySimulation();
            }
            else
            {
                ConfirmSimulation();
            }

            return;
        }

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

    private void HandleResultFocusLeft(InputAction.CallbackContext _)
    {
        MoveResultKeyboardFocus(ResultKeyboardFocus.Retry);
    }

    private void HandleResultFocusRight(InputAction.CallbackContext _)
    {
        MoveResultKeyboardFocus(ResultKeyboardFocus.Confirm);
    }

    private void MoveResultKeyboardFocus(ResultKeyboardFocus target)
    {
        // 확정 Spacebar를 누르고 있는 동안에는 하이라이트를 옮기지 않아 실행 대상이 눈에 보이는 것과 어긋나지 않게 한다.
        if (StageInputModalGate.IsBlocked ||
            !IsResultKeyboardNavActive() ||
            isSpacebarConfirmHeld ||
            resultKeyboardFocus == target)
        {
            return;
        }

        SetResultKeyboardFocus(target);
    }

    private void SetResultKeyboardFocus(ResultKeyboardFocus focus)
    {
        resultKeyboardFocus = focus;
        ApplyResultKeyboardFocusVisuals(isPressed: false);

        if (focus != ResultKeyboardFocus.None)
        {
            // 키보드 하이라이트도 마우스 hover와 같은 소리를 내 어느 버튼이 선택됐는지 들리게 한다.
            view?.PlayResultKeyboardHoverSfx(focus == ResultKeyboardFocus.Retry);
        }
    }

    // 현재 하이라이트 상태를 View의 hover/pressed 표시로 변환한다.
    private void ApplyResultKeyboardFocusVisuals(bool isPressed)
    {
        SimulationResultButtonKeyboardVisual focusVisual = isPressed
            ? SimulationResultButtonKeyboardVisual.Pressed
            : SimulationResultButtonKeyboardVisual.Hover;
        view?.ApplyResultKeyboardVisuals(
            resultKeyboardFocus == ResultKeyboardFocus.Retry ? focusVisual : SimulationResultButtonKeyboardVisual.Normal,
            resultKeyboardFocus == ResultKeyboardFocus.Confirm ? focusVisual : SimulationResultButtonKeyboardVisual.Normal);
    }

    private void ResetResultKeyboardFocus()
    {
        if (resultKeyboardFocus == ResultKeyboardFocus.None && !isSpacebarConfirmHeld)
        {
            return;
        }

        resultKeyboardFocus = ResultKeyboardFocus.None;
        isSpacebarConfirmHeld = false;
        view?.ApplyResultKeyboardVisuals(
            SimulationResultButtonKeyboardVisual.Normal,
            SimulationResultButtonKeyboardVisual.Normal);
    }

    // 결과 버튼 키보드 조작이 유효한 상태인지 판정한다. 판정 다이얼로그가 열려 있으면
    // Spacebar는 다이얼로그 진행에 쓰여야 하므로 여기서는 받지 않는다.
    private bool IsResultKeyboardNavActive()
    {
        bool hasSimulationResult = stageSceneFlowBinder != null && stageSceneFlowBinder.HasPendingSimulationResult;
        bool isResultDialogVisible = resultDialogController != null && resultDialogController.IsVisible;
        return hasSimulationResult && !isResultDialogVisible;
    }

    private void HandleModalBlockedChanged(bool isBlocked)
    {
        // Spacebar를 누른 채 모달이 열리면 릴리스해도 실행되지 않으므로 pressed 표시를 먼저 되돌린다.
        if (isBlocked && isSpacebarPlayHeld)
        {
            isSpacebarPlayHeld = false;
            ApplyState();
        }

        // 결과 버튼 확정 중 모달이 개입해도 같은 이유로 pressed 표시를 취소한다. 하이라이트는 유지한다.
        if (isBlocked && isSpacebarConfirmHeld)
        {
            isSpacebarConfirmHeld = false;
            ApplyResultKeyboardFocusVisuals(isPressed: false);
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

        // 실행 중 클릭(스킵 버튼 등)으로 남은 EventSystem 선택을 비워, ←/→가 UI Navigate로
        // 다른 버튼에 selected 표시를 입히거나 Enter Submit이 중복 실행되지 않게 한다 (StageTutorialController 선례).
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void ApplyState()
    {
        EnsureBindings();

        // 실행 중에는 Play를 숨겨 연출 스킵 버튼이 같은 우상단 영역을 사용하고, 결과가 생긴 뒤 Retry/Confirm으로 교체한다.
        // Retry 되감기 중에도 Play는 즉시 활성 상태다. SpriteSwap disabled 자동 표시 때문에
        // 비상호작용으로 두면 되감기 0.45초 동안 Deact가 보이고, 클릭은 CompleteRetryResetImmediately가 안전하게 처리한다.
        bool isSimulationMode = simulationController != null && simulationController._isRunning;
        bool hasSimulationResult = stageSceneFlowBinder != null && stageSceneFlowBinder.HasPendingSimulationResult;

        // 결과 버튼이 사라지거나 판정 다이얼로그로 넘어가면 키보드 하이라이트를 초기화해,
        // 다음에 버튼이 다시 표시될 때 이전 hover/pressed Sprite가 남지 않게 한다.
        if (!IsResultKeyboardNavActive())
        {
            ResetResultKeyboardFocus();
        }

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
