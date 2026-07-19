using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ResultDialog의 한 대사를 화면에 적용하는 불변 View 상태다.
/// </summary>
public readonly struct ResultDialogDialogueViewState
{
    public string SpeakerName { get; }
    public string DialogueText { get; }
    public string CharacterState { get; }

    public ResultDialogDialogueViewState(
        string speakerName,
        string dialogueText,
        string characterState)
    {
        SpeakerName = speakerName;
        DialogueText = dialogueText;
        CharacterState = characterState;
    }
}

/// <summary>
/// ResultDialog의 텍스트, 캐릭터, 선택 버튼 표현과 플레이어 입력 전달만 담당하는 Passive View다.
/// 결과 판정과 표시 단계 전이는 Controller가 결정한다.
/// </summary>
public sealed class ResultDialogView : MonoBehaviour
{
    private const string ElizaSpeakerName = "부관 엘리자";
    private const string ElizaSpeakerRichText = "<color=#dd9f7b>부관</color> 엘리자";
    private const string PlayerSpeakerName = "당신";
    private const string PlayerSpeakerRichText = "<color=#dd9f7b>당신</color>";

    [Header("Bindings")]
    [SerializeField] private GameObject characterRoot;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button textboxButton;
    [SerializeField] private Button advanceButton;
    [SerializeField] private Button confirmCommandButton;
    [SerializeField] private Button reconsiderButton;

    [Header("Audio")]
    [SerializeField] private AudioSource advanceAudioSource;
    [SerializeField] private AudioClip advanceClip;

    public event Action AdvanceRequested;
    public event Action ConfirmRequested;
    public event Action ReconsiderRequested;

    private void OnEnable()
    {
        textboxButton?.onClick.AddListener(RequestAdvance);
        advanceButton?.onClick.AddListener(RequestAdvance);
        confirmCommandButton?.onClick.AddListener(RequestConfirm);
        reconsiderButton?.onClick.AddListener(RequestReconsider);
    }

    private void OnDisable()
    {
        textboxButton?.onClick.RemoveListener(RequestAdvance);
        advanceButton?.onClick.RemoveListener(RequestAdvance);
        confirmCommandButton?.onClick.RemoveListener(RequestConfirm);
        reconsiderButton?.onClick.RemoveListener(RequestReconsider);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RequestAdvance();
        }
    }

    public void ApplyDialogue(ResultDialogDialogueViewState state)
    {
        if (speakerText != null)
        {
            speakerText.text = state.SpeakerName switch
            {
                ElizaSpeakerName => ElizaSpeakerRichText,
                PlayerSpeakerName => PlayerSpeakerRichText,
                _ => state.SpeakerName,
            };
        }

        if (dialogueText != null)
        {
            dialogueText.text = state.DialogueText;
        }

        bool showCharacter = !string.IsNullOrWhiteSpace(state.CharacterState);
        characterRoot?.SetActive(showCharacter);

        if (showCharacter && characterAnimator != null)
        {
            characterAnimator.Play(state.CharacterState, 0, 0f);
        }
    }

    public void SetChoiceButtonsVisible(bool showConfirm, bool showReconsider)
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

    public void PlayAdvanceSound()
    {
        if (advanceAudioSource != null && advanceClip != null)
        {
            advanceAudioSource.PlayOneShot(advanceClip);
        }
    }

    private void RequestAdvance()
    {
        AdvanceRequested?.Invoke();
    }

    private void RequestConfirm()
    {
        ConfirmRequested?.Invoke();
    }

    private void RequestReconsider()
    {
        ReconsiderRequested?.Invoke();
    }
}
