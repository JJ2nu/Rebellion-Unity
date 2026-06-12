using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ResultDialogController : MonoBehaviour
{
    private const string ElizaSpeakerName = "부관 엘리자";
    private const string ElizaSpeakerRichText = "<color=#dd9f7b>부관</color> 엘리자";
    private const string PlayerSpeakerName = "당신";
    private const string PlayerSpeakerRichText = "<color=#dd9f7b>당신</color>";

    private enum DisplayState
    {
        Hidden,
        Dialogue,
        Choices,
    }

    [Header("Data")]
    [SerializeField] private TextAsset resultDialogueCsv;

    [Header("Bindings")]
    [SerializeField] private GameObject characterRoot;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button textboxButton;
    [SerializeField] private Button advanceButton;
    [SerializeField] private Button confirmCommandButton;
    [SerializeField] private Button reconsiderButton;
    [SerializeField] private StageSimulationControls stageSimulationControls;
    [SerializeField] private StageSceneFlowBinder stageSceneFlowBinder;

    private ResultDialogueDataTable dataTable;
    private TextAsset cachedCsv;
    private DisplayState displayState = DisplayState.Hidden;
    private SimulationController.SimulationResult currentResult;
    private int lastAdvanceFrame = -1;

    public bool IsVisible => gameObject.activeSelf;

    private void OnEnable()
    {
        textboxButton?.onClick.AddListener(AdvanceDialogue);
        advanceButton?.onClick.AddListener(AdvanceDialogue);
        confirmCommandButton?.onClick.AddListener(ConfirmCommand);
        reconsiderButton?.onClick.AddListener(Reconsider);
    }

    private void OnDisable()
    {
        textboxButton?.onClick.RemoveListener(AdvanceDialogue);
        advanceButton?.onClick.RemoveListener(AdvanceDialogue);
        confirmCommandButton?.onClick.RemoveListener(ConfirmCommand);
        reconsiderButton?.onClick.RemoveListener(Reconsider);
    }

    private void Update()
    {
        if (displayState == DisplayState.Dialogue &&
            Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            AdvanceDialogue();
        }
    }

    public bool Show(string stageId, SimulationController.SimulationResult result)
    {
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
        SetChoiceButtonsVisible(false, false);
        return true;
    }

    public void AdvanceDialogue()
    {
        if (displayState != DisplayState.Dialogue || lastAdvanceFrame == Time.frameCount)
        {
            return;
        }

        lastAdvanceFrame = Time.frameCount;
        displayState = DisplayState.Choices;

        bool canConfirm = currentResult == SimulationController.SimulationResult.AllyDeadWin ||
                          currentResult == SimulationController.SimulationResult.CivilianDeadWin ||
                          currentResult == SimulationController.SimulationResult.BothDeadWin;
        SetChoiceButtonsVisible(canConfirm, true);
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
        stageSceneFlowBinder?.ConfirmSimulationResult();
    }

    public void Reconsider()
    {
        if (displayState != DisplayState.Choices)
        {
            return;
        }

        Hide();
        stageSimulationControls?.RetrySimulation();
    }

    private void Hide()
    {
        displayState = DisplayState.Hidden;
        SetChoiceButtonsVisible(false, false);
        gameObject.SetActive(false);
    }

    private void ApplyLine(ResultDialogueLineData line)
    {
        if (speakerText != null)
        {
            speakerText.text = line.SpeakerName switch
            {
                ElizaSpeakerName => ElizaSpeakerRichText,
                PlayerSpeakerName => PlayerSpeakerRichText,
                _ => line.SpeakerName,
            };
        }

        if (dialogueText != null)
        {
            dialogueText.text = line.DialogueText;
        }

        bool showCharacter = !string.IsNullOrWhiteSpace(line.CharacterState);
        characterRoot?.SetActive(showCharacter);

        if (showCharacter && characterAnimator != null)
        {
            characterAnimator.Play(line.CharacterState, 0, 0f);
        }
    }

    private void SetChoiceButtonsVisible(bool showConfirm, bool showReconsider)
    {
        if (confirmCommandButton != null)
        {
            confirmCommandButton.gameObject.SetActive(showConfirm);
        }

        if (reconsiderButton != null)
        {
            reconsiderButton.gameObject.SetActive(showReconsider);
        }
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
}
