using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Opening Shot 전용 화면 단계를 조율하고 총격 프레임의 게임 효과를 Task 33 Context에 요청한다.
/// View는 표현만 담당하며 이 Controller가 연출 순서, 스킵과 완료 경계를 소유한다.
/// </summary>
public sealed class OpeningShotPresentationController : PreSimulationSkillPresentation
{
    public enum PresentationStage
    {
        Idle,
        Entering,
        Zooming,
        Aiming,
        Firing,
        DeathHold,
        Returning,
    }

    [Header("View")]
    [SerializeField] private OpeningShotPresentationView view;

    [Header("Timeline")]
    [SerializeField, Min(0f)] private float entryDuration = 0.5f;
    [SerializeField, Min(0f)] private float zoomDuration = 0.85f;
    [SerializeField, Min(0f)] private float aimHoldDuration = 0.55f;
    [SerializeField, Min(0f)] private float shotReactionDuration = 0.18f;
    [SerializeField, Min(0f)] private float deathHoldDuration = 0.1f;
    [SerializeField, Min(0f)] private float returnDuration = 0.25f;

    [Header("Target Framing")]
    [SerializeField, Range(0f, 1f)] private float headAimHeight = 0.82f;
    [SerializeField, Min(1f)] private float framingPadding = 1.2f;
    [SerializeField, Range(0.1f, 1f)] private float cinematicVisibleHeight = 0.75f;
    [SerializeField, Min(0.1f)] private float minimumCameraDistance = 2.5f;

    [Header("Shot Reaction")]
    [SerializeField, Min(0f)] private float shakePositionStrength = 0.12f;
    [SerializeField, Min(0f)] private float shakeRotationStrength = 2.5f;
    [SerializeField, Min(1f)] private float shakeFrequency = 18f;

    private bool isPlaying;
    private bool skipRequested;

    public bool IsPlaying => isPlaying;
    public bool IsSkipRequested => skipRequested;
    public PresentationStage CurrentStage { get; private set; }

    public override bool CanPresent(SkillBase skill)
    {
        return skill is OpeningShotSkill;
    }

    public override IEnumerator Play(PreSimulationPresentationContext context)
    {
        OpeningShotSkill openingShotSkill = context?.Skill as OpeningShotSkill;
        PieceBase target = openingShotSkill?.Target;

        if (view == null || target == null || target.IsDead)
        {
            yield break;
        }

        isPlaying = true;
        skipRequested = false;
        view.SkipRequested -= HandleSkipRequested;
        view.SkipRequested += HandleSkipRequested;

        try
        {
            if (!view.BeginPresentation(context.AllPieces))
            {
                yield break;
            }

            CurrentStage = PresentationStage.Entering;
            yield return RunStage(entryDuration, view.SetEntryProgress);
            if (CompleteSkipIfRequested(context))
            {
                yield break;
            }

            view.ActivateCinematicCamera();
            if (!view.PrepareTargetFraming(
                    target.transform,
                    headAimHeight,
                    framingPadding,
                    cinematicVisibleHeight,
                    minimumCameraDistance))
            {
                yield break;
            }

            CurrentStage = PresentationStage.Zooming;
            yield return RunStage(zoomDuration, view.SetZoomProgress);
            if (CompleteSkipIfRequested(context))
            {
                yield break;
            }

            CurrentStage = PresentationStage.Aiming;
            yield return RunStage(aimHoldDuration, null);
            if (CompleteSkipIfRequested(context))
            {
                yield break;
            }

            // 총성, 카메라 반동과 스킬 효과가 같은 프레임에 시작된다.
            CurrentStage = PresentationStage.Firing;
            view.PlayShotSfx();
            context.TryApplyEffect();
            yield return RunStage(
                shotReactionDuration,
                progress => view.SetShotReaction(
                    progress,
                    shakePositionStrength,
                    shakeRotationStrength,
                    shakeFrequency));
            view.ResetShotReaction();

            if (CompleteSkipIfRequested(context))
            {
                yield break;
            }

            CurrentStage = PresentationStage.DeathHold;
            yield return RunStage(deathHoldDuration, null);
            if (CompleteSkipIfRequested(context))
            {
                yield break;
            }

            CurrentStage = PresentationStage.Returning;
            yield return RunStage(returnDuration, view.SetReturnProgress);
        }
        finally
        {
            view.SkipRequested -= HandleSkipRequested;
            view.RestoreImmediate();
            skipRequested = false;
            isPlaying = false;
            CurrentStage = PresentationStage.Idle;
        }
    }

    public override void CancelPresentation()
    {
        skipRequested = false;
        isPlaying = false;
        CurrentStage = PresentationStage.Idle;

        if (view != null)
        {
            view.SkipRequested -= HandleSkipRequested;
            view.RestoreImmediate();
        }
    }

    private IEnumerator RunStage(float duration, Action<float> applyProgress)
    {
        float safeDuration = Mathf.Max(0f, duration);
        applyProgress?.Invoke(0f);

        if (safeDuration <= 0f)
        {
            applyProgress?.Invoke(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < safeDuration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            applyProgress?.Invoke(Mathf.Clamp01(elapsed / safeDuration));
            yield return null;
        }

        if (!skipRequested)
        {
            applyProgress?.Invoke(1f);
        }
    }

    private bool CompleteSkipIfRequested(PreSimulationPresentationContext context)
    {
        if (!skipRequested)
        {
            return false;
        }

        // 총격 전 ESC라도 게임 효과와 실행 사실을 Context의 1회 보장 경로로 확정한다.
        context.TryApplyEffect();
        view.RestoreImmediate();
        return true;
    }

    private void HandleSkipRequested()
    {
        if (isPlaying)
        {
            skipRequested = true;
        }
    }
}
