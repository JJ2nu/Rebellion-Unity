using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Plays dialogue lines from CSV data and updates character animation.
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

    [Header("Events")]
    [SerializeField] private UnityEvent nextStageRequestedEvent;

    private DialogueDataTable table;
    private TextAsset cachedDialogueCsv;
    private List<DialogueLineData> currentLines = new();
    private int currentIndex;
    private int lastAdvanceFrame = -1;

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

        Debug.Log($"Dialogue preview started. Level: {level}, Result: {result}, Lines: {currentLines.Count}");

        if (currentLines.Count == 0)
        {
            ClearText();
            return;
        }

        ShowLine(currentLines[currentIndex]);
    }

    public void PlayPreviewDialogue()
    {
        Play(previewLevel, previewResult);
    }

    public void AdvanceDialogue()
    {
        if (Application.isPlaying && lastAdvanceFrame == Time.frameCount)
        {
            return;
        }

        lastAdvanceFrame = Application.isPlaying ? Time.frameCount : -1;

        AdvanceCurrentLine();
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
        if (currentLines == null || currentLines.Count == 0)
        {
            Debug.LogWarning("Dialogue textbox clicked, but no dialogue is playing.");
            return;
        }

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

    #endregion

    #region Character

    private void ApplyCharacterAlpha(DialogueLineData line)
    {
        if (characterCanvasGroup == null)
        {
            return;
        }

        characterCanvasGroup.alpha = line.SpeakerName == PlayerSpeakerName ? playerSpeakerAlpha : 1f;
    }

    #endregion
}
