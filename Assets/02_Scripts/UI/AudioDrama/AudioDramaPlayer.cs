// 오디오드라마 패널에서 오디오와 시간별 대사를 재생한다.

using System;
using System.Collections;
using TMPro;
using UnityEngine;

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
    [SerializeField] private TMP_Text dialogueText;

    [Header("Fade")]
    [SerializeField] private float panelFadeDuration = 0.25f;
    [SerializeField] private float textFadeDuration = 0.35f;

    private AudioDramaDataTable dataTable;
    private Coroutine playRoutine;

    #endregion

    #region Properties

    public bool IsPlaying => playRoutine != null;

    #endregion

    #region Unity Events

    private void Awake()
    {
        Initialize();
        HideImmediate();
    }

    #endregion

    #region Public Methods

    public void PlayByStageId(string stageId)
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        HideImmediate();
        playRoutine = StartCoroutine(PlayByStageIdAndWait(stageId));
    }

    public IEnumerator PlayByStageIdAndWait(string stageId)
    {
        Initialize();

        if (!dataTable.TryGetByStageId(stageId, out AudioDramaData data))
        {
            Debug.LogWarning($"AudioDrama data was not found. Stage ID: {stageId}", this);
            HideImmediate();
            playRoutine = null;
            yield break;
        }

        AudioClip clip = FindAudioClip(data.AudioId);
        if (clip == null)
        {
            Debug.LogWarning($"AudioDrama audio clip was not found. Audio ID: {data.AudioId}", this);
            HideImmediate();
            playRoutine = null;
            yield break;
        }

        yield return PlayDataAndWait(data, clip);
        playRoutine = null;
    }

    public void Stop()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        HideImmediate();
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

        if (dialogueText == null)
        {
            dialogueText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    #endregion

    #region Playback

    private IEnumerator PlayDataAndWait(AudioDramaData data, AudioClip clip)
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;
        dialogueText.text = string.Empty;
        SetTextAlpha(0f);

        yield return FadePanel(0f, 1f, panelFadeDuration);

        audioSource.clip = clip;
        audioSource.time = 0f;
        audioSource.Play();

        for (int index = 0; index < data.Lines.Count; index++)
        {
            AudioDramaLineData line = data.Lines[index];

            while (audioSource.isPlaying && audioSource.time < line.StartTime)
            {
                yield return null;
            }

            dialogueText.text = line.Text;

            float duration = Mathf.Max(0f, line.EndTime - line.StartTime);
            float fadeDuration = Mathf.Min(textFadeDuration, duration * 0.5f);
            float holdDuration = Mathf.Max(0f, duration - fadeDuration * 2f);

            yield return FadeText(0f, 1f, fadeDuration);
            yield return new WaitForSeconds(holdDuration);
            yield return FadeText(1f, 0f, fadeDuration);
        }

        while (audioSource.isPlaying)
        {
            yield return null;
        }

        yield return FadePanel(1f, 0f, panelFadeDuration);
        HideImmediate();
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

    #endregion

    #region Visibility

    private void HideImmediate()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
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

    #endregion
}
