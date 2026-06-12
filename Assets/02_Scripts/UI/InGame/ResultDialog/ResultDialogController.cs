using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 시뮬레이션 결과 대사를 표시하고 Dialogue에서 Choices로 이어지는 판정 패널 상태를 관리한다.
/// 최종 결과 확정과 Retry 처리는 직접 구현하지 않고 Stage의 기존 흐름에 위임한다.
/// </summary>
public sealed class ResultDialogController : MonoBehaviour
{
    private const string ElizaSpeakerName = "부관 엘리자";
    private const string ElizaSpeakerRichText = "<color=#dd9f7b>부관</color> 엘리자";
    private const string PlayerSpeakerName = "당신";
    private const string PlayerSpeakerRichText = "<color=#dd9f7b>당신</color>";

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

    [Header("Audio")]
    [SerializeField] private AudioSource advanceAudioSource;
    [SerializeField] private AudioClip advanceClip;

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
        // Textbox와 별도 진행 버튼이 같은 프레임에 눌려도 한 번만 Choices로 전환한다.
        if (displayState != DisplayState.Dialogue || lastAdvanceFrame == Time.frameCount)
        {
            return;
        }

        lastAdvanceFrame = Time.frameCount;
        PlayAdvanceSound();
        displayState = DisplayState.Choices;

        // 실패 결과는 재고만 허용하고, 불완전 승리만 현재 명령을 확정할 수 있다.
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

    private void PlayAdvanceSound()
    {
        if (advanceAudioSource == null || advanceClip == null)
        {
            return;
        }

        advanceAudioSource.PlayOneShot(advanceClip);
    }
}
