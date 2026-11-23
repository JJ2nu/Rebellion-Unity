using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 브롤러(주먹) 기물. 페이즈 1에서 행동.
/// Attack 클립 길이를 stepDuration에 맞게 speed 조정 후 트리거.
/// </summary>
public class BrawlerPiece : PieceBase
{
    [Header("Attack Movement")]
    [SerializeField] private float _attackStepDistance = 0.65f;
    [SerializeField, Range(0f, 1f)] private float _advanceEndNormalizedTime = 0.52f;
    [SerializeField, Range(0f, 1f)] private float _retreatStartNormalizedTime = 0.62f;
    [SerializeField, Range(0f, 1f)] private float _retreatEndNormalizedTime = 0.95f;

    private AttackHitbox _fistHitBox;
    private float _attackClipLength = -1f;
    private bool _isExecutingAttack;
    // 주먹이 앞으로 뻗어 타격 지점에 닿았는지. 단검 기물은 이 순간에 맞춰 튀어나간다.
    private bool _hasReachedHandoff;
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

    public override bool HasReachedPhaseHandoff => _hasReachedHandoff || IsActionFinished;

    protected override PieceBase FindTarget(IReadOnlyList<PieceBase> allPieces)
    {
        var closest = FindClosestInLine(allPieces);
        return (closest != null && IsEnemyOf(closest)) ? closest : null;
    }

    public override IEnumerator ExecuteAction(IReadOnlyList<PieceBase> allPieces, float stepDuration)
    {
        _hasReachedHandoff = false;
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

        StageManager.Instance?.PlayBrawlerAttackSfx();
        _fistHitBox?.BeginAttack();
        SetAnimatorRootMotion(false);
        _animator?.SetTrigger("Attack");

        var (dx, dy) = GetFacingDelta();
        Vector3 attackDirection = new Vector3(dx, 0f, dy);
        Vector3 attackPosition = originalPosition + attackDirection * _attackStepDistance;
        float advanceEnd = Mathf.Clamp01(_advanceEndNormalizedTime);
        float retreatStart = Mathf.Clamp01(Mathf.Max(_retreatStartNormalizedTime, advanceEnd));
        float retreatEnd = Mathf.Clamp01(Mathf.Max(_retreatEndNormalizedTime, retreatStart));

        float advanceDuration = stepDuration * advanceEnd;
        float holdDuration = stepDuration * (retreatStart - advanceEnd);
        float retreatDuration = stepDuration * (retreatEnd - retreatStart);
        float remainingDuration = stepDuration * (1f - retreatEnd);

        if (advanceDuration > 0f)
        {
            yield return MovePositionOverTime(originalPosition, attackPosition, advanceDuration);
        }
        else
        {
            transform.position = attackPosition;
        }

        _hasReachedHandoff = true;

        if (IsDead)
        {
            FinishAttackExecution();
            yield break;
        }

        if (holdDuration > 0f)
        {
            yield return WaitForAttackRemainingTime(holdDuration);
        }

        if (IsDead)
        {
            FinishAttackExecution();
            yield break;
        }

        if (retreatDuration > 0f)
        {
            yield return MovePositionOverTime(attackPosition, originalPosition, retreatDuration);
        }
        else
        {
            transform.position = originalPosition;
        }

        if (IsDead)
        {
            FinishAttackExecution();
            yield break;
        }

        if (remainingDuration > 0f)
        {
            yield return WaitForAttackRemainingTime(remainingDuration);
        }

        if (IsDead)
        {
            FinishAttackExecution();
            yield break;
        }

        if (!IsDead)
        {
            transform.position = originalPosition;
        }
        FinishAttackExecution();
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
        _hasReachedHandoff = false;
        base.ResetState();
    }

    public override void OnSimulationStart()
    {
        _hasReachedHandoff = false;
        base.OnSimulationStart();
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

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        if (!IsDead)
        {
            transform.position = to;
        }
    }

    private IEnumerator WaitForAttackRemainingTime(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsDead)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void FinishAttackExecution()
    {
        if (_animator != null) _animator.speed = 1f;
        SetAnimatorRootMotion(false);
        _isExecutingAttack = false;
        FinishAction();
    }
}
