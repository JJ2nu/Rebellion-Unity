using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 슬래셔(단검) 기물. 페이즈 2에서 행동.
/// 가장 가까운 적(최대 2칸)을 향해 돌진, 도착 시 데미지 → 해당 칸을 점령한다.
/// </summary>
public class SlasherPiece : PieceBase
{
    private const string AttackStateName = "Attack";

    [Header("Slasher Config")]
    [SerializeField, Range(0.1f, 10f)] private float _animSpeedMultiplier = 1f;
    [SerializeField] private float _slashRootMotionDistance = 3.5944f;


    private float _attackClipLength = 1.367f;

    private int _currentAttackRange = 1;
    private AttackHitbox _knifeHitBox;

    private void Awake()
    {
        base.Awake();
        _knifeHitBox = GetComponentInChildren<AttackHitbox>();
        _knifeHitBox?.Initialize(this);
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                string n = clip.name.ToLower();
                if (n.Contains("attack") || n.Contains("knife"))
                {
                    _attackClipLength = clip.length;
                    break;
                }
            }
        }

    }

    public override int SimulationPhaseIndex => 2;

    public override void OnSimulationStart()
    {
        base.OnSimulationStart();
    }

    protected override PieceBase FindTarget(IReadOnlyList<PieceBase> allPieces)
    {
        var closest = FindClosestInLine(allPieces);
        return (closest != null && IsEnemyOf(closest)) ? closest : null;
    }

    public override IEnumerator ExecuteAction(IReadOnlyList<PieceBase> allPieces, float stepDuration)
    {
        PieceBase target = FindTarget(allPieces);
        if (target == null)
        {
            FinishAction();
            yield break;
        }

        int dx = target.GridX - GridX;
        int dy = target.GridY - GridY;
        int distance = Mathf.Abs(dx) + Mathf.Abs(dy);
        float worldDistance = Vector3.Distance(transform.position, target.transform.position);
        Debug.Log($"[Slasher] {name} ({GridX},{GridY}) attacks {target.name} ({target.GridX},{target.GridY}) gridDistance={distance}, worldDistance={worldDistance:F2}", this);

        int targetGX = target.GridX;
        int targetGY = target.GridY;
        var (facingDx, facingDy) = GetFacingDelta();
        Vector3 attackDirection = new Vector3(facingDx, 0f, facingDy);
        Vector3 startCellPosition = GetCellWorldPosition(GridX, GridY, transform.position);
        Vector3 targetCellPosition = GetCellWorldPosition(targetGX, targetGY, target.transform.position);
        float desiredMoveDistance = Vector3.Distance(startCellPosition, targetCellPosition);
        float rootMotionScale = _slashRootMotionDistance > 0f
            ? desiredMoveDistance / _slashRootMotionDistance
            : 1f;

        _knifeHitBox?.BeginAttack();
        if (_animator != null)
        {
            SetAnimatorRootMotionOverride(attackDirection, rootMotionScale);
            _animator.speed = _animSpeedMultiplier;
            _animator.Play(AttackStateName, 0, 0f);
            _animator.Update(0f);
            yield return WaitForAttackAnimationEnd();
        }
        else
        {
            yield return new WaitForSeconds(_attackClipLength);
        }

        _knifeHitBox?.EndAttack();
        GridX = targetGX;
        GridY = targetGY;
        if (_animator != null)
        {
            _animator.speed = 1f;
        }
        if (!IsDead)
        {
            SetAnimatorRootMotion(false);
        }
        FinishAction();
    }

    public override void ShowAttackRangeCells()
    {
        if (GameManager.Instance == null) return;

        int boardSize = StageManager.Instance?.CurrentStageData?.boardSize ?? 6;
        var (dx, dy) = GetFacingDelta();
        var indices = new System.Collections.Generic.List<int>();
        _currentAttackRange = ManhattanDistanceTo( FindTarget(StageManager.Instance?.GetAllActivePieces()));   

        for (int i = 0; i <= _currentAttackRange; i++)
        {
            int tx = GridX + dx * i;
            int ty = GridY + dy * i;
            if (tx < 0 || ty < 0 || tx >= boardSize || ty >= boardSize) break;
            indices.Add(StageGridIndexUtility.ToCellIndex(boardSize, tx, ty));
        }
        if (CanAct)
        {
            StageManager.Instance.SetAttackRange(indices.ToArray());
            GameManager.Instance.ShowMoveRangeHighlight(indices.ToArray(), GetFacingRotation());
        }
    }

    private Vector3 GetCellWorldPosition(int gridX, int gridY, Vector3 fallback)
    {
        int boardSize = StageManager.Instance?.CurrentStageData?.boardSize ?? 6;
        int cellIdx = StageGridIndexUtility.ToCellIndex(boardSize, gridX, gridY);
        return GameManager.Instance != null
            ? GameManager.Instance.GetCellPosition(cellIdx)
            : fallback;
    }

    private IEnumerator WaitForAttackAnimationEnd()
    {
        bool enteredAttackState = false;

        while (true)
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(AttackStateName))
            {
                enteredAttackState = true;
                if (stateInfo.normalizedTime >= 1f)
                {
                    break;
                }
            }
            else if (enteredAttackState)
            {
                break;
            }

            yield return null;
        }
    }

    public void OnAttackAnimationEnd()
    {
    }
}
