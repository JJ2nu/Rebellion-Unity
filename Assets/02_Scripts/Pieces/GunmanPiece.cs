using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건맨(총) 기물. 페이즈 3에서 행동.
/// "Attack" 트리거 → _aimDelay 후 FlarePoint에서 bullet 생성 → _fireDelay마다 1칸 이동.
/// 총알은 코드 이동이므로 첫 번째 충돌 기물에 TakeDamage.
/// </summary>
public class GunmanPiece : PieceBase
{
    [Header("Gunman Config")]
    [SerializeField] private Transform _flarePoint;
    [SerializeField, Range(0.1f, 10f)] private float _bulletSpeedMultiplier = 2f; // 총알 이동 속도 조절용

    [SerializeField] private GameObject _bullet;

    private AttackHitbox _bulletHitBox;
    private float _attackClipLength;
    [SerializeField, Range(0.1f, 10f)] private float _fireMotionClipLength = 1.1f;

    private void Awake()
    {
        base.Awake();
        _bullet?.SetActive(false);
        _bulletHitBox = _bullet?.GetComponent<AttackHitbox>();
        _bulletHitBox?.Initialize(this);
        _bulletHitBox?.SetAsBullet(true);

        if (_animator != null)
        {
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.Contains("Attack") || clip.name.Contains("Shoot") || clip.name.Contains("Fire"))
                {
                    _attackClipLength = clip.length;
                    break;
                }
            }
        }
    }

    public override int SimulationPhaseIndex => 3;

    protected override PieceBase FindTarget(IReadOnlyList<PieceBase> allPieces)
    {
        var closest = FindClosestInLine(allPieces);
        return (closest != null && IsEnemyOf(closest)) ? closest : null;
    }

    public override IEnumerator ExecuteAction(IReadOnlyList<PieceBase> allPieces, float stepDuration)
    {
        yield return Fire(allPieces, stepDuration);
    }

    /// <summary>외부(스킬 등)에서도 직접 발사 가능.</summary>
    public IEnumerator Fire(IReadOnlyList<PieceBase> allPieces, float stepDuration)
    {
        // 총알 스폰 위치: 셀 정중앙 X/Z + FlarePoint 높이
        float spawnY = _flarePoint != null ? _flarePoint.position.y : transform.position.y + 0.5f;
        int selfCellIdx = StageGridIndexUtility.ToCellIndex(StageManager.Instance?.CurrentStageData?.boardSize ?? 6, GridX, GridY);
        Vector3 cellCenter = GameManager.Instance != null
            ? GameManager.Instance.GetCellPosition(selfCellIdx)
            : transform.position;

        // 공격 애니메이션을 1스텝 안에 맞춰 재생 후 총알 발사
        // if (_animator != null && _attackClipLength > 0f)
        //     _animator.speed = _attackClipLength / stepDuration;
        _animator?.SetTrigger("Attack");
        float waitTime = stepDuration;
        yield return new WaitForSeconds(_fireMotionClipLength);

        // // 총알 생성
        _bullet.transform.position = _flarePoint.position;
        _bulletHitBox?.BeginAttack();
        _bullet.SetActive(true);


        // 총알을 1칸씩 이동하며 피격 체크
        var (dx, dy) = GetFacingDelta();
        int cx = GridX;
        int cy = GridY;
        int boardSize = StageManager.Instance?.CurrentStageData?.boardSize ?? 6;

        Vector3 bulletHeight = Vector3.up * _flarePoint.position.y; // FlarePoint 높이 유지
        Vector3 currentPos = _flarePoint.position;

        for (int step = 0; step < AttackRange; step++)
        {
            cx += dx;
            cy += dy;

            if (cx < 0 || cy < 0 || cx >= boardSize || cy >= boardSize)
                break;

            int cellIdx = StageGridIndexUtility.ToCellIndex(boardSize, cx, cy);
            Vector3 cellFloor = GameManager.Instance != null
                ? GameManager.Instance.GetCellPosition(cellIdx)
                : Vector3.zero;
            Vector3 targetPos = new Vector3(cellFloor.x, _flarePoint.position.y, cellFloor.z);

            // 한 칸을 stepDuration/2 시간 동안 이동 (1스텝에 2칸)
            float elapsed = 0f;
            float cellDuration = stepDuration;
            while (elapsed < cellDuration)
            {
                elapsed += Time.deltaTime * _bulletSpeedMultiplier;
                _bullet.transform.position = Vector3.Lerp(currentPos, targetPos, elapsed / cellDuration);
                yield return null;
            }

            if (_bullet != null)
                _bullet.transform.position = targetPos;
            currentPos = targetPos;

        }

        if (_bullet.transform.position.x > 5f || _bullet.transform.position.x < -5f || _bullet.transform.position.z > 5f || _bullet.transform.position.z < -5f)
        {
            _bulletHitBox?.EndAttack();
            _bullet.SetActive(false);
            _bullet.transform.position = _flarePoint.position;
        }

        FinishAction();
    }
    // ─── Helper ─────────────────────────────────────────────────────

    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }
    public override void ResetState()
    {
        base.ResetState();
        if (_bullet != null)
        {
            _bulletHitBox?.EndAttack();
            _bullet.SetActive(false);
            _bullet.transform.position = _flarePoint.position;
        }
    }
}
