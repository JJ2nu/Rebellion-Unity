using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 판정과 분리된 Presentation의 단일 구독 지점이다.
/// 현재는 상태 보관과 CombatCameraPresentation으로의 카메라 이벤트 전달만 수행한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SimulationController), typeof(CombatCameraPresentation))]
public sealed class CombatPresentationDirector : MonoBehaviour
{
    [Header("Hit Stop")]
    [SerializeField] private bool enableHitStop = true;
    [SerializeField, Range(0.01f, 1f)] private float hitStopTimeScale = 0.1f;
    [SerializeField, Min(0f)] private float bluntHitStopDuration = 0.055f;
    [SerializeField, Min(0f)] private float slashHitStopDuration = 0.045f;
    [SerializeField, Min(0f)] private float allyProjectileHitStopDuration = 0.045f;
    [SerializeField, Min(1f)] private float lethalHitStopMultiplier = 1.25f;

    private SimulationController simulationController;
    private CombatCameraPresentation cameraPresentation;
    private Coroutine hitStopCoroutine;
    private bool isHitStopActive;
    private float hitStopEndRealtime;
    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;

    public int CurrentRunId { get; private set; }
    public int CurrentPhaseIndex { get; private set; }
    public bool IsRunActive { get; private set; }
    public IReadOnlyList<CombatPieceSnapshot> CurrentRunPieces { get; private set; } = System.Array.Empty<CombatPieceSnapshot>();

    private void Awake()
    {
        TryGetComponent(out simulationController);
        TryGetComponent(out cameraPresentation);
    }

    private void OnEnable()
    {
        EnsureSimulationController();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        RestoreTimeScale();
        ResetLocalState();
    }

    private void Subscribe()
    {
        if (simulationController == null)
        {
            return;
        }

        simulationController.SimulationStarted -= HandleSimulationStarted;
        simulationController.SimulationStarted += HandleSimulationStarted;
        simulationController.PhaseStarted -= HandlePhaseStarted;
        simulationController.PhaseStarted += HandlePhaseStarted;
        simulationController.PhaseEnded -= HandlePhaseEnded;
        simulationController.PhaseEnded += HandlePhaseEnded;
        simulationController.AttackStarted -= HandleAttackStarted;
        simulationController.AttackStarted += HandleAttackStarted;
        simulationController.HitConfirmed -= HandleHitConfirmed;
        simulationController.HitConfirmed += HandleHitConfirmed;
        simulationController.ProjectileSpawned -= HandleProjectileSpawned;
        simulationController.ProjectileSpawned += HandleProjectileSpawned;
        simulationController.PieceDied -= HandlePieceDied;
        simulationController.PieceDied += HandlePieceDied;
        simulationController.CombatSimulationFinished -= HandleSimulationFinished;
        simulationController.CombatSimulationFinished += HandleSimulationFinished;
        simulationController.SimulationReset -= ResetLocalState;
        simulationController.SimulationReset += ResetLocalState;
    }

    private void Unsubscribe()
    {
        if (simulationController == null)
        {
            return;
        }

        simulationController.SimulationStarted -= HandleSimulationStarted;
        simulationController.PhaseStarted -= HandlePhaseStarted;
        simulationController.PhaseEnded -= HandlePhaseEnded;
        simulationController.AttackStarted -= HandleAttackStarted;
        simulationController.HitConfirmed -= HandleHitConfirmed;
        simulationController.ProjectileSpawned -= HandleProjectileSpawned;
        simulationController.PieceDied -= HandlePieceDied;
        simulationController.CombatSimulationFinished -= HandleSimulationFinished;
        simulationController.SimulationReset -= ResetLocalState;
    }

    private void HandleSimulationStarted(CombatSimulationContext context)
    {
        CurrentRunId = context.RunId;
        CurrentPhaseIndex = 0;
        IsRunActive = true;
        CurrentRunPieces = context.Pieces;
        cameraPresentation?.BeginRun(context);
    }

    private void HandlePhaseStarted(CombatPhaseContext context)
    {
        if (context.RunId == CurrentRunId)
        {
            CurrentPhaseIndex = context.PhaseIndex;
            cameraPresentation?.BeginPhase(context);
        }
    }

    private void HandlePhaseEnded(CombatPhaseContext context)
    {
        if (context.RunId == CurrentRunId && CurrentPhaseIndex == context.PhaseIndex)
        {
            CurrentPhaseIndex = 0;
        }
    }

    private void HandleAttackStarted(CombatAttackContext context)
    {
        if (context.RunId == CurrentRunId)
        {
            cameraPresentation?.IncludeAttack(context);
        }
    }

    private void HandleHitConfirmed(CombatHitContext context)
    {
        if (context.RunId == CurrentRunId)
        {
            cameraPresentation?.PlayHitImpact(context);
            PlayHitStop(context);
        }
    }

    private void HandleProjectileSpawned(CombatProjectileSpawnedContext context)
    {
        if (context.RunId == CurrentRunId)
        {
            cameraPresentation?.RegisterProjectile(context);
        }
    }

    private void HandlePieceDied(CombatPieceDiedContext context)
    {
    }

    private void HandleSimulationFinished(CombatSimulationFinishedContext context)
    {
        if (context.RunId != CurrentRunId)
        {
            return;
        }

        CurrentPhaseIndex = 0;
        IsRunActive = false;
        CurrentRunPieces = context.Pieces;
        RestoreTimeScale();
        cameraPresentation?.ReturnToBaseline();
    }

    private void ResetLocalState()
    {
        RestoreTimeScale();
        cameraPresentation?.RestoreImmediately();
        CurrentRunId = 0;
        CurrentPhaseIndex = 0;
        IsRunActive = false;
        CurrentRunPieces = System.Array.Empty<CombatPieceSnapshot>();
    }

    private void EnsureSimulationController()
    {
        if (simulationController == null)
        {
            TryGetComponent(out simulationController);
        }
    }

    private void PlayHitStop(CombatHitContext context)
    {
        if (!enableHitStop || !CanPlayHitStop(context))
        {
            return;
        }

        float duration = GetHitStopDuration(context);
        if (duration <= 0f)
        {
            return;
        }

        hitStopEndRealtime = Mathf.Max(
            hitStopEndRealtime,
            Time.realtimeSinceStartup + duration);

        if (isHitStopActive)
        {
            return;
        }

        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;
        ApplyHitStopTimeScale();
        hitStopCoroutine = StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        isHitStopActive = true;

        while (Time.realtimeSinceStartup < hitStopEndRealtime)
        {
            yield return null;
        }

        hitStopCoroutine = null;
        RestoreTimeScale();
    }

    private void ApplyHitStopTimeScale()
    {
        float clampedTimeScale = Mathf.Clamp(hitStopTimeScale, 0.01f, 1f);
        Time.timeScale = clampedTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime * clampedTimeScale;
    }

    private void RestoreTimeScale()
    {
        if (!isHitStopActive && hitStopCoroutine == null)
        {
            return;
        }

        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
            hitStopCoroutine = null;
        }

        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;
        hitStopEndRealtime = 0f;
        isHitStopActive = false;
    }

    private float GetHitStopDuration(CombatHitContext context)
    {
        float duration = context.AttackType switch
        {
            HitImpactAttackType.Blunt => bluntHitStopDuration,
            HitImpactAttackType.Projectile => allyProjectileHitStopDuration,
            _ => slashHitStopDuration,
        };

        if (context.IsLethal)
        {
            duration *= lethalHitStopMultiplier;
        }

        return duration;
    }

    private static bool CanPlayHitStop(CombatHitContext context)
    {
        if (context.AttackType == HitImpactAttackType.Blunt || context.AttackType == HitImpactAttackType.Slash)
        {
            return true;
        }

        return context.AttackType == HitImpactAttackType.Projectile
            && context.Attacker.Faction == Faction.Ally;
    }

}
