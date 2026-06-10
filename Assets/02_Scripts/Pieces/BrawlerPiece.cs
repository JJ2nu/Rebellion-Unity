using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 브롤러(주먹) 기물. 페이즈 1에서 행동.
/// Attack 클립 길이를 stepDuration에 맞게 speed 조정 후 트리거.
/// </summary>
public class BrawlerPiece : PieceBase
{
    private AttackHitbox _fistHitBox;
    private float _attackClipLength = -1f;

    private void Awake()
    {
        base.Awake();
        _fistHitBox = GetComponentInChildren<AttackHitbox>();
        _fistHitBox?.Initialize(this);

        // Attack 클립 길이 미리 캐싱
        if (_animator != null)
        {
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.Contains("Attack") || clip.name.Contains("attack") || clip.name.Contains("Punch") || clip.name.Contains("punch"))
                {
                    _attackClipLength = clip.length;
                    break;
                }
            }
        }
    }

    public override int SimulationPhaseIndex => 1;

    protected override PieceBase FindTarget(IReadOnlyList<PieceBase> allPieces)
    {
        var closest = FindClosestInLine(allPieces);
        return (closest != null && IsEnemyOf(closest)) ? closest : null;
    }

    public override IEnumerator ExecuteAction(IReadOnlyList<PieceBase> allPieces, float stepDuration)
    {
        var target = FindTarget(allPieces);
        if (target == null)
        {
            FinishAction();
            yield break;
        }

        if (_animator != null && _attackClipLength > 0f)
            _animator.speed = _attackClipLength / stepDuration;

        _fistHitBox?.BeginAttack();
        _animator?.SetTrigger("Attack");

        // 애니메이션 절반 지점(주먹 뻗는 정점)에서 타격 판정
        yield return new WaitForSeconds(stepDuration );
        // if (!target.IsDead)
        //     target.TakeDamage(1);


        if (_animator != null) _animator.speed = 1f;
        FinishAction();
    }
}
