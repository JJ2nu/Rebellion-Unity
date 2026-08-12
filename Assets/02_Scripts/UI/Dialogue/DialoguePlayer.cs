using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

// CSV 대사를 순서대로 표시하고 화자에 맞는 캐릭터 연출과 다음 Stage 요청을 전달한다.
[ExecuteAlways]
public sealed class DialoguePlayer : MonoBehaviour
{
    #region Constants

    private const string PlayerSpeakerName = "\uB2F9\uC2E0";

    #endregion

    #region Fields

    [Header("Data")]
    [SerializeField] private TextAsset dialogueCsv;

    [Header("Preview")]
    [SerializeField] private string previewLevel = "stage_001";
    [SerializeField] private SimulationController.SimulationResult previewResult = SimulationController.SimulationResult.PerfectWin;
    [SerializeField] private bool playPreviewOnStart;
    [SerializeField] private bool previewInEditMode = true;

    [Header("Bindings")]
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private CanvasGroup characterCanvasGroup;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button textboxButton;
    [SerializeField] private bool autoBindChildren = true;

    [Header("Character")]
    [SerializeField, Range(0f, 1f)] private float playerSpeakerAlpha = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource advanceAudioSource;
    [SerializeField] private AudioClip advanceClip;

    [Header("Skip")]
    // 대사 재생 중에만 표시되는 우상단 >> skip 버튼. Scene onClick이 SkipDialogue()를 호출한다. (Task 61)
    [SerializeField] private Button skipButton;
    // ESC 스킵 입력. Task 46 시뮬레이션 스킵과 같은 UI/Cancel 액션을 연결하고, 대사 재생 중에만 구독한다.
    [SerializeField] private InputActionReference skipAction;

    [Header("Events")]
    [SerializeField] private UnityEvent nextStageRequestedEvent;

    private DialogueDataTable table;
    private TextAsset cachedDialogueCsv;
    private List<DialogueLineData> currentLines = new();
    private int currentIndex;
    private int lastAdvanceFrame = -1;
    // 다음 단계 요청 후 스킵 입력이 다시 요청을 만들지 않게 막는다. Play() 시작 시 해제된다.
    private bool hasRequestedNextStage;
    // 다른 시스템이 켠 Cancel 액션을 끄지 않도록 이 컴포넌트가 직접 켠 경우만 기억한다.
    private bool skipActionEnabledBySelf;

    #endregion

    #region Events

    public event Action NextStageRequested;

    #endregion

    #region Properties

    public string PreviewLevel => previewLevel;
    public SimulationController.SimulationResult PreviewResult => previewResult;
    public bool PreviewInEditMode => previewInEditMode;
    public int CurrentLineCount => currentLines?.Count ?? 0;
    public int CurrentIndex => currentIndex;

    #endregion

    #region Unity Events

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        if (playPreviewOnStart)
        {
            PlayPreviewDialogue();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            // Spacebar도 Textbox 클릭과 같은 진행 경로를 사용해 마지막 대사의 Scene 요청까지 일관되게 처리한다.
            AdvanceDialogue();
        }
    }

    private void OnDisable()
    {
        // ExecuteAlways라 편집 모드에서도 호출되지만 해제는 안전한 no-op이다.
        UnbindSkipAction();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || !previewInEditMode)
        {
            return;
        }

        EditorApplication.delayCall -= PlayPreviewDialogueInEditor;
        EditorApplication.delayCall += PlayPreviewDialogueInEditor;
#endif
    }

    #endregion

#if UNITY_EDITOR
    #region Editor Preview

    private void PlayPreviewDialogueInEditor()
    {
        if (this == null || Application.isPlaying || !previewInEditMode)
        {
            return;
        }

        PlayPreviewDialogue();
        EditorUtility.SetDirty(this);

        if (speakerText != null)
        {
            EditorUtility.SetDirty(speakerText);
        }

        if (dialogueText != null)
        {
            EditorUtility.SetDirty(dialogueText);
        }
    }

    #endregion
#endif

    #region Public Methods

    public void Play(string level, SimulationController.SimulationResult result)
    {
        EnsureInitialized();

        currentLines = table.GetLines(level, result);
        currentIndex = 0;
        hasRequestedNextStage = false;

        Debug.Log($"Dialogue preview started. Level: {level}, Result: {result}, Lines: {currentLines.Count}");

        if (currentLines.Count == 0)
        {
            ClearText();
            SetSkipAvailable(false);
            return;
        }

        SetSkipAvailable(true);
        ShowLine(currentLines[currentIndex]);
    }

    public void PlayPreviewDialogue()
    {
        Play(previewLevel, previewResult);
    }

    public void AdvanceDialogue()
    {
        // 같은 프레임에 클릭과 Spacebar가 함께 들어와도 대사가 두 줄 진행되지 않게 막는다.
        if (Application.isPlaying && lastAdvanceFrame == Time.frameCount)
        {
            return;
        }

        if (currentLines == null || currentLines.Count == 0)
        {
            Debug.LogWarning("Dialogue textbox clicked, but no dialogue is playing.");
            return;
        }

        lastAdvanceFrame = Application.isPlaying ? Time.frameCount : -1;

        PlayAdvanceSound();
        AdvanceCurrentLine();
    }

    /// <summary>
    /// 남은 대사 전체를 건너뛰고 마지막 대사를 끝까지 진행했을 때와 동일한 다음 단계 요청을 한 번만 발생시킨다.
    /// skip 버튼 onClick과 ESC(UI/Cancel) 입력이 공용으로 호출한다. (Task 61)
    /// </summary>
    public void SkipDialogue()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        // 재생 중인 대사가 없거나 이미 다음 단계를 요청했으면 스킵 입력을 무시한다.
        if (currentLines == null || currentLines.Count == 0 || hasRequestedNextStage)
        {
            return;
        }

        // 같은 프레임에 클릭·Spacebar 진행과 겹쳐도 이중 진행되지 않게 진행 프레임 가드를 공유한다.
        if (lastAdvanceFrame == Time.frameCount)
        {
            return;
        }

        lastAdvanceFrame = Time.frameCount;

        PlayAdvanceSound();
        RequestNextStage();
    }

    #endregion

    #region Preview

    [ContextMenu("Play Preview Dialogue")]
    private void PlayPreviewDialogueFromContextMenu()
    {
        PlayPreviewDialogue();
    }

    #endregion

    #region Initialization

    private void EnsureInitialized()
    {
        if (table == null || cachedDialogueCsv != dialogueCsv)
        {
            cachedDialogueCsv = dialogueCsv;
            table = DialogueDataTable.FromCsv(dialogueCsv);
        }

        if (autoBindChildren)
        {
            BindChildrenByName();
        }

        if (characterCanvasGroup == null && characterAnimator != null)
        {
            characterCanvasGroup = characterAnimator.GetComponent<CanvasGroup>();
        }
    }

    [ContextMenu("Rebind Dialogue Children")]
    private void BindChildrenByName()
    {
        Transform speaker = FindChildRecursive(transform, "Txt_Speaker");
        Transform dialogue = FindChildRecursive(transform, "Txt_Dialogue");
        Transform character = FindChildRecursive(transform, "Img_Char");
        Transform textboxImage = FindChildRecursive(transform, "Img_Textbox");

        if (speaker != null)
        {
            speakerText = speaker.GetComponent<TMP_Text>();
        }

        if (dialogue != null)
        {
            dialogueText = dialogue.GetComponent<TMP_Text>();
        }

        if (character != null)
        {
            characterAnimator = character.GetComponent<Animator>();
            characterCanvasGroup = character.GetComponent<CanvasGroup>();
        }

        if (textboxImage != null)
        {
            textboxButton = textboxImage.GetComponent<Button>();
        }

        ValidateBindings();
    }

    private void ValidateBindings()
    {
        if (speakerText == null)
        {
            Debug.LogError("Dialogue binding missing: Txt_Speaker TMP_Text.");
        }

        if (dialogueText == null)
        {
            Debug.LogError("Dialogue binding missing: Txt_Dialogue TMP_Text.");
        }

        if (characterAnimator == null)
        {
            Debug.LogError("Dialogue binding missing: Img_Char Animator.");
        }

        if (textboxButton == null)
        {
            Debug.LogError("Dialogue binding missing: Img_Textbox Button.");
        }
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);

            if (child.name == childName)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    #endregion

    #region Playback

    private void ShowLine(DialogueLineData line)
    {
        if (speakerText != null)
        {
            speakerText.SetText(line.SpeakerName);
            speakerText.ForceMeshUpdate();
        }
        else
        {
            Debug.LogError($"Cannot show SpeakerName. Txt_Speaker is not bound. SpeakerName: {line.SpeakerName}");
        }

        if (dialogueText != null)
        {
            dialogueText.SetText(line.DialogueText);
            dialogueText.ForceMeshUpdate();
        }
        else
        {
            Debug.LogError($"Cannot show DialogueText. Txt_Dialogue is not bound. DialogueText: {line.DialogueText}");
        }

        if (characterAnimator != null && !string.IsNullOrWhiteSpace(line.CharacterImage))
        {
            characterAnimator.Play(line.CharacterImage, 0, 0f);
        }

        ApplyCharacterAlpha(line);

        Debug.Log($"Dialogue line {currentIndex + 1}/{currentLines.Count}: {line.SpeakerName} / {line.NextAction}");
    }

    private void AdvanceCurrentLine()
    {
        DialogueLineData line = currentLines[currentIndex];

        if (line.NextAction == DialogueNextAction.NextStage)
        {
            RequestNextStage();
            return;
        }

        currentIndex++;

        if (currentIndex >= currentLines.Count)
        {
            RequestNextStage();
            return;
        }

        ShowLine(currentLines[currentIndex]);
    }

    private void RequestNextStage()
    {
        // 스킵과 일반 진행 어느 경로로 와도 요청 후에는 스킵 버튼·ESC 입력을 정리한다.
        hasRequestedNextStage = true;
        SetSkipAvailable(false);

        Debug.Log("Dialogue requested next stage.");
        nextStageRequestedEvent?.Invoke();
        NextStageRequested?.Invoke();
    }

    private void ClearText()
    {
        if (speakerText != null)
        {
            speakerText.text = string.Empty;
        }

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }
    }

    private void PlayAdvanceSound()
    {
        if (advanceAudioSource == null || advanceClip == null)
        {
            return;
        }

        advanceAudioSource.PlayOneShot(advanceClip);
    }

    #endregion

    #region Skip

    /// <summary>
    /// 스킵 버튼 표시와 ESC(UI/Cancel) 구독을 함께 전환한다.
    /// 대사가 재생 중일 때만 켜서 다른 화면 상태의 Cancel 입력과 겹치지 않게 한다.
    /// 편집 모드 미리보기에서는 Scene을 더럽히지 않도록 아무것도 하지 않는다.
    /// </summary>
    private void SetSkipAvailable(bool available)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (skipButton != null && skipButton.gameObject.activeSelf != available)
        {
            skipButton.gameObject.SetActive(available);
        }

        if (available)
        {
            BindSkipAction();
        }
        else
        {
            UnbindSkipAction();
        }
    }

    private void BindSkipAction()
    {
        if (skipAction == null || skipAction.action == null)
        {
            return;
        }

        InputAction action = skipAction.action;
        action.performed -= HandleSkipPerformed;
        action.performed += HandleSkipPerformed;

        if (!action.enabled)
        {
            action.Enable();
            skipActionEnabledBySelf = true;
        }
    }

    private void UnbindSkipAction()
    {
        if (skipAction == null || skipAction.action == null)
        {
            skipActionEnabledBySelf = false;
            return;
        }

        InputAction action = skipAction.action;
        action.performed -= HandleSkipPerformed;

        if (skipActionEnabledBySelf && action.enabled)
        {
            action.Disable();
        }

        skipActionEnabledBySelf = false;
    }

    private void HandleSkipPerformed(InputAction.CallbackContext _)
    {
        SkipDialogue();
    }

    #endregion

    #region Character

    private void ApplyCharacterAlpha(DialogueLineData line)
    {
        if (characterCanvasGroup == null)
        {
            return;
        }

        // CharacterImage가 빈 대사는 캐릭터가 등장하지 않는 연출이므로 완전히 숨긴다. (예: stage_008 엘리자 사망 분기)
        if (string.IsNullOrWhiteSpace(line.CharacterImage))
        {
            characterCanvasGroup.alpha = 0f;
            return;
        }

        characterCanvasGroup.alpha = line.SpeakerName == PlayerSpeakerName ? playerSpeakerAlpha : 1f;
    }

    #endregion
}
