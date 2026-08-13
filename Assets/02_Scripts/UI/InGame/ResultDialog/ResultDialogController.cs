using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 시뮬레이션 결과 대사를 표시하고 Dialogue에서 Choices로 이어지는 판정 패널 상태를 관리한다.
/// 최종 결과 확정과 Retry 처리는 직접 구현하지 않고 Stage의 기존 흐름에 위임한다.
/// </summary>
[RequireComponent(typeof(ResultDialogView))]
public sealed class ResultDialogController : MonoBehaviour
{
    // 상태를 명시해 Spacebar와 버튼 입력이 서로 다른 단계의 동작을 중복 실행하지 않게 한다.
    private enum DisplayState
    {
        Hidden,
        Dialogue,
        Choices,
    }

    // 선택지 키보드 하이라이트 대상이다. None이면 아직 아무 선택지도 선택되지 않았다.
    private enum ChoiceKeyboardFocus
    {
        None,
        Confirm,
        Reconsider,
    }

    [Header("Data")]
    [SerializeField] private TextAsset resultDialogueCsv;

    [Header("Bindings")]
    [SerializeField] private ResultDialogView view;
    [SerializeField] private StageSimulationControls stageSimulationControls;
    [SerializeField] private StageSceneFlowBinder stageSceneFlowBinder;

    private ResultDialogueDataTable dataTable;
    private TextAsset cachedCsv;
    private DisplayState displayState = DisplayState.Hidden;
    private SimulationController.SimulationResult currentResult;
    private int lastAdvanceFrame = -1;

    // UI Submit과 분리된 선택지 키보드 조작 전용 Action이다. 다이얼로그가 표시된 동안(OnEnable~OnDisable)에만 살아 있다.
    // 대사(Dialogue) 단계의 Spacebar 진행은 기존 View의 검사 경로가 담당하고, 이 Action은 Choices 단계만 처리한다.
    private InputAction choiceSpacebarAction;
    private InputAction choiceFocusUpAction;
    private InputAction choiceFocusDownAction;

    // 현재 키보드 하이라이트가 어느 선택지 위에 있는지와, 확정 Spacebar를 누르고 있는지 추적한다.
    private ChoiceKeyboardFocus choiceKeyboardFocus = ChoiceKeyboardFocus.None;
    private bool isChoiceSpacebarHeld;

    // Choices 진입 시점의 확정 가능 여부다. 위 방향키·첫 Spacebar의 하이라이트 대상 결정에 쓴다.
    private bool canConfirmChoice;

    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        if (view == null)
        {
            Debug.LogWarning($"{nameof(ResultDialogController)} has no ResultDialog view assigned.", this);
        }

        choiceSpacebarAction = new InputAction(
            "ResultDialogChoiceSpacebar",
            InputActionType.Button,
            "<Keyboard>/space");
        choiceFocusUpAction = new InputAction(
            "ResultDialogChoiceFocusUp",
            InputActionType.Button,
            "<Keyboard>/upArrow");
        choiceFocusDownAction = new InputAction(
            "ResultDialogChoiceFocusDown",
            InputActionType.Button,
            "<Keyboard>/downArrow");
    }

    private void OnEnable()
    {
        WorldInputRaycaster.Instance?.SetInputBlocked(true);
        SubscribeViewEvents();

        choiceSpacebarAction.performed += HandleChoiceSpacebarPressed;
        choiceSpacebarAction.canceled += HandleChoiceSpacebarReleased;
        choiceSpacebarAction.Enable();
        choiceFocusUpAction.performed += HandleChoiceFocusUp;
        choiceFocusUpAction.Enable();
        choiceFocusDownAction.performed += HandleChoiceFocusDown;
        choiceFocusDownAction.Enable();
    }

    private void OnDisable()
    {
        WorldInputRaycaster.Instance?.SetInputBlocked(false);
        UnsubscribeViewEvents();

        if (choiceSpacebarAction != null)
        {
            choiceSpacebarAction.performed -= HandleChoiceSpacebarPressed;
            choiceSpacebarAction.canceled -= HandleChoiceSpacebarReleased;
            choiceSpacebarAction.Disable();
        }

        if (choiceFocusUpAction != null)
        {
            choiceFocusUpAction.performed -= HandleChoiceFocusUp;
            choiceFocusUpAction.Disable();
        }

        if (choiceFocusDownAction != null)
        {
            choiceFocusDownAction.performed -= HandleChoiceFocusDown;
            choiceFocusDownAction.Disable();
        }

        choiceKeyboardFocus = ChoiceKeyboardFocus.None;
        isChoiceSpacebarHeld = false;
    }

    private void OnDestroy()
    {
        choiceSpacebarAction?.Dispose();
        choiceFocusUpAction?.Dispose();
        choiceFocusDownAction?.Dispose();
    }

    public bool Show(string stageId, SimulationController.SimulationResult result)
    {
        if (view == null)
        {
            Debug.LogError("[ResultDialogController] ResultDialogView is not assigned.", this);
            return false;
        }

        EnsureDataTable();

        if (dataTable == null || !dataTable.TryGetLine(stageId, result, out ResultDialogueLineData line))
        {
            return false;
        }

        currentResult = result;
        displayState = DisplayState.Dialogue;
        lastAdvanceFrame = -1;
        ResetChoiceKeyboardState();

        gameObject.SetActive(true);
        ApplyLine(line);
        view?.SetChoiceButtonsVisible(false, false);
        return true;
    }

    public void AdvanceDialogue()
    {
        // Textbox와 별도 진행 버튼이 같은 프레임에 눌려도 한 번만 Choices로 전환한다.
        if (displayState != DisplayState.Dialogue || lastAdvanceFrame == Time.frameCount)
        {
            return;
        }

        lastAdvanceFrame = Time.frameCount;
        view?.PlayAdvanceSound();
        displayState = DisplayState.Choices;

        // 실패 결과는 재고만 허용하고, 불완전 승리만 현재 명령을 확정할 수 있다.
        canConfirmChoice = currentResult == SimulationController.SimulationResult.AllyDeadWin ||
                           currentResult == SimulationController.SimulationResult.CivilianDeadWin ||
                           currentResult == SimulationController.SimulationResult.BothDeadWin;

        // 선택지는 하이라이트 없는 상태로 시작한다. 첫 키보드 입력은 하이라이트만 표시해 연타 오확정을 막는다.
        ResetChoiceKeyboardState();
        view?.SetChoiceButtonsVisible(canConfirmChoice, true);

        // 직전 마우스 클릭(Textbox 등)으로 남은 EventSystem 선택을 비워, ↑/↓가 UI Navigate로
        // 다른 버튼에 selected 표시를 입히거나 Enter Submit이 중복 실행되지 않게 한다 (StageTutorialController 선례).
        EventSystem.current?.SetSelectedGameObject(null);
    }

    public void ConfirmCommand()
    {
        if (displayState != DisplayState.Choices ||
            currentResult == SimulationController.SimulationResult.Lose ||
            currentResult == SimulationController.SimulationResult.AllyDeadLose)
        {
            return;
        }

        Hide();

        // pending 결과를 지우고 Scene 흐름을 진행하는 책임은 StageSceneFlowBinder에 유지한다.
        stageSceneFlowBinder?.ConfirmSimulationResult();
    }

    public void Reconsider()
    {
        if (displayState != DisplayState.Choices)
        {
            return;
        }

        Hide();

        // 판정 패널의 재고 버튼도 Stage의 기존 Retry 복원 경로를 그대로 사용한다.
        stageSimulationControls?.RetrySimulation();
    }

    private void Hide()
    {
        displayState = DisplayState.Hidden;
        ResetChoiceKeyboardState();
        view?.SetChoiceButtonsVisible(false, false);
        gameObject.SetActive(false);
    }

    private void HandleChoiceSpacebarPressed(InputAction.CallbackContext _)
    {
        // Choices 단계가 아니면 받지 않는다. 대사 단계의 Spacebar는 기존 View 검사 경로가 이 콜백 이후(Update)에 처리하므로
        // 대사를 넘긴 그 Space 입력이 여기서 하이라이트로 새지 않는다. 모달(타이틀 복귀 팝업 등) 잠금 중에도 받지 않는다.
        if (displayState != DisplayState.Choices || StageInputModalGate.IsBlocked)
        {
            return;
        }

        if (choiceKeyboardFocus == ChoiceKeyboardFocus.None)
        {
            // 첫 Spacebar는 실행이 아니라 위 선택지(확정 불가 결과면 유일한 재고 선택지) 하이라이트만 표시한다.
            SetChoiceKeyboardFocus(canConfirmChoice ? ChoiceKeyboardFocus.Confirm : ChoiceKeyboardFocus.Reconsider);
            return;
        }

        // 하이라이트 상태의 Spacebar는 확정 입력이다. pressed 색과 클릭 SFX를 마우스 PointerDown처럼
        // 누르는 순간 표시·재생하고, 실행은 릴리스에서 한다.
        isChoiceSpacebarHeld = true;
        ApplyChoiceKeyboardVisuals(isPressed: true);
        view?.PlayChoiceKeyboardClickSfx(choiceKeyboardFocus == ChoiceKeyboardFocus.Confirm);
    }

    private void HandleChoiceSpacebarReleased(InputAction.CallbackContext _)
    {
        if (!isChoiceSpacebarHeld)
        {
            return;
        }

        isChoiceSpacebarHeld = false;

        if (displayState != DisplayState.Choices || choiceKeyboardFocus == ChoiceKeyboardFocus.None)
        {
            return;
        }

        // 기존 버튼 클릭과 같은 공개 경로로 실행한다. 두 메서드 모두 Hide를 거치며 키보드 상태도 함께 초기화된다.
        if (choiceKeyboardFocus == ChoiceKeyboardFocus.Confirm)
        {
            ConfirmCommand();
        }
        else
        {
            Reconsider();
        }
    }

    private void HandleChoiceFocusUp(InputAction.CallbackContext _)
    {
        // 위 선택지는 확정 가능 결과에서만 확정 버튼이고, 재고만 있는 결과에서는 유일한 재고 버튼이다.
        MoveChoiceKeyboardFocus(canConfirmChoice ? ChoiceKeyboardFocus.Confirm : ChoiceKeyboardFocus.Reconsider);
    }

    private void HandleChoiceFocusDown(InputAction.CallbackContext _)
    {
        MoveChoiceKeyboardFocus(ChoiceKeyboardFocus.Reconsider);
    }

    private void MoveChoiceKeyboardFocus(ChoiceKeyboardFocus target)
    {
        // 확정 Spacebar를 누르고 있는 동안에는 하이라이트를 옮기지 않아 실행 대상이 눈에 보이는 것과 어긋나지 않게 한다.
        if (displayState != DisplayState.Choices ||
            StageInputModalGate.IsBlocked ||
            isChoiceSpacebarHeld ||
            choiceKeyboardFocus == target)
        {
            return;
        }

        SetChoiceKeyboardFocus(target);
    }

    private void SetChoiceKeyboardFocus(ChoiceKeyboardFocus focus)
    {
        choiceKeyboardFocus = focus;
        ApplyChoiceKeyboardVisuals(isPressed: false);

        if (focus != ChoiceKeyboardFocus.None)
        {
            // 키보드 하이라이트도 마우스 hover와 같은 소리를 내 어느 선택지가 선택됐는지 들리게 한다.
            view?.PlayChoiceKeyboardHoverSfx(focus == ChoiceKeyboardFocus.Confirm);
        }
    }

    // 현재 하이라이트 상태를 View의 hover/pressed 색 표시로 변환한다.
    private void ApplyChoiceKeyboardVisuals(bool isPressed)
    {
        ResultDialogChoiceKeyboardVisual focusVisual = isPressed
            ? ResultDialogChoiceKeyboardVisual.Pressed
            : ResultDialogChoiceKeyboardVisual.Hover;
        view?.ApplyChoiceKeyboardVisuals(
            choiceKeyboardFocus == ChoiceKeyboardFocus.Confirm ? focusVisual : ResultDialogChoiceKeyboardVisual.Normal,
            choiceKeyboardFocus == ChoiceKeyboardFocus.Reconsider ? focusVisual : ResultDialogChoiceKeyboardVisual.Normal);
    }

    private void ResetChoiceKeyboardState()
    {
        choiceKeyboardFocus = ChoiceKeyboardFocus.None;
        isChoiceSpacebarHeld = false;
        view?.ApplyChoiceKeyboardVisuals(
            ResultDialogChoiceKeyboardVisual.Normal,
            ResultDialogChoiceKeyboardVisual.Normal);
    }

    private void ApplyLine(ResultDialogueLineData line)
    {
        ResultDialogDialogueViewState state = new(
            line.SpeakerName,
            line.DialogueText,
            line.CharacterState);
        view?.ApplyDialogue(state);
    }

    private void EnsureDataTable()
    {
        if (dataTable != null && cachedCsv == resultDialogueCsv)
        {
            return;
        }

        cachedCsv = resultDialogueCsv;
        dataTable = ResultDialogueDataTable.FromCsv(resultDialogueCsv);
    }

    private void SubscribeViewEvents()
    {
        if (view == null)
        {
            return;
        }

        view.AdvanceRequested -= AdvanceDialogue;
        view.AdvanceRequested += AdvanceDialogue;
        view.ConfirmRequested -= ConfirmCommand;
        view.ConfirmRequested += ConfirmCommand;
        view.ReconsiderRequested -= Reconsider;
        view.ReconsiderRequested += Reconsider;
    }

    private void UnsubscribeViewEvents()
    {
        if (view == null)
        {
            return;
        }

        view.AdvanceRequested -= AdvanceDialogue;
        view.ConfirmRequested -= ConfirmCommand;
        view.ReconsiderRequested -= Reconsider;
    }
}
