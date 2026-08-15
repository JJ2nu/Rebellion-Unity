// 오디오드라마 패널에서 오디오와 시간별 대사를 재생한다.

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

public sealed class AudioDramaPlayer : MonoBehaviour
{
    #region Types

    [Serializable]
    private sealed class AudioClipBinding
    {
        public string audioId;
        public AudioClip audioClip;
    }

    #endregion

    #region Fields

    [Header("Data")]
    [SerializeField] private TextAsset audioDramaCsv;
    [SerializeField] private AudioClipBinding[] audioClips = Array.Empty<AudioClipBinding>();
    [SerializeField] private string previewStageId = "1";

    [Header("Bindings")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Input")]
    [SerializeField] private InputActionReference skipAction;

    [Header("Fade")]
    [SerializeField] private float panelFadeDuration = 0.25f;
    [SerializeField] private float textFadeDuration = 0.35f;

    private AudioDramaDataTable dataTable;
    private Coroutine playRoutine;
    private bool skipRequested;
    private bool holdPanelAfterPlayback;
    private bool wasLastPlaybackSkipped;
    private int playbackEndedFrame = -1;

    #endregion

    #region Properties

    public bool IsPlaying => playRoutine != null;
    public bool IsFullyVisible => canvasGroup != null && canvasGroup.alpha >= 0.99f;
    public bool WasLastPlaybackSkipped => wasLastPlaybackSkipped;
    public bool BlocksSimulationSpacebar =>
        IsPlaying || playbackEndedFrame == Time.frameCount;

    #endregion

    #region Unity Events

    private void Awake()
    {
        Initialize();
        HideImmediate();
    }

    private void OnEnable()
    {
        // 컴포넌트 활성 기간에 UI/Cancel 액션을 연결하고, 표시 중인 오디오드라마에만 스킵을 적용한다.
        BindSkipAction();
    }

    private void OnDisable()
    {
        UnbindSkipAction();
    }

    #endregion

    #region Public Methods

    public void PlayByStageId(string stageId)
    {
        StartPlayback(stageId, false);
    }

    public void PlayEndingByStageId(string stageId)
    {
        // 엔딩 전환막이 패널을 완전히 덮기 전에는 Stage 화면이 다시 드러나지 않도록 마지막 화면을 유지한다.
        StartPlayback(stageId, true);
    }

    public IEnumerator PlayByStageIdAndWait(string stageId)
    {
        holdPanelAfterPlayback = false;
        yield return PlayByStageIdAndWaitInternal(stageId, false);
    }

    public void ReleaseHeldEndingPanel()
    {
        holdPanelAfterPlayback = false;
        HideImmediate();
    }

    private void StartPlayback(string stageId, bool shouldHoldPanelAfterPlayback)
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        holdPanelAfterPlayback = false;
        wasLastPlaybackSkipped = false;
        HideImmediate();
        holdPanelAfterPlayback = shouldHoldPanelAfterPlayback;
        playRoutine = StartCoroutine(
            PlayByStageIdAndWaitInternal(stageId, shouldHoldPanelAfterPlayback));
    }

    private IEnumerator PlayByStageIdAndWaitInternal(string stageId, bool shouldHoldPanelAfterPlayback)
    {
        Initialize();
        skipRequested = false;
        // StartCoroutine 반환값이 playRoutine에 먼저 기록된 다음 실패/완료 상태를 해제할 수 있게 한 프레임 양보한다.
        yield return null;

        if (!dataTable.TryGetByStageId(stageId, out AudioDramaData data))
        {
            Debug.LogWarning($"AudioDrama data was not found. Stage ID: {stageId}", this);
            HideImmediate();
            holdPanelAfterPlayback = false;
            playRoutine = null;
            playbackEndedFrame = Time.frameCount;
            yield break;
        }

        AudioClip clip = FindAudioClip(data.AudioId);
        if (clip == null)
        {
            Debug.LogWarning($"AudioDrama audio clip was not found. Audio ID: {data.AudioId}", this);
            HideImmediate();
            holdPanelAfterPlayback = false;
            playRoutine = null;
            playbackEndedFrame = Time.frameCount;
            yield break;
        }

        yield return PlayDataAndWait(data, clip, shouldHoldPanelAfterPlayback);
        playRoutine = null;
        playbackEndedFrame = Time.frameCount;
    }

    public void Stop()
    {
        // 대기 중인 모든 fade/자막 루프가 같은 프레임에 종료 조건을 볼 수 있도록 먼저 표시한다.
        skipRequested = true;
        wasLastPlaybackSkipped = true;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (holdPanelAfterPlayback)
        {
            HoldPanelImmediate();
        }
        else
        {
            HideImmediate();
        }

        // AudioDrama와 Simulation Controls가 같은 Space performed 이벤트를 받는다.
        // 스킵 콜백이 먼저 실행돼 IsPlaying이 false가 되더라도 같은 프레임의 Play 입력은 막는다.
        playbackEndedFrame = Time.frameCount;
    }

    #endregion

    #region Preview

    [ContextMenu("Play Preview Stage")]
    private void PlayPreviewStage()
    {
        PlayByStageId(previewStageId);
    }

    #endregion

    #region Initialization

    private void Initialize()
    {
        if (dataTable == null)
        {
            dataTable = AudioDramaDataTable.FromCsv(audioDramaCsv);
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        if (dialogueText == null)
        {
            dialogueText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void BindSkipAction()
    {
        if (skipAction?.action == null)
        {
            return;
        }

        skipAction.action.performed -= HandleSkipPerformed;
        skipAction.action.performed += HandleSkipPerformed;
        skipAction.action.Enable();
    }

    private void UnbindSkipAction()
    {
        if (skipAction?.action == null)
        {
            return;
        }

        skipAction.action.performed -= HandleSkipPerformed;
    }

    private void HandleSkipPerformed(InputAction.CallbackContext _)
    {
        if (IsVisibleOrPlaying())
        {
            Stop();
        }
    }

    #endregion

    #region Playback

    private IEnumerator PlayDataAndWait(
        AudioDramaData data,
        AudioClip clip,
        bool shouldHoldPanelAfterPlayback)
    {
        // 일반 재생은 즉시 패널을 정리하고, 엔딩 재생은 전환막이 올라올 때까지 현재 화면을 유지한다.
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;
        dialogueText.text = string.Empty;
        SetTextAlpha(0f);

        yield return FadePanel(0f, 1f, panelFadeDuration);
        if (skipRequested)
        {
            FinishPlaybackVisibility(shouldHoldPanelAfterPlayback);
            yield break;
        }

        audioSource.clip = clip;
        audioSource.time = 0f;
        audioSource.Play();
        PlayVideoIfAssigned();

        for (int index = 0; index < data.Lines.Count; index++)
        {
            AudioDramaLineData line = data.Lines[index];

            while (!skipRequested && audioSource.isPlaying && audioSource.time < line.StartTime)
            {
                yield return null;
            }

            if (skipRequested)
            {
                FinishPlaybackVisibility(shouldHoldPanelAfterPlayback);
                yield break;
            }

            dialogueText.text = line.Text;

            float duration = Mathf.Max(0f, line.EndTime - line.StartTime);
            float fadeDuration = Mathf.Min(textFadeDuration, duration * 0.5f);
            float holdDuration = Mathf.Max(0f, duration - fadeDuration * 2f);

            yield return FadeText(0f, 1f, fadeDuration);
            yield return WaitForSecondsOrSkip(holdDuration);
            yield return FadeText(1f, 0f, fadeDuration);

            if (skipRequested)
            {
                FinishPlaybackVisibility(shouldHoldPanelAfterPlayback);
                yield break;
            }
        }

        while (!skipRequested && audioSource.isPlaying)
        {
            yield return null;
        }

        if (shouldHoldPanelAfterPlayback)
        {
            HoldPanelImmediate();
        }
        else
        {
            yield return FadePanel(1f, 0f, panelFadeDuration);
            HideImmediate();
        }
    }

    private AudioClip FindAudioClip(string audioId)
    {
        for (int index = 0; index < audioClips.Length; index++)
        {
            AudioClipBinding binding = audioClips[index];
            if (binding != null && binding.audioId == audioId)
            {
                return binding.audioClip;
            }
        }

        return null;
    }

    #endregion

    #region Fade

    private IEnumerator FadePanel(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        canvasGroup.alpha = from;

        while (elapsed < duration)
        {
            if (skipRequested)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private IEnumerator FadeText(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetTextAlpha(to);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (skipRequested)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            SetTextAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetTextAlpha(to);
    }

    private void SetTextAlpha(float alpha)
    {
        Color color = dialogueText.color;
        color.a = alpha;
        dialogueText.color = color;
    }

    private IEnumerator WaitForSecondsOrSkip(float duration)
    {
        float elapsed = 0f;
        while (!skipRequested && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void PlayVideoIfAssigned()
    {
        if (videoPlayer == null)
        {
            return;
        }

        videoPlayer.time = 0d;
        videoPlayer.Play();
    }

    #endregion

    #region Visibility

    private void FinishPlaybackVisibility(bool shouldHoldPanelAfterPlayback)
    {
        if (shouldHoldPanelAfterPlayback)
        {
            HoldPanelImmediate();
            return;
        }

        HideImmediate();
    }

    private void HoldPanelImmediate()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        if (videoPlayer != null)
        {
            videoPlayer.Pause();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = false;
        }
    }

    private void HideImmediate()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.time = 0d;
        }

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
            SetTextAlpha(0f);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private bool IsVisibleOrPlaying()
    {
        return playRoutine != null ||
               (!skipRequested && audioSource != null && audioSource.isPlaying) ||
               (videoPlayer != null && videoPlayer.isPlaying) ||
               (canvasGroup != null && canvasGroup.alpha > 0f);
    }

    #endregion
}
