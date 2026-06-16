using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 슬래셔(단검) 기물. 페이즈 2에서 행동.
/// 가장 가까운 적(최대 2칸)을 향해 돌진, 도착 시 데미지 → 해당 칸을 점령한다.
/// </summary>
public class SlasherPiece : PieceBase
{
    [Header("Slasher Config")]
    [SerializeField, Range(0f, 1f)] private float _dashFraction = 0.6f;
    [SerializeField, Min(0f)] private float _windupEndTime = 0.08f;
    [SerializeField, Min(0f)] private float _slashMoveEndTime = 0.3f;
    [SerializeField, Min(0f)] private float _settleEndTime = 0.42f;
    [SerializeField, Range(0.1f, 10f)] private float _animSpeedMultiplier = 1f;


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
        int targetGX = target.GridX;
        int targetGY = target.GridY;
        Vector3 startWorldPos = transform.position;
        Vector3 targetWorldPos = GetCellWorldPosition(targetGX, targetGY, target.transform.position);
        float moveDuration = Mathf.Max(0.01f, _attackClipLength / _animSpeedMultiplier);

        _knifeHitBox?.BeginAttack();
        if (_animator != null)
        {
            _animator.speed = _animSpeedMultiplier;
        }
        _animator?.SetTrigger("Attack");

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float animationTime = elapsed * _animSpeedMultiplier;
            float moveProgress = EvaluateMoveProgress(animationTime);
            transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, moveProgress);
            yield return null;
        }

        transform.position = targetWorldPos;
        GridX = targetGX;
        GridY = targetGY;
        if (_animator != null)
        {
            _animator.speed = 1f;
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

    private float EvaluateMoveProgress(float animationTime)
    {
        if (animationTime <= _windupEndTime)
        {
            float t = Mathf.Clamp01(animationTime / Mathf.Max(0.01f, _windupEndTime));
            return Mathf.Lerp(0f, 0.08f, t);
        }

        if (animationTime <= _slashMoveEndTime)
        {
            float t = Mathf.Clamp01((animationTime - _windupEndTime) / Mathf.Max(0.01f, _slashMoveEndTime - _windupEndTime));
            t = 1f - Mathf.Pow(1f - t, 3f);
            return Mathf.Lerp(0.08f, 0.95f, t);
        }

        if (animationTime <= _settleEndTime)
        {
            float t = Mathf.Clamp01((animationTime - _slashMoveEndTime) / Mathf.Max(0.01f, _settleEndTime - _slashMoveEndTime));
            t = t * t * (3f - 2f * t);
            return Mathf.Lerp(0.95f, 1f, t);
        }

        return 1f;
    }

    public void OnAttackAnimationEnd()
    {
    }
}
