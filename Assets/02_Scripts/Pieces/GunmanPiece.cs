using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건맨(총) 기물. 페이즈 3에서 행동.
/// Shoot2 준비 모션 후 FlarePoint에서 bullet 생성.
/// 총알은 코드 이동이므로 첫 번째 충돌 기물에 TakeDamage.
/// </summary>
public class GunmanPiece : PieceBase
{
    [Header("Gunman Config")]
    [SerializeField] private Transform _flarePoint;
    [SerializeField] private Transform _flarePoint2;
    [SerializeField, Range(0.1f, 50f)] private float _bulletSpeedMultiplier = 2f; // 총알 이동 속도 조절용

    [SerializeField] private GameObject _bullet;

    [SerializeField, Min(0f)] private float _sideAimFireDelay = 1.3f;
    [SerializeField] private float _sideAimYawOffset = -75f;

    private void Awake()
    {
        base.Awake();
        if (_bullet != null && _bullet.scene.IsValid())
        {
            _bullet.SetActive(false);
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
        Quaternion originalRotation = transform.rotation;

        StageManager.Instance?.PlayGunReadySfx();
        transform.rotation = originalRotation * Quaternion.Euler(0f, _sideAimYawOffset, 0f);
        _animator?.SetTrigger("Shoot2");

        if (_sideAimFireDelay > 0f)
        {
            yield return new WaitForSeconds(_sideAimFireDelay);
        }

        Transform selectedFlarePoint = _flarePoint2 != null
            ? _flarePoint2
            : _flarePoint;

        if (_bullet == null || selectedFlarePoint == null)
        {
            if (_animator != null) _animator.speed = 1f;
            transform.rotation = originalRotation;
            SetAnimatorRootMotion(false);
            FinishAction();
            yield break;
        }

        var (dx, dy) = GetFacingDelta();
        Vector3 fireDirection = new Vector3(dx, 0f, dy);
        StageManager.Instance?.PlayGunFireSfx();
        PlayMuzzleFlash(selectedFlarePoint, fireDirection);

        GameObject bulletInstance = Instantiate(_bullet, selectedFlarePoint.position, selectedFlarePoint.rotation);
        bulletInstance.SetActive(true);

        AttackHitbox bulletHitBox = bulletInstance.GetComponent<AttackHitbox>();
        bulletHitBox?.Initialize(this);
        bulletHitBox?.SetAsBullet(true);
        bulletHitBox?.BeginAttack();

        BulletController bulletController = bulletInstance.GetComponent<BulletController>();
        if (bulletController == null)
        {
            Destroy(bulletInstance);
            if (_animator != null) _animator.speed = 1f;
            transform.rotation = originalRotation;
            SetAnimatorRootMotion(false);
            FinishAction();
            yield break;
        }

        bulletController.Fire(fireDirection, _bulletSpeedMultiplier);
        SimulationController.Instance?.ReportProjectileSpawned(this, bulletController);
        yield return new WaitUntil(() => bulletController == null || !bulletController.IsFlying);

        if (!IsDead)
        {
            if (_animator != null) _animator.speed = 1f;
            transform.rotation = originalRotation;
            SetAnimatorRootMotion(false);
        }

        FinishAction();
    }

    private static void PlayMuzzleFlash(Transform flarePoint, Vector3 fireDirection)
    {
        ParticleSystem muzzleFlash = flarePoint.GetComponentInChildren<ParticleSystem>(true);
        if (muzzleFlash == null)
        {
            return;
        }

        muzzleFlash.transform.localPosition = Vector3.zero;
        muzzleFlash.transform.rotation = Quaternion.LookRotation(fireDirection, Vector3.up);

        ParticleSystem.MainModule main = muzzleFlash.main;
        main.startSize = new ParticleSystem.MinMaxCurve(1.9f, 2.4f);
        main.startColor = new Color(7f, 3.2f, 1.1f, 1f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.09f);

        muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        muzzleFlash.Play(true);
    }
}
