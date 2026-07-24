using UnityEngine;

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

    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        if (view == null)
        {
            Debug.LogWarning($"{nameof(ResultDialogController)} has no ResultDialog view assigned.", this);
        }
    }

    private void OnEnable()
    {
        WorldInputRaycaster.Instance?.SetInputBlocked(true);
        SubscribeViewEvents();
    }

    private void OnDisable()
    {
        WorldInputRaycaster.Instance?.SetInputBlocked(false);
        UnsubscribeViewEvents();
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
        bool canConfirm = currentResult == SimulationController.SimulationResult.AllyDeadWin ||
                          currentResult == SimulationController.SimulationResult.CivilianDeadWin ||
                          currentResult == SimulationController.SimulationResult.BothDeadWin;
        view?.SetChoiceButtonsVisible(canConfirm, true);
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
        view?.SetChoiceButtonsVisible(false, false);
        gameObject.SetActive(false);
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
