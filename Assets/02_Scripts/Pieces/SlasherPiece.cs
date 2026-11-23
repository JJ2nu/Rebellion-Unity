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
    private const string IdleStateName = "idle";

    [Header("Slasher Config")]
    [SerializeField, Range(0.1f, 10f)] private float _animSpeedMultiplier = 1f;
    [SerializeField] private float _slashRootMotionDistance = 3.5944f;
    [SerializeField, Min(0f)] private float _arrivalEaseDuration = 0.08f;

    [Header("Ready Pose")]
    // Slash.anim 0.20초(1.567초 중) 지점. 몸이 가장 낮아지고 돌진이 시작되기 직전 자세에서 멈춰 기다린다.
    [SerializeField, Range(0f, 1f)] private float _crouchHoldNormalizedTime = 0.128f;
    // 웅크리는 구간의 루트 이동 거리. 웅크린 자세에서 돌진을 이어갈 때 남은 거리로 배율을 다시 계산한다.
    [SerializeField, Min(0f)] private float _crouchRootMotionDistance = 0.28f;
    // 돌진 대부분이 끝난 지점(약 0.47초). 권총 기물은 이 지점부터 발사할 수 있다.
    [SerializeField, Range(0f, 1f)] private float _handoffNormalizedTime = 0.3f;


    private float _attackClipLength = 1.367f;

    private int _currentAttackRange = 1;
    private AttackHitbox _knifeHitBox;
    private bool _isCrouching;
    private bool _hasReachedHandoff;
    private Coroutine _crouchRoutine;

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

    public override bool HasReachedPhaseHandoff => _hasReachedHandoff || IsActionFinished;

    /// <summary>
    /// 돌진 직전의 낮은 자세까지만 공격 모션을 재생하고 멈춘다. 주먹 기물이 움직인 뒤 ExecuteAction이 같은 자세에서 이어 튀어나간다.
    /// </summary>
    public override void BeginReadyPose()
    {
        if (_animator == null || IsDead)
        {
            return;
        }

        StopCrouchRoutine();
        SetAnimatorRootMotion(false);
        _animator.speed = _animSpeedMultiplier;
        _animator.Play(AttackStateName, 0, 0f);
        _animator.Update(0f);
        _isCrouching = true;
        _crouchRoutine = StartCoroutine(HoldCrouchPose());
    }

    public override void OnSimulationStart()
    {
        ClearReadyState();
        base.OnSimulationStart();
    }

    public override void ResetState()
    {
        ClearReadyState();
        base.ResetState();
    }

    protected override PieceBase FindTarget(IReadOnlyList<PieceBase> allPieces)
    {
        var closest = FindClosestInLine(allPieces);
        return (closest != null && IsEnemyOf(closest)) ? closest : null;
    }

    public override IEnumerator ExecuteAction(IReadOnlyList<PieceBase> allPieces, float stepDuration)
    {
        bool resumeFromCrouch = _isCrouching && _animator != null;
        _isCrouching = false;
        _hasReachedHandoff = false;
        StopCrouchRoutine();

        // 표적은 출발 순간에 다시 찾는다. 앞선 주먹 공격으로 표적이 사라졌으면 웅크린 자세를 풀고 끝낸다.
        PieceBase target = FindTarget(allPieces);
        if (target == null)
        {
            if (resumeFromCrouch)
            {
                ReturnToIdlePose();
            }

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
        // 웅크린 자세에서 이어가면 웅크리는 구간의 루트 이동은 이미 지나갔으므로 남은 거리 기준으로 배율을 맞춘다.
        float remainingRootMotionDistance = resumeFromCrouch
            ? _slashRootMotionDistance - _crouchRootMotionDistance
            : _slashRootMotionDistance;
        float rootMotionScale = remainingRootMotionDistance > 0.01f
            ? desiredMoveDistance / remainingRootMotionDistance
            : 1f;

        StageManager.Instance?.PlaySlasherAttackSfx();
        _knifeHitBox?.BeginAttack();
        if (_animator != null)
        {
            // 웅크린 자세에서 이어갈 때는 루트 이동을 켜는 첫 프레임의 누적 이동을 버려 제자리 튐을 막는다.
            SetAnimatorRootMotionOverride(attackDirection, rootMotionScale, resumeFromCrouch);
            _animator.speed = _animSpeedMultiplier;
            if (!resumeFromCrouch)
            {
                _animator.Play(AttackStateName, 0, 0f);
                _animator.Update(0f);
            }

            yield return WaitForAttackAnimationEnd();
        }
        else
        {
            yield return new WaitForSeconds(_attackClipLength);
        }

        if (_animator != null)
        {
            _animator.speed = 1f;
        }
        if (!IsDead)
        {
            SetAnimatorRootMotion(false);
            yield return EaseToPosition(transform.position, targetCellPosition, _arrivalEaseDuration);
            GridX = targetGX;
            GridY = targetGY;

            if (_animator != null && _animator.HasState(0, Animator.StringToHash(IdleStateName)))
            {
                _animator.Play(IdleStateName, 0, 0f);
                _animator.Update(0f);
            }
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
                if (stateInfo.normalizedTime >= _handoffNormalizedTime)
                {
                    _hasReachedHandoff = true;
                }

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

    private IEnumerator EaseToPosition(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f || Vector3.SqrMagnitude(from - to) < 0.0001f)
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
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }

        if (!IsDead)
        {
            transform.position = to;
        }
    }

    private IEnumerator HoldCrouchPose()
    {
        while (_isCrouching && !IsDead)
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(AttackStateName) && stateInfo.normalizedTime >= _crouchHoldNormalizedTime)
            {
                _animator.speed = 0f;
                break;
            }

            yield return null;
        }

        _crouchRoutine = null;
    }

    private void ReturnToIdlePose()
    {
        if (_animator == null)
        {
            return;
        }

        _animator.speed = 1f;
        SetAnimatorRootMotion(false);
        if (_animator.HasState(0, Animator.StringToHash(IdleStateName)))
        {
            _animator.CrossFadeInFixedTime(IdleStateName, 0.15f, 0);
        }
    }

    private void StopCrouchRoutine()
    {
        if (_crouchRoutine != null)
        {
            StopCoroutine(_crouchRoutine);
            _crouchRoutine = null;
        }
    }

    private void ClearReadyState()
    {
        StopCrouchRoutine();
        _isCrouching = false;
        _hasReachedHandoff = false;
    }

    public void OnAttackAnimationEnd()
    {
    }
}
