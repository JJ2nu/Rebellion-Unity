// 인게임 미션 슬롯 프리팹의 텍스트와 실패선 표현을 갱신한다.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InGameMissionSlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text missionText;
    [SerializeField] private TMP_Text missionResultText;
    [SerializeField] private Image failImage;
    [SerializeField, Min(0.01f)] private float failRevealDuration = 0.35f;
    [SerializeField] private AudioSource failAudioSource;
    [SerializeField] private AudioClip failSfx;

    private Coroutine failRevealCoroutine;

    public float FailRevealDuration => Mathf.Max(0.01f, failRevealDuration);
    public float FailSequenceDuration => Mathf.Max(
        FailRevealDuration,
        failSfx != null ? failSfx.length : 0f);

    private void Awake()
    {
        EnsureFailAudioSource();
        ResetFailureImmediate();
    }

    private void OnDisable()
    {
        StopFailureReveal();
    }

    public void Bind(string mission)
    {
        if (missionText == null)
        {
            Debug.LogWarning($"{nameof(InGameMissionSlotUI)} has no mission text assigned.", this);
            return;
        }

        missionText.text = mission;
    }

    public void BindEnemyProgress(string missionLabel, int deadEnemyCount, int totalEnemyCount)
    {
        if (missionResultText == null)
        {
            Debug.LogWarning($"{nameof(InGameMissionSlotUI)} has no mission result text assigned.", this);
            return;
        }

        int safeTotalEnemyCount = Mathf.Max(0, totalEnemyCount);
        int safeDeadEnemyCount = Mathf.Clamp(deadEnemyCount, 0, safeTotalEnemyCount);
        string safeMissionLabel = string.IsNullOrWhiteSpace(missionLabel)
            ? "모든 적 처치"
            : missionLabel;

        // View가 전달한 최종 표시값만 사용해 시뮬레이션 상태를 Slot에서 해석하지 않는다.
        missionResultText.text = $"{safeMissionLabel} ({safeDeadEnemyCount}/{safeTotalEnemyCount})";
    }

    public void ShowFailure(bool isFailed)
    {
        StopFailureReveal();
        if (!isFailed)
        {
            ResetFailureImmediate();
            return;
        }

        if (failImage == null)
        {
            Debug.LogWarning($"{nameof(InGameMissionSlotUI)} has no fail image assigned.", this);
            return;
        }

        failRevealCoroutine = StartCoroutine(RevealFailure());
    }

    public void ResetFailureImmediate()
    {
        StopFailureReveal();
        if (failImage == null)
        {
            return;
        }

        // 같은 Sprite를 왼쪽 원점의 Horizontal Filled 이미지로 사용해 별도 Mask 없이 선을 드러낸다.
        failImage.type = Image.Type.Filled;
        failImage.fillMethod = Image.FillMethod.Horizontal;
        failImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        failImage.fillAmount = 0f;
        failImage.gameObject.SetActive(false);
    }

    private IEnumerator RevealFailure()
    {
        failImage.type = Image.Type.Filled;
        failImage.fillMethod = Image.FillMethod.Horizontal;
        failImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        failImage.fillAmount = 0f;
        failImage.gameObject.SetActive(true);

        EnsureFailAudioSource();
        if (failAudioSource != null && failSfx != null)
        {
            failAudioSource.PlayOneShot(failSfx);
        }

        float duration = FailRevealDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            failImage.fillAmount = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        failImage.fillAmount = 1f;
        failRevealCoroutine = null;
    }

    private void EnsureFailAudioSource()
    {
        if (failAudioSource != null || failSfx == null)
        {
            return;
        }

        failAudioSource = GetComponent<AudioSource>();
        if (failAudioSource != null)
        {
            return;
        }

        // 별도 연결이 없어도 실패 SFX를 재생할 수 있도록 슬롯에 2D AudioSource를 준비한다.
        failAudioSource = gameObject.AddComponent<AudioSource>();
        failAudioSource.playOnAwake = false;
        failAudioSource.loop = false;
        failAudioSource.spatialBlend = 0f;
    }

    private void StopFailureReveal()
    {
        if (failRevealCoroutine == null)
        {
            return;
        }

        StopCoroutine(failRevealCoroutine);
        failRevealCoroutine = null;
    }
}
