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
    [SerializeField, Range(0.1f, 50f)] private float _bulletSpeedMultiplier = 2f; // 총알 이동 속도 조절용

    [SerializeField] private GameObject _bullet;

    private float _attackClipLength;
    [SerializeField, Range(0.1f, 10f)] private float _fireMotionClipLength = 1.43f;

    private void Awake()
    {
        base.Awake();
        if (_bullet != null && _bullet.scene.IsValid())
        {
            _bullet.SetActive(false);
        }

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
        _animator?.SetTrigger("Attack");
        yield return new WaitForSeconds(_fireMotionClipLength);

        if (_bullet == null || _flarePoint == null)
        {
            FinishAction();
            yield break;
        }

        var (dx, dy) = GetFacingDelta();
        Vector3 fireDirection = new Vector3(dx, 0f, dy);
        GameObject bulletInstance = Instantiate(_bullet, _flarePoint.position, _flarePoint.rotation);
        bulletInstance.SetActive(true);

        AttackHitbox bulletHitBox = bulletInstance.GetComponent<AttackHitbox>();
        bulletHitBox?.Initialize(this);
        bulletHitBox?.SetAsBullet(true);
        bulletHitBox?.BeginAttack();

        BulletController bulletController = bulletInstance.GetComponent<BulletController>();
        if (bulletController == null)
        {
            Destroy(bulletInstance);
            FinishAction();
            yield break;
        }

        bulletController.Fire(fireDirection, _bulletSpeedMultiplier);
        yield return new WaitUntil(() => bulletController == null || !bulletController.IsFlying);

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
    }
}
