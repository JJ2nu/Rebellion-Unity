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

    private Vector3 _spawnWorldPos;
    private int _spawnGridX;
    private int _spawnGridY;
    private bool _spawnRecorded;

    private void OnDisable()
    {
        // 기물이 슬롯으로 되돌아갔을 때(비활성화 시) 기록을 초기화하여
        // 다음에 다시 배치될 때 새로운 위치를 정상적으로 기록하도록 합니다.
        _spawnRecorded = false;
    }

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();

        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                string n = clip.name.ToLower();
                if (n.Contains("knife01") || n.Contains("knife_01") || n.Contains("attack_01"))
                    _attack1ClipLength = clip.length;
                else if (n.Contains("knife02") || n.Contains("knife_02") || n.Contains("attack_02"))
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

        base.OnSimulationStart();
    }

    protected override PieceBase FindTarget(IReadOnlyList<PieceBase> allPieces)
    {
        var closest = FindClosestInLine(allPieces);
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

        int dist = ManhattanDistanceTo(target);
        bool is1Cell = dist <= 1;
        float clipLen = is1Cell ? _attack1ClipLength : _attack2ClipLength;

        int targetGX = target.GridX;
        int targetGY = target.GridY;

        int boardSize = StageManager.Instance?.CurrentStageData?.boardSize ?? 6;
        int cellIdx = StageGridIndexUtility.ToCellIndex(boardSize, targetGX, targetGY);
        Vector3 targetWorldPos = GameManager.Instance != null
            ? GameManager.Instance.GetCellPosition(cellIdx)
            : transform.position;

        if (_animator != null && clipLen > 0f)
            _animator.speed = (clipLen / stepDuration) * _animSpeedMultiplier;

        _animator?.SetTrigger(is1Cell ? "Attack" : "Attack2");

        Vector3 startPos = transform.position;
        float dashTime = stepDuration * _dashFraction;
        float elapsed = 0f;

        // 돌진 시간 동안 부드럽게 위치 이동
        while (elapsed < dashTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dashTime);
            // 빠르고 역동적인 느낌을 위한 Ease-Out 곡선 적용
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            
            transform.position = Vector3.Lerp(startPos, targetWorldPos, easeT);
            yield return null;
        }

        // 목적지 도착 완료 처리
        transform.position = targetWorldPos;
        GridX = targetGX;
        GridY = targetGY;

        if (!target.IsDead)
            target.TakeDamage(1);

        // 남은 stepDuration 동안 애니메이션 마무리 대기
        float remainingTime = stepDuration - dashTime;
        if (remainingTime > 0f)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        if (_animator != null) _animator.speed = 1f;
        FinishAction();
    }
}
