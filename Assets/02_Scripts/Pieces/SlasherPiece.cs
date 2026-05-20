using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 슬래셔(단검) 기물. 페이즈 2에서 행동.
/// "Attack" 애니메이터 트리거 → _slashDelay 후 Knife 히트박스 활성화 → 충돌로 데미지.
/// 대상이 죽으면 해당 칸을 점령한다.
/// </summary>
public class SlasherPiece : PieceBase
{
    [Header("Slasher Config")]
    [SerializeField] private float _slashDelay = 0.3f;

    private Animator _animator;
    private AttackHitbox _knifeHitbox;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _knifeHitbox = GetComponentInChildren<AttackHitbox>();
        _knifeHitbox?.Initialize(this);
    }

    public override int SimulationPhaseIndex => 2;

    public override void OnSimulationStart()
    {
        base.OnSimulationStart();
    }

    protected override PieceBase FindTarget(IReadOnlyList<PieceBase> allPieces)
    {
        PieceBase closest = FindClosestInLine(allPieces);
        return (closest != null && IsEnemy(closest)) ? closest : null;
    }

    public override IEnumerator ExecuteAction(IReadOnlyList<PieceBase> allPieces, float stepDuration)
    {
        var target = FindTarget(allPieces);
        if (target == null)
        {
            FinishAction();
            yield break;
        }

        _animator?.SetTrigger("Attack");

        yield return new WaitForSeconds(_slashDelay);

        _knifeHitbox?.BeginAttack();

        yield return new WaitForSeconds(stepDuration - _slashDelay > 0
            ? stepDuration - _slashDelay
            : 0.05f);

        _knifeHitbox?.EndAttack();

        // 대상이 죽었으면 해당 칸 점령
        if (target.IsDead)
        {
            GridX = target.GridX;
            GridY = target.GridY;
        }

        FinishAction();
    }
}
