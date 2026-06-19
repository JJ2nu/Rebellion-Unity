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
    [SerializeField] private float _attackStepDistance = 0.65f;
    [SerializeField] private float _retreatDuration = 0.15f;
    private bool _isExecutingAttack;
    private Vector3 _attackStartPosition;

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

        Vector3 originalPosition = transform.position;
        _attackStartPosition = originalPosition;
        _isExecutingAttack = true;
        var (dx, dy) = GetFacingDelta();
        Vector3 attackDirection = new Vector3(dx, 0f, dy);
        Vector3 attackPosition = originalPosition + attackDirection * _attackStepDistance;

        _fistHitBox?.BeginAttack();
        SetAnimatorRootMotion(true,false);
        _animator?.SetTrigger("Attack");

        yield return MovePositionOverTime(originalPosition, attackPosition, stepDuration);

        if (_animator != null) _animator.speed = 1f;
        if (!IsDead)
        {
            yield return MovePositionOverTime(transform.position, originalPosition, _retreatDuration);
            SetAnimatorRootMotion(false);
        }
        _isExecutingAttack = false;
        FinishAction();
    }

    public override void Die()
    {
        if (IsDead)
        {
            return;
        }

        if (_isExecutingAttack)
        {
            transform.position = _attackStartPosition;
        }

        base.Die();
    }

    public override void ResetState()
    {
        _isExecutingAttack = false;
        base.ResetState();
    }

    private IEnumerator MovePositionOverTime(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            transform.position = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsDead)
            {
                yield break;
            }

            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(from, to, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!IsDead)
        {
            transform.position = to;
        }
    }
}
