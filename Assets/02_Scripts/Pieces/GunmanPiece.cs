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
    [SerializeField] private float _aimDelay = 0.5f;
    [SerializeField] private GameObject bulletPrefab;

    private Animator _animator;
    [SerializeField] private Transform _flarePoint;
    private float _attackClipLength;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();

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
        return (closest != null && IsEnemy(closest)) ? closest : null;
    }

    public override IEnumerator ExecuteAction(IReadOnlyList<PieceBase> allPieces, float stepDuration)
    {
        yield return Fire(allPieces, stepDuration);
    }

    /// <summary>외부(스킬 등)에서도 직접 발사 가능.</summary>
    public IEnumerator Fire(IReadOnlyList<PieceBase> allPieces, float stepDuration)
    {
        var target = FindTarget(allPieces);
        if (target == null)
        {
            FinishAction();
            yield break;
        }

        // 총알 스폰 위치: 셀 정중앙 X/Z + FlarePoint 높이
        float spawnY = _flarePoint != null ? _flarePoint.position.y : transform.position.y + 0.5f;
        int selfCellIdx = StageGridIndexUtility.ToCellIndex(StageManager.Instance?.CurrentStageData?.boardSize ?? 6, GridX, GridY);
        Vector3 cellCenter = GameManager.Instance != null
            ? GameManager.Instance.GetCellPosition(selfCellIdx)
            : transform.position;
        Vector3 spawnPos = new Vector3(cellCenter.x, spawnY, cellCenter.z);

        // 공격 애니메이션을 1스텝 안에 맞춰 재생 후 총알 발사
        if (_animator != null && _attackClipLength > 0f)
            _animator.speed = _attackClipLength / stepDuration;
        _animator?.SetTrigger("Attack");
        float waitTime = stepDuration;
        yield return new WaitForSeconds(waitTime);
        if (_animator != null)
            _animator.speed = 1f;

        // 총알 생성

        GameObject bullet = bulletPrefab != null
            ? Instantiate(bulletPrefab, spawnPos, transform.rotation)
            : null;

        if (bullet != null)
        {
            var bc = bullet.GetComponent<BulletController>();
            if (bc != null) bc.ShooterFaction = Faction;
        }

        // 총알을 1칸씩 이동하며 피격 체크
        var (dx, dy) = GetFacingDelta();
        int cx = GridX;
        int cy = GridY;
        int boardSize = StageManager.Instance?.CurrentStageData?.boardSize ?? 6;

        Vector3 bulletHeight = Vector3.up * spawnPos.y; // FlarePoint 높이 유지
        Vector3 currentPos = spawnPos;

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
            Vector3 targetPos = new Vector3(cellFloor.x, spawnPos.y, cellFloor.z);

            // 한 칸을 stepDuration/2 시간 동안 이동 (1스텝에 2칸)
            float elapsed = 0f;
            float cellDuration = stepDuration * 0.5f;
            while (elapsed < cellDuration)
            {
                elapsed += Time.deltaTime;
                if (bullet != null)
                    bullet.transform.position = Vector3.Lerp(currentPos, targetPos, elapsed / cellDuration);
                yield return null;
            }

            if (bullet != null)
                bullet.transform.position = targetPos;
            currentPos = targetPos;

            PieceBase hit = FindPieceAtGrid(allPieces, cx, cy);
            if (hit != null && !hit.IsDead)
                hit.Die();
        }

        if (bullet != null)
            Destroy(bullet);

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
}
