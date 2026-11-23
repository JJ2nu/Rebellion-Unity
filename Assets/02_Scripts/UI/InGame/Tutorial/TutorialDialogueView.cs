using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 위에 겹쳐 보이는 미연시형 튜토리얼 대사창 View.
/// ResultDialogPanel의 캐릭터·대사창 연출을 축소 배치해 보드가 가려지지 않게 한다.
/// 진행 상태와 입력 해석은 GuidedTutorialDirector가 결정하고, 이 View는 표시와 클릭 전달만 담당한다.
/// </summary>
public sealed class TutorialDialogueView : MonoBehaviour
{
    private const string ElizaSpeakerName = "부관 엘리자";
    private const string ElizaSpeakerRichText = "<color=#dd9f7b>부관</color> 엘리자";
    private const string PlayerSpeakerName = "당신";
    private const string PlayerSpeakerRichText = "<color=#dd9f7b>당신</color>";

    // 문장 끝 부호(. ? ! …) 뒤 공백을 줄바꿈으로 바꿔 한 문장이 한 줄에서 시작하게 한다.
    private static readonly Regex SentenceBreak = new(@"(?<=[.?!…])[ \t]+(?=\S)", RegexOptions.Compiled);

    [Header("Bindings")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject characterRoot;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button textboxButton;
    // 대사를 넘길 수 있을 때만 보이는 진행 표시(▶) 버튼이다. 행동 대기 안내 중에는 숨긴다.
    [SerializeField] private Button advanceButton;

    [Header("Layout")]
    // 대사창이 대원 슬롯 앞에 있을 때(일반 대사)는 넓게, 슬롯 뒤로 내려갈 때(슬롯 조작 안내)는 슬롯을 피해 좁게 쓴다.
    [SerializeField, Min(100f)] private float fullTextWidth = 1480f;
    [SerializeField, Min(100f)] private float compactTextWidth = 900f;
    // 켜면 대사 데이터에 줄바꿈이 없어도 문장이 끝날 때마다 다음 줄로 넘긴다.
    [SerializeField] private bool breakAfterSentence = true;

    [Header("Typing")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 48f;

    [Header("Audio")]
    [SerializeField] private AudioSource advanceAudioSource;
    [SerializeField] private AudioClip advanceClip;

    private string currentCharacterState;
    private float typingStartTime;
    private int totalCharacters;
    private bool isVisible;

    public event Action AdvanceRequested;

    public bool IsTyping { get; private set; }

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        SetVisible(false);
    }

    private void OnEnable()
    {
        textboxButton?.onClick.AddListener(RequestAdvance);
        advanceButton?.onClick.AddListener(RequestAdvance);
    }

    private void OnDisable()
    {
        textboxButton?.onClick.RemoveListener(RequestAdvance);
        advanceButton?.onClick.RemoveListener(RequestAdvance);
    }

    private void Update()
    {
        if (!IsTyping || dialogueText == null)
        {
            return;
        }

        int visible = Mathf.FloorToInt((Time.unscaledTime - typingStartTime) * charactersPerSecond);
        if (visible >= totalCharacters)
        {
            CompleteTyping();
            return;
        }

        dialogueText.maxVisibleCharacters = visible;
    }

    /// <summary>
    /// 대사 한 줄을 표시한다. canAdvance가 false면 진행 표시를 숨겨 플레이어 행동을 기다리는 안내로 보이게 한다.
    /// </summary>
    public void ShowLine(string speaker, string text, string characterState, bool canAdvance)
    {
        SetVisible(true);

        if (speakerText != null)
        {
            speakerText.text = speaker switch
            {
                ElizaSpeakerName => ElizaSpeakerRichText,
                PlayerSpeakerName => PlayerSpeakerRichText,
                _ => speaker ?? string.Empty,
            };
        }

        bool showCharacter = !string.IsNullOrWhiteSpace(characterState);
        characterRoot?.SetActive(showCharacter);
        if (showCharacter && characterAnimator != null && characterState != currentCharacterState)
        {
            characterAnimator.Play(characterState, 0, 0f);
        }

        currentCharacterState = showCharacter ? characterState : null;

        if (advanceButton != null)
        {
            advanceButton.gameObject.SetActive(canAdvance);
        }

        if (textboxButton != null)
        {
            // 행동 대기 안내 중에는 대사창이 클릭을 먹지 않게 해 뒤쪽 UI·보드 조작을 방해하지 않는다.
            textboxButton.interactable = canAdvance;
            if (textboxButton.targetGraphic != null)
            {
                textboxButton.targetGraphic.raycastTarget = canAdvance;
            }
        }

        BeginTyping(FormatText(text));
    }

    /// <summary>
    /// 대사 글자 영역 너비를 바꾼다. 줄바꿈이 새 너비로 다시 계산되도록 현재 문장을 다시 배치한다.
    /// </summary>
    public void SetCompact(bool compact)
    {
        if (dialogueText == null)
        {
            return;
        }

        RectTransform textRect = dialogueText.rectTransform;
        float width = compact ? compactTextWidth : fullTextWidth;
        if (Mathf.Approximately(textRect.sizeDelta.x, width))
        {
            return;
        }

        textRect.sizeDelta = new Vector2(width, textRect.sizeDelta.y);
        dialogueText.ForceMeshUpdate();
        totalCharacters = dialogueText.textInfo.characterCount;
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }

        if (!visible)
        {
            currentCharacterState = null;
            IsTyping = false;
        }
    }

    /// <summary>결과 판정 패널처럼 다른 대사 UI가 위에 뜰 때 내용은 유지한 채 잠시 숨긴다.</summary>
    public void SetSuppressed(bool suppressed)
    {
        if (canvasGroup == null || !isVisible)
        {
            return;
        }

        canvasGroup.alpha = suppressed ? 0f : 1f;
        canvasGroup.blocksRaycasts = !suppressed;
    }

    public void CompleteTyping()
    {
        IsTyping = false;
        if (dialogueText != null)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
        }
    }

    public void PlayAdvanceSound()
    {
        if (advanceAudioSource != null && advanceClip != null)
        {
            advanceAudioSource.PlayOneShot(advanceClip);
        }
    }

    private string FormatText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return breakAfterSentence ? SentenceBreak.Replace(text, "\n") : text;
    }

    private void BeginTyping(string text)
    {
        if (dialogueText == null)
        {
            return;
        }

        dialogueText.text = text;
        dialogueText.ForceMeshUpdate();
        totalCharacters = dialogueText.textInfo.characterCount;
        typingStartTime = Time.unscaledTime;
        IsTyping = totalCharacters > 0;
        dialogueText.maxVisibleCharacters = IsTyping ? 0 : int.MaxValue;
    }

    private void RequestAdvance()
    {
        AdvanceRequested?.Invoke();
    }
}
