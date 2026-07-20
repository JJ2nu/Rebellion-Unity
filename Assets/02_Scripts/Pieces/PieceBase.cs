using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 기물의 베이스 클래스. 그리드 위에 배치되어 시뮬레이션에 참여한다.
/// 새로운 기물 추가 시 이 클래스를 상속하고 ExecuteAction()과 FindTarget()을 구현한다.
/// </summary>
public abstract class PieceBase : MonoBehaviour, IWorldInputTarget
{
    // ─── Inspector ──────────────────────────────────────────────────
    [Header("Piece Config")]
    [SerializeField] protected Faction _faction = Faction.Ally;
    [SerializeField] protected PieceType _pieceType = PieceType.Brawler;
    [SerializeField] protected int _maxHealth = 1;
    [SerializeField] protected int _attackRange = 1;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _deathCrossFadeDuration = 0.12f;

    public GameObject _HUD{get; set; }
    public GameObject _DirectionIndicator { get; set; }
    protected Animator _animator;

    public bool _isTargeted = false;
    public bool _isInRange = false;

    // ─── Properties ─────────────────────────────────────────────────
    public Faction Faction => _faction;
    public PieceType PieceType => _pieceType;
    public int MaxHealth => _maxHealth;
    public int AttackRange => _attackRange;

    public int GridX { get; set; }
    public int GridY { get; set; }
    public Direction FacingDirection { get; set; } = Direction.East;

    public int CurrentHealth { get; private set; }
    [SerializeField] public bool IsDead = false;
    public bool IsActionFinished { get; protected set; }
    private bool _hasSpawnState;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private int _spawnGridX;
    private int _spawnGridY;
    private Direction _spawnFacingDirection;
    private Direction? _pendingDamageDirection;
    private Quaternion _rotationBeforeDeath;
    private bool _hasRotationBeforeDeath;
    private Coroutine _retryRewindCoroutine;
    private Animator _retryRewindAnimator;
    private bool _useManualRootMotion;
    private bool _skipNextRootMotionDelta;
    private bool _overrideRootMotionDirection;
    private Vector3 _rootMotionWorldDirection;
    private float _rootMotionDistanceScale = 1f;

    /// <summary>페이즈 인덱스 (Brawler=1, Slasher=2, Gunman=3, 미행동=0)</summary>
    public virtual int SimulationPhaseIndex => 0;

    /// <summary>스킬 등으로 페이즈를 앞뒤로 이동. +1=뒤로, -1=앞으로</summary>
    public int PhaseOffset { get; set; } = 0;

    /// <summary>실제 실행 페이즈 = SimulationPhaseIndex + PhaseOffset</summary>
    public int EffectivePhaseIndex => SimulationPhaseIndex + PhaseOffset;

    /// <summary>시뮬레이션 0페이즈에서 설정된 공격 가능 여부</summary>
    [SerializeField] public bool CanAct = false;

    /// <summary>현재 기물 목록 기준으로 즉시 공격 가능 여부를 반환한다 (호버 표시용).</summary>
    public bool CheckCanActNow(IReadOnlyList<PieceBase> allPieces) => FindTarget(allPieces) != null;

    // ─── Events ─────────────────────────────────────────────────────
    public event Action<PieceBase> OnDied;
    public event Action<PieceBase, int> OnDamageTaken;
    public event Action<PieceBase> OnActionFinished;

    // ─── Simulation lifecycle ────────────────────────────────────────

    protected void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        OnDied += piece =>
        {
            if (SimulationController.Instance != null)
            {
                SimulationController.Instance._currentDeadCount[(int)_faction]++;
            }
        };
    }
    private void Start()
    {
        _HUD = transform.Find("HUD")?.gameObject;

        _HUD?.SetActive(true);

        _DirectionIndicator = transform.Find("DirectionIndicator")?.gameObject;
        _DirectionIndicator?.SetActive(true);

        ResetColliderState();
    }
    public void Update()
    {
        if (_HUD != null)
        {
            if (_isInRange)
            {
                _HUD.GetComponent<InGameHUDUI>().Dead();
            }
            else
            {
                if (!_isTargeted)
                {
                    if (SimulationController.Instance == null || !SimulationController.Instance._isRunning)
                        CheckCanAct(StageManager.Instance.GetAllActivePieces());
                    if (CanAct)
                    {
                        _HUD.GetComponent<InGameHUDUI>().Active();
                    }
                    else
                    {
                        _HUD.GetComponent<InGameHUDUI>().Inactive();
                    }
                }
                else
                {
                    _HUD.GetComponent<InGameHUDUI>().Dead();
                }
            }
        }
    }
    public virtual void OnSimulationStart()
    {
        _DirectionIndicator?.SetActive(false);
        CurrentHealth = _maxHealth;
        IsDead = false;
        IsActionFinished = false;
        CanAct = false;
        PhaseOffset = 0;
        ResetAnimatorState(false);
        ResetColliderState();
    }

    /// <summary>Phase 0에서 호출. FindTarget 결과로 CanAct를 설정한다.</summary>
    public void CheckCanAct(IReadOnlyList<PieceBase> allPieces)
    {
        CanAct = !IsDead && FindTarget(allPieces) != null;
    }

    /// <summary>
    /// 시뮬레이션 단계에서 이 기물이 수행할 행동. 코루틴으로 구현한다.
    /// stepDuration: 한 스탭당 경과 시간 (초)
    /// </summary>
    public abstract IEnumerator ExecuteAction(IReadOnlyList<PieceBase> allPieces, float stepDuration);

    /// <summary>
    /// 현재 방향과 사정거리를 기준으로 공격 대상을 탐색한다.
    /// </summary>
    protected abstract PieceBase FindTarget(IReadOnlyList<PieceBase> allPieces);

    // ─── Combat ─────────────────────────────────────────────────────

    public virtual void TakeDamage(int damage, Direction? attackDirection = null)
    {
        if (IsDead) return;

        _pendingDamageDirection = attackDirection;
        CurrentHealth -= damage;
        OnDamageTaken?.Invoke(this, damage);

        if (CurrentHealth <= 0)
            Die();
        else
            _pendingDamageDirection = null;
    }

    public virtual void Die()
    {
        if (IsDead) return;

        IsDead = true;
        IsActionFinished = true;
        _rotationBeforeDeath = transform.rotation;
        _hasRotationBeforeDeath = true;

        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col.GetComponent<AttackHitbox>() != null)
            {
                continue;
            }

            col.enabled = false;
        }

        PlayDeathAnimation();
        _pendingDamageDirection = null;
        OnDied?.Invoke(this);
    }

    protected virtual void PlayDeathAnimation()
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator == null) return;

        animator.speed = 1f;
        ResetAnimatorTrigger(animator, "Attack");
        ResetAnimatorTrigger(animator, "Reset");
        if (_pendingDamageDirection.HasValue)
        {
            transform.rotation = DirectionToRotation(_pendingDamageDirection.Value);
        }

        SetAnimatorRootMotion(true, true);

        int hitStateHash = Animator.StringToHash("Hit");
        if (animator.HasState(0, hitStateHash))
        {
            animator.CrossFadeInFixedTime(hitStateHash, _deathCrossFadeDuration, 0, 0f);
            return;
        }

        animator.SetTrigger("Hit");
    }


    public virtual void ResetState()
    {
        CurrentHealth = _maxHealth;
        IsDead = false;
        IsActionFinished = false;
        CanAct = false;
        PhaseOffset = 0;
        _pendingDamageDirection = null;
        if (_hasRotationBeforeDeath)
        {
            transform.rotation = _rotationBeforeDeath;
            _hasRotationBeforeDeath = false;
        }

        ResetAnimatorState(true);
        ResetColliderState();
        _DirectionIndicator?.SetActive(true);
    }

    public void CaptureSpawnState()
    {
        _hasSpawnState = true;
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
        _spawnGridX = GridX;
        _spawnGridY = GridY;
        _spawnFacingDirection = FacingDirection;
    }

    public void RestoreSpawnState()
    {
        if (!_hasSpawnState)
        {
            return;
        }

        transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        GridX = _spawnGridX;
        GridY = _spawnGridY;
        FacingDirection = _spawnFacingDirection;
    }

    public void StartRetryRewind(float duration)
    {
        if (_retryRewindCoroutine != null)
        {
            StopCoroutine(_retryRewindCoroutine);
            DisableRetryRewindRootMotion();
        }

        _retryRewindCoroutine = StartCoroutine(RewindToSpawnForRetry(duration));
    }

    public IEnumerator RewindToSpawnForRetry(float duration)
    {
        if (!_hasSpawnState)
        {
            _retryRewindCoroutine = null;
            yield break;
        }

        gameObject.SetActive(true);

        var animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            _retryRewindAnimator = animator;
            SetAnimatorRootMotion(false);
            PlayResetAnimation();
        }

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, _spawnPosition, eased),
                Quaternion.Slerp(startRotation, _spawnRotation, eased));

            elapsed += Time.deltaTime;
            yield return null;
        }

        RestoreSpawnState();
        ResetState();
        StabilizeAnimatorForRetry(animator);
        RestoreSpawnState();
        yield return null;
        RestoreSpawnState();

        DisableRetryRewindRootMotion();
        _retryRewindCoroutine = null;
    }

    private void StabilizeAnimatorForRetry(Animator animator)
    {
        if (animator == null)
        {
            return;
        }

        ResetAnimatorTrigger(animator, "Attack");
        ResetAnimatorTrigger(animator, "Hit");
        ResetAnimatorTrigger(animator, "Reset");

        if (animator.HasState(0, Animator.StringToHash("Idle")))
        {
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);
        }
    }

    private void DisableRetryRewindRootMotion()
    {
        if (_retryRewindAnimator != null)
        {
            _retryRewindAnimator.applyRootMotion = false;
            _retryRewindAnimator = null;
        }

        _useManualRootMotion = false;
        _skipNextRootMotionDelta = false;
        _overrideRootMotionDirection = false;
        _rootMotionWorldDirection = Vector3.zero;
        _rootMotionDistanceScale = 1f;
    }

    public void PlayResetAnimation()
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            return;
        }

        if (HasTriggerParameter(animator, "Reset"))
        {
            animator.SetTrigger("Reset");
            return;
        }

        animator.Play("Idle", 0, 0f);
    }

    // ─── Grid Helpers ────────────────────────────────────────────────

    /// <summary>
    /// 현재 방향 기준의 그리드 델타 (dx, dy)를 반환한다.
    /// </summary>
    public (int dx, int dy) GetFacingDelta()
    {
        return FacingDirection switch
        {
            Direction.North => (0, 1),
            Direction.East => (1, 0),
            Direction.South => (0, -1),
            Direction.West => (-1, 0),
            _ => (0, 0),
        };
    }

    /// <summary>
    /// 두 기물 사이의 맨해튼 거리를 반환한다.
    /// </summary>
    public int ManhattanDistanceTo(PieceBase other)
    {
        if (other == null) return 0;
        return Mathf.Abs(GridX - other.GridX) + Mathf.Abs(GridY - other.GridY);
    }

    /// <summary>
    /// 대상이 이 기물의 정면 방향 직선상 사정거리 이내에 있는지 확인한다.
    /// </summary>
    public bool IsInLineOfFire(PieceBase target)
    {
        var (dx, dy) = GetFacingDelta();

        for (int i = 1; i <= _attackRange; i++)
        {
            if (GridX + dx * i == target.GridX && GridY + dy * i == target.GridY)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 정면 방향 직선에서 가장 가까운 기물을 반환한다 (진영 무관, 블로킹 포함).
    /// </summary>
    protected PieceBase FindClosestInLine(IReadOnlyList<PieceBase> allPieces)
    {
        var (dx, dy) = GetFacingDelta();
        for (int dist = 1; dist <= _attackRange; dist++)
        {
            int tx = GridX + dx * dist;
            int ty = GridY + dy * dist;
            foreach (var piece in allPieces)
            {
                if (piece == this || piece.IsDead) continue;
                if (piece.GridX == tx && piece.GridY == ty) return piece;
            }
        }
        return null;
    }

    /// <summary>
    /// 지정 좌표에 있는 살아있는 기물을 반환한다.
    /// </summary>
    protected static PieceBase FindPieceAtGrid(IReadOnlyList<PieceBase> allPieces, int x, int y)
    {
        foreach (var piece in allPieces)
            if (!piece.IsDead && piece.GridX == x && piece.GridY == y)
                return piece;
        return null;
    }

    /// <summary>
    /// other가 자신의 적 진영인지 확인한다 (중립은 적이 아님). AttackHitbox 등 외부에서도 사용 가능.
    /// </summary>

    public bool IsEnemyOf(PieceBase other)
    {
        if (Faction == Faction.Ally) return other.Faction == Faction.Enemy;
        if (Faction == Faction.Enemy) return other.Faction == Faction.Ally || other.Faction == Faction.Neutral;
        return false;
    }

    #region IWorldInputTarget

    public void OnWorldHover(WorldInputEventData eventData)
    {
        var allPieces = StageManager.Instance?.GetAllActivePieces();
        bool willAttack = allPieces != null && CheckCanActNow(allPieces);
        Color outlineColor = Faction == Faction.Neutral || willAttack ? Color.green : Color.red;
        GetComponent<OutlineEffect>()?.ShowWithColor(outlineColor);
        ShowAttackRangeCells();
    }
    public void OnWorldUnHover(WorldInputEventData eventData)
    {
        GetComponent<OutlineEffect>()?.Hide();
        StageManager.Instance?.ClearAttackRange();
        GameManager.Instance?.ClearAllRangeHighlights();
    }

    public static event System.Action<PieceBase> AllyRightClicked;
    public static event System.Action<PieceBase> AllyLeftClicked;

    public void OnWorldLeftClick(WorldInputEventData eventData)
    {
        // OpeningShot 타겟팅 중에는 적 선택만 받아야 하므로 아군 배치/회수 입력은 여기서 끊는다.
        if (ShouldBlockAllyClickDuringOpeningShotTargeting())
        {
            return;
        }

        SimulationController.Instance?.OnClickPiece(this);
        if (_faction != Faction.Ally) return;
        AllyLeftClicked?.Invoke(this);
    }

    public void OnWorldRightClick(WorldInputEventData eventData)
    {
        // 같은 차단을 우클릭에도 적용해 타겟팅 중 아군 회수나 방향 조작이 섞이지 않게 한다.
        if (ShouldBlockAllyClickDuringOpeningShotTargeting())
        {
            return;
        }

        SimulationController.Instance?.OnRightClickPiece(this);

        if (_faction != Faction.Ally) return;
        AllyRightClicked?.Invoke(this);
    }

    private bool ShouldBlockAllyClickDuringOpeningShotTargeting()
    {
        // 차단 범위를 아군 입력으로 한정해 적군 타겟 확정/해제 경로는 그대로 통과시킨다.
        if (_faction != Faction.Ally || SimulationController.Instance == null)
        {
            return false;
        }

        foreach (SkillBase skill in SimulationController.Instance.GetStageSkills())
        {
            OpeningShotSkill openingShotSkill = skill as OpeningShotSkill;
            if (openingShotSkill != null)
            {
                return openingShotSkill.isTargetingMode;
            }
        }

        return false;
    }

    #endregion

    public virtual void ShowAttackRangeCells()
    {
        if (GameManager.Instance == null) return;

        int boardSize = StageManager.Instance?.CurrentStageData?.boardSize ?? 6;
        var (dx, dy) = GetFacingDelta();
        var indices = new System.Collections.Generic.List<int>();

        for (int i = 1; i <= _attackRange; i++)
        {
            int tx = GridX + dx * i;
            int ty = GridY + dy * i;
            if (tx < 0 || ty < 0 || tx >= boardSize || ty >= boardSize) break;
            indices.Add(StageGridIndexUtility.ToCellIndex(boardSize, tx, ty));
        }
        if (CanAct)
        {
            StageManager.Instance.SetAttackRange(indices.ToArray());
            GameManager.Instance.ShowCellRangeHighlight(indices.ToArray());
        }
    }

    protected void FinishAction()
    {
        IsActionFinished = true;
        OnActionFinished?.Invoke(this);
    }

    protected IEnumerator WaitForSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
    }

    public Quaternion GetFacingRotation()
    {
        return DirectionToRotation(FacingDirection);
    }

    public static Quaternion DirectionToRotation(Direction direction)
    {
        return direction switch
        {
            Direction.North => Quaternion.Euler(0, 0, 0),
            Direction.East => Quaternion.Euler(0, 90, 0),
            Direction.South => Quaternion.Euler(0, 180, 0),
            Direction.West => Quaternion.Euler(0, 270, 0),
            _ => Quaternion.identity,
        };
    }

    private void ResetAnimatorState(bool snapToIdle)
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            return;
        }

        animator.speed = 1f;
        animator.applyRootMotion = false;
        _useManualRootMotion = false;
        _skipNextRootMotionDelta = false;
        _overrideRootMotionDirection = false;
        _rootMotionWorldDirection = Vector3.zero;
        _rootMotionDistanceScale = 1f;
        if (snapToIdle && animator.HasState(0, Animator.StringToHash("Idle")))
        {
            animator.Play("Idle", 0, 0f);
        }
    }

    protected void SetAnimatorRootMotion(bool enabled, bool skipNextDelta = false)
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = enabled;
        }

        _useManualRootMotion = enabled;
        _skipNextRootMotionDelta = enabled && skipNextDelta;
        _overrideRootMotionDirection = false;
        _rootMotionWorldDirection = Vector3.zero;
        _rootMotionDistanceScale = 1f;
    }

    protected void SetAnimatorRootMotionOverride(Vector3 worldDirection, float distanceScale, bool skipNextDelta = false)
    {
        SetAnimatorRootMotion(true, skipNextDelta);

        worldDirection.y = 0f;
        _rootMotionWorldDirection = worldDirection.sqrMagnitude > 0.0001f
            ? worldDirection.normalized
            : transform.forward;
        _rootMotionDistanceScale = Mathf.Max(0f, distanceScale);
        _overrideRootMotionDirection = true;
    }

    protected void SetAnimatorRootMotionVerticalOnly()
    {
        SetAnimatorRootMotionOverride(transform.forward, 0f);
    }

    private void OnAnimatorMove()
    {
        if (!_useManualRootMotion || _animator == null)
        {
            return;
        }

        if (_skipNextRootMotionDelta)
        {
            _skipNextRootMotionDelta = false;
            return;
        }

        if (_overrideRootMotionDirection)
        {
            float forwardDistance = Mathf.Abs(Vector3.Dot(_animator.deltaPosition, transform.forward));
            Vector3 horizontalDelta = _rootMotionWorldDirection * forwardDistance * _rootMotionDistanceScale;
            Vector3 verticalDelta = Vector3.up * _animator.deltaPosition.y;
            transform.position += horizontalDelta + verticalDelta;
            return;
        }

        transform.position += _animator.deltaPosition;
        transform.rotation *= _animator.deltaRotation;
    }

    private void ResetColliderState()
    {
        EndAttackHitboxes();

        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col.GetComponent<AttackHitbox>() != null)
            {
                continue;
            }

            col.enabled = true;
        }
    }

    public void EndAttackHitboxes()
    {
        foreach (var hitbox in GetComponentsInChildren<AttackHitbox>())
        {
            hitbox.EndAttack();
        }
    }

    private static bool HasTriggerParameter(Animator animator, string parameterName)
    {
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private static void ResetAnimatorTrigger(Animator animator, string parameterName)
    {
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
            {
                animator.ResetTrigger(parameterName);
                return;
            }
        }
    }
}
