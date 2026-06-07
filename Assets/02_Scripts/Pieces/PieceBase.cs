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
    public GameObject _HUD{get; set; }
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

    private void Start()
    {
        _HUD = transform.Find("HUD")?.gameObject;

        _HUD?.SetActive(true);
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = true;
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
        CurrentHealth = _maxHealth;
        IsDead = false;
        IsActionFinished = false;
        CanAct = false;
        PhaseOffset = 0;

        foreach (var col in GetComponentsInChildren<Collider>())
        {
            // AttackHitbox 전용 콜라이더는 BeginAttack/EndAttack으로만 제어
            if (col.GetComponent<AttackHitbox>() != null) continue;
            col.enabled = true;
        }
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

    public virtual void TakeDamage(int damage)
    {
        if (IsDead) return;

        CurrentHealth -= damage;
        OnDamageTaken?.Invoke(this, damage);

        if (CurrentHealth <= 0)
            Die();
    }

    public virtual void Die()
    {
        if (IsDead) return;

        IsDead = true;
        IsActionFinished = true;

        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        PlayDeathAnimation();
        OnDied?.Invoke(this);
    }

    protected virtual void PlayDeathAnimation()
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator == null) return;
        // Play()는 HasExitTime/Transition 완전 무시하고 즉시 강제 재생
        animator.Play("Hit", 0, 0f);
    }


    public void ResetState()
    {
        CurrentHealth = _maxHealth;
        IsDead = false;
        IsActionFinished = false;
        CanAct = false;
        PhaseOffset = 0;
        var animator = GetComponentInChildren<Animator>();

        animator?.Play("Idle", 0, 0f);
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = true;
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
        if (Faction == Faction.Enemy) return other.Faction == Faction.Ally;
        return false;
    }

    #region IWorldInputTarget

    public void OnWorldHover(WorldInputEventData eventData)
    {
        var allPieces = StageManager.Instance?.GetAllActivePieces();
        bool willAttack = allPieces != null && CheckCanActNow(allPieces);
        Color outlineColor = willAttack ? Color.green : Color.red;
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
        SimulationController.Instance?.OnClickPiece(this);
        if (_faction != Faction.Ally) return;
        AllyLeftClicked?.Invoke(this);
    }

    public void OnWorldRightClick(WorldInputEventData eventData)
    {
        if (_faction != Faction.Ally) return;
        AllyRightClicked?.Invoke(this);
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
        return FacingDirection switch
        {
            Direction.North => Quaternion.Euler(0, 0, 0),
            Direction.East => Quaternion.Euler(0, 90, 0),
            Direction.South => Quaternion.Euler(0, 180, 0),
            Direction.West => Quaternion.Euler(0, 270, 0),
            _ => Quaternion.identity,
        };
    }
}
