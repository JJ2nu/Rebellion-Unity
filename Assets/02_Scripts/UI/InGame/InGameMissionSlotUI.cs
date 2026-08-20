// 인게임 미션 슬롯 프리팹의 텍스트와 실패선·클리어 동그라미 표현을 갱신한다.

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
    // 클리어 동그라미는 실패선과 같은 톤의 별도 이미지로, 원을 그리듯 Radial 채움으로 드러낸다.
    [SerializeField] private Image clearImage;
    [SerializeField, Min(0.01f)] private float clearRevealDuration = 0.4f;
    [SerializeField] private AudioClip clearSfx;

    private Coroutine failRevealCoroutine;
    private Coroutine clearRevealCoroutine;

    public float FailRevealDuration => Mathf.Max(0.01f, failRevealDuration);
    public float FailSequenceDuration => Mathf.Max(
        FailRevealDuration,
        failSfx != null ? failSfx.length : 0f);
    public float ClearRevealDuration => Mathf.Max(0.01f, clearRevealDuration);
    public float ClearSequenceDuration => Mathf.Max(
        ClearRevealDuration,
        clearSfx != null ? clearSfx.length : 0f);

    private void Awake()
    {
        EnsureResultAudioSource();
        ResetFailureImmediate();
        ResetSuccessImmediate();
    }

    private void OnDisable()
    {
        StopFailureReveal();
        StopSuccessReveal();
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

    public void ShowSuccess(bool isSucceeded)
    {
        StopSuccessReveal();
        if (!isSucceeded)
        {
            ResetSuccessImmediate();
            return;
        }

        if (clearImage == null)
        {
            Debug.LogWarning($"{nameof(InGameMissionSlotUI)} has no clear image assigned.", this);
            return;
        }

        clearRevealCoroutine = StartCoroutine(RevealSuccess());
    }

    public void ResetSuccessImmediate()
    {
        StopSuccessReveal();
        if (clearImage == null)
        {
            return;
        }

        // 동그라미 Sprite를 Radial360 Filled로 사용해 원을 그리는 방향으로 드러낸다.
        ApplyClearImageFillSettings();
        clearImage.fillAmount = 0f;
        clearImage.gameObject.SetActive(false);
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

        EnsureResultAudioSource();
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

    private IEnumerator RevealSuccess()
    {
        ApplyClearImageFillSettings();
        clearImage.fillAmount = 0f;
        clearImage.gameObject.SetActive(true);

        EnsureResultAudioSource();
        if (failAudioSource != null && clearSfx != null)
        {
            failAudioSource.PlayOneShot(clearSfx);
        }

        // 실패선과 동일하게 unscaled 시간으로 진행해 배속·스킵 상태의 영향을 받지 않는다.
        float duration = ClearRevealDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            clearImage.fillAmount = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        clearImage.fillAmount = 1f;
        clearRevealCoroutine = null;
    }

    private void ApplyClearImageFillSettings()
    {
        // 동그라미 Sprite의 꼬리(우하단)가 마지막에 드러나도록 하단 원점 시계방향으로 채운다.
        clearImage.type = Image.Type.Filled;
        clearImage.fillMethod = Image.FillMethod.Radial360;
        clearImage.fillOrigin = (int)Image.Origin360.Bottom;
        clearImage.fillClockwise = true;
    }

    private void EnsureResultAudioSource()
    {
        // 실패·클리어 SFX가 하나라도 연결되어 있으면 공용 2D AudioSource를 준비한다.
        if (failAudioSource != null || (failSfx == null && clearSfx == null))
        {
            return;
        }

        failAudioSource = GetComponent<AudioSource>();
        if (failAudioSource != null)
        {
            return;
        }

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

    private void StopSuccessReveal()
    {
        if (clearRevealCoroutine == null)
        {
            return;
        }

        StopCoroutine(clearRevealCoroutine);
        clearRevealCoroutine = null;
    }
}
