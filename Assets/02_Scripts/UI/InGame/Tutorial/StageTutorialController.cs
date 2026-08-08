using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Stage 1 튜토리얼의 페이지 상태, Spacebar 입력, SFX와 모달 입력 잠금 생명주기를 관리한다.
/// </summary>
public sealed class StageTutorialController : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private StageTutorialView view;
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioClip advanceSfx;
    [SerializeField, Min(0f)] private float advanceSfxStartSeconds = 0.062f;

    private InputAction spaceAdvanceAction;
    private IDisposable inputBlockLease;
    private int currentPageIndex;
    private int lastAdvanceFrame = -1;

    public bool IsShowing { get; private set; }

    private void Awake()
    {
        // UI Submit과 분리된 Space 전용 Action을 사용하고, 같은 프레임 중복은 Advance에서 한 번 더 막는다.
        spaceAdvanceAction = new InputAction(
            "AdvanceStageTutorial",
            InputActionType.Button,
            "<Keyboard>/space");
    }

    private void OnEnable()
    {
        if (view != null)
        {
            view.AdvanceRequested += Advance;
        }

        spaceAdvanceAction.performed += HandleSpaceAdvance;
        spaceAdvanceAction.Enable();
    }

    private void OnDisable()
    {
        if (view != null)
        {
            view.AdvanceRequested -= Advance;
            view.SetVisible(false);
        }

        if (spaceAdvanceAction != null)
        {
            spaceAdvanceAction.performed -= HandleSpaceAdvance;
            spaceAdvanceAction.Disable();
        }

        ReleaseInputBlock();
        IsShowing = false;
    }

    private void OnDestroy()
    {
        spaceAdvanceAction?.Dispose();
    }

    public IEnumerator ShowAndWait()
    {
        if (view == null || view.PageCount == 0)
        {
            Debug.LogWarning("[StageTutorialController] Tutorial View or page sprites are missing.", this);
            yield break;
        }

        if (IsShowing)
        {
            while (IsShowing)
            {
                yield return null;
            }

            yield break;
        }

        currentPageIndex = 0;
        lastAdvanceFrame = -1;
        IsShowing = true;
        inputBlockLease = StageInputModalGate.Acquire();
        ShowCurrentPage();

        while (IsShowing)
        {
            yield return null;
        }
    }

    private void Advance()
    {
        if (!IsShowing || lastAdvanceFrame == Time.frameCount)
        {
            return;
        }

        lastAdvanceFrame = Time.frameCount;
        PlayAdvanceSfx();

        if (currentPageIndex + 1 < view.PageCount)
        {
            currentPageIndex++;
            ShowCurrentPage();
            return;
        }

        Close();
    }

    private void ShowCurrentPage()
    {
        view.ShowPage(currentPageIndex);
        // Space가 EventSystem Submit과 튜토리얼 Action에 동시에 소비되지 않도록 선택 상태를 비운다.
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void Close()
    {
        view.SetVisible(false);
        EventSystem.current?.SetSelectedGameObject(null);
        ReleaseInputBlock();
        IsShowing = false;
    }

    private void ReleaseInputBlock()
    {
        inputBlockLease?.Dispose();
        inputBlockLease = null;
    }

    private void PlayAdvanceSfx()
    {
        if (sfxAudioSource == null || advanceSfx == null)
        {
            return;
        }

        // 원본 48 kHz PCM의 약 62 ms 선행 무음을 건너뛰되, clip 교체 시에도 실제 주파수·길이에 맞춰 clamp한다.
        int requestedStartSample = Mathf.RoundToInt(advanceSfxStartSeconds * advanceSfx.frequency);
        int startSample = Mathf.Clamp(requestedStartSample, 0, Mathf.Max(0, advanceSfx.samples - 1));

        // 전용 Tutorial Source를 명시적으로 재시작해 빠른 반복 입력도 해당 입력 순간부터 같은 SFX를 낸다.
        sfxAudioSource.Stop();
        sfxAudioSource.clip = advanceSfx;
        sfxAudioSource.timeSamples = startSample;
        sfxAudioSource.Play();
    }

    private void HandleSpaceAdvance(InputAction.CallbackContext _)
    {
        Advance();
    }
}
