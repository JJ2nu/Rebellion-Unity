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
    [SerializeField, Range(0.1f, 2f)] private float _animSpeedMultiplier = 0.8f; // 애니메이션 속도 조절용

    private Animator _animator;
    private float _attack1ClipLength = -1f;
    private float _attack2ClipLength = -1f;

    private bool _attackAnimEnded =false;
    private Vector3 targetWorldPos;
    private Vector3 _spawnWorldPos;
    private int _spawnGridX;
    private int _spawnGridY;
    private bool _spawnRecorded;

    private int _currentAttackRange = 1;

    private AttackHitbox _knifeHitBox;

    private void OnDisable()
    {
        // 기물이 슬롯으로 되돌아갔을 때(비활성화 시) 기록을 초기화하여
        // 다음에 다시 배치될 때 새로운 위치를 정상적으로 기록하도록 합니다.
        _spawnRecorded = false;
    }

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _knifeHitBox = GetComponentInChildren<AttackHitbox>();
        _knifeHitBox?.Initialize(this);
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                string n = clip.name.ToLower();
                if (n.Contains("range1") || n.Contains("knife_01") || n.Contains("attack_01"))
                    _attack1ClipLength = clip.length;
                else if (n.Contains("range2") || n.Contains("knife_02") || n.Contains("attack_02"))
                    _attack2ClipLength = clip.length;
            }
        }

    }

    public override int SimulationPhaseIndex => 2;

    public override void OnSimulationStart()
    {

        // 시뮬레이션 최초 시작 시 배치 위치 기록
        if (!_spawnRecorded)
        {
            _spawnWorldPos = transform.position;
            _spawnGridX = GridX;
            _spawnGridY = GridY;
            _spawnRecorded = true;
        }
        else
        {
            // 리셋: 초기 배치 위치로 복원
            transform.position = _spawnWorldPos;
            GridX = _spawnGridX;
            GridY = _spawnGridY;
        }
            _attackAnimEnded = false;

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
        int dist = Mathf.Abs(dx) + Mathf.Abs(dy);
        bool is1Cell = dist <= 1;

        int targetGX = target.GridX - Mathf.Clamp(dx, -1, 1);
        int targetGY = target.GridY - Mathf.Clamp(dy, -1, 1);
        targetWorldPos = target.transform.position;
        _attackAnimEnded = false;
        _knifeHitBox?.BeginAttack();
        _animator?.SetTrigger(is1Cell ? "Attack" : "Attack2");

        yield return new WaitUntil(() => _attackAnimEnded == true);
        transform.position = targetWorldPos;
        GridX = targetGX;
        GridY = targetGY;

        // if (!target.IsDead)
        //     target.TakeDamage(1);

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

    public void OnAttackAnimationEnd()
    {
        _attackAnimEnded = true;
    }
    
}
