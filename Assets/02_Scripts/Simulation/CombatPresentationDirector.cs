using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 판정과 분리된 Presentation의 단일 구독 지점이다.
/// 상태 보관, 카메라 이벤트 전달, 사망 반동에 필요한 명중 문맥 연결을 수행한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(
    typeof(SimulationController),
    typeof(CombatCameraPresentation),
    typeof(CombatDeathReactionPresentation))]
public sealed class CombatPresentationDirector : MonoBehaviour
{
    [Header("Combat Presentation")]
    [SerializeField] private bool enableCombatPresentation = true;

    [Header("Hit Stop")]
    [SerializeField] private bool enableHitStop = true;
    [SerializeField, Range(0.01f, 1f)] private float hitStopTimeScale = 0.1f;
    [SerializeField, Min(0f)] private float bluntHitStopDuration = 0.069f;
    [SerializeField, Min(0f)] private float slashHitStopDuration = 0.056f;
    [SerializeField, Min(0f)] private float allyProjectileHitStopDuration = 0.056f;

    private SimulationController simulationController;
    private CombatCameraPresentation cameraPresentation;
    private CombatDeathReactionPresentation deathReactionPresentation;
    private Coroutine hitStopCoroutine;
    // PieceDied에는 공격 타입/방향이 없으므로 직전 명중 문맥을 피해자별로 보관했다가 사망 확정 시 소비한다.
    // IsLethal에 의존하지 않아 현재의 한 방 규칙과 향후 체력 규칙 모두에서 동일하게 동작한다.
    private readonly Dictionary<PieceBase, CombatHitContext> pendingHitContexts = new();
    private bool isHitStopActive;
    private bool isHitStopSuppressed;
    private bool isCombatCameraSuppressed;
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
        TryGetComponent(out deathReactionPresentation);
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
        simulationController.CombatPresentationReady -= HandleCombatPresentationReady;
        simulationController.CombatPresentationReady += HandleCombatPresentationReady;
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
        simulationController.CombatPresentationReady -= HandleCombatPresentationReady;
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
        // 결과 화면에서 새 시뮬레이션을 바로 시작하는 경로도 안전하게 처리한다.
        deathReactionPresentation?.RestoreImmediately();
        pendingHitContexts.Clear();
        CurrentRunId = context.RunId;
        CurrentPhaseIndex = 0;
        IsRunActive = true;
        CurrentRunPieces = context.Pieces;
    }

    private void HandleCombatPresentationReady(CombatSimulationContext context)
    {
        if (context.RunId != CurrentRunId || !IsRunActive)
        {
            return;
        }

        CurrentRunPieces = context.Pieces;
        if (enableCombatPresentation && !isCombatCameraSuppressed)
        {
            cameraPresentation?.BeginRun(context);
        }
    }

    private void HandlePhaseStarted(CombatPhaseContext context)
    {
        if (context.RunId == CurrentRunId)
        {
            CurrentPhaseIndex = context.PhaseIndex;
            if (enableCombatPresentation && !isCombatCameraSuppressed)
            {
                cameraPresentation?.BeginPhase(context);
            }
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
            if (enableCombatPresentation && !isCombatCameraSuppressed)
            {
                cameraPresentation?.IncludeAttack(context);
            }
        }
    }

    private void HandleHitConfirmed(CombatHitContext context)
    {
        if (context.RunId == CurrentRunId)
        {
            if (enableCombatPresentation)
            {
                // 스킵 중에도 사망 반동에 필요한 명중 문맥은 유지하고, 카메라 임펄스만 차단한다.
                if (!isCombatCameraSuppressed)
                {
                    cameraPresentation?.PlayHitImpact(context);
                }

                CacheHitContext(context);
                PlayHitStop(context);
            }
        }
    }

    private void HandleProjectileSpawned(CombatProjectileSpawnedContext context)
    {
        if (context.RunId == CurrentRunId)
        {
            if (enableCombatPresentation && !isCombatCameraSuppressed)
            {
                cameraPresentation?.RegisterProjectile(context);
            }
        }
    }

    private void HandlePieceDied(CombatPieceDiedContext context)
    {
        PieceBase piece = context.Piece.Piece;
        if (context.RunId != CurrentRunId || piece == null)
        {
            return;
        }

        if (pendingHitContexts.TryGetValue(piece, out CombatHitContext hitContext))
        {
            pendingHitContexts.Remove(piece);
            deathReactionPresentation?.Play(piece, hitContext.ImpactDirection, hitContext.AttackType);
        }
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
        pendingHitContexts.Clear();
        // 종료 줌아웃은 스킵 여부와 관계없이 유지한다. 스킵 중이면 이미 복귀가 진행 중이므로 목표만 재확인된다.
        if (enableCombatPresentation)
        {
            cameraPresentation?.ReturnToBaseline();
        }
    }

    private void ResetLocalState()
    {
        RestoreTimeScale();
        cameraPresentation?.RestoreImmediately();
        deathReactionPresentation?.RestoreImmediately();
        pendingHitContexts.Clear();
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

        if (deathReactionPresentation == null)
        {
            TryGetComponent(out deathReactionPresentation);
        }
    }

    private void CacheHitContext(CombatHitContext context)
    {
        if (context.Victim.Piece != null)
        {
            pendingHitContexts[context.Victim.Piece] = context;
        }
    }

    /// <summary>
    /// 연출 스킵의 고속 진행 동안 히트스톱이 timeScale을 덮어쓰지 않도록 억제한다.
    /// 활성 히트스톱은 즉시 종료해, 히트스톱이 보관한 timeScale 복원과 스킵 배속이 충돌하지 않게 한다.
    /// </summary>
    public void SetHitStopSuppressed(bool suppressed)
    {
        isHitStopSuppressed = suppressed;

        if (suppressed)
        {
            RestoreTimeScale();
        }
    }

    /// <summary>
    /// 연출 스킵의 고속 진행 동안 전투 카메라 연출이 배속으로 재생되어 어지럽지 않도록 차단한다.
    /// 차단 시작 시 기존 종료 연출(ReturnToBaseline)로 부드러운 줌아웃을 시작해
    /// 기물들이 고속으로 움직이는 동안 카메라는 기본 구도로 복귀만 하고, 해제 전까지 새 연출을 시작하지 않는다.
    /// 줌아웃 easing은 unscaled 시간이라 배속의 영향을 받지 않는다.
    /// </summary>
    public void SetCombatCameraSuppressed(bool suppressed)
    {
        isCombatCameraSuppressed = suppressed;

        if (suppressed)
        {
            cameraPresentation?.ReturnToBaseline();
        }
    }

    private void PlayHitStop(CombatHitContext context)
    {
        if (!enableHitStop || isHitStopSuppressed || !CanPlayHitStop(context))
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

        return duration;
    }

    private static bool CanPlayHitStop(CombatHitContext context)
    {
        if (context.AttackType == HitImpactAttackType.Blunt || context.AttackType == HitImpactAttackType.Slash)
        {
            return true;
        }

        return context.AttackType == HitImpactAttackType.Projectile
            && context.Attacker.Faction == Faction.Ally
            && context.Victim.Faction == Faction.Enemy;
    }

}
