using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건맨(총) 기물. 페이즈 3에서 행동.
/// 시뮬레이션 시작 순간 Shoot2로 조준 자세까지 올린 뒤 멈춰 기다리고, 자기 페이즈가 오면 바로 FlarePoint에서 bullet 생성.
/// 총알은 코드 이동이므로 첫 번째 충돌 기물에 TakeDamage.
/// </summary>
public class GunmanPiece : PieceBase
{
    [Header("Gunman Config")]
    [SerializeField] private Transform _flarePoint;
    [SerializeField] private Transform _flarePoint2;
    [SerializeField, Range(0.1f, 50f)] private float _bulletSpeedMultiplier = 2f; // 총알 이동 속도 조절용

    [SerializeField] private GameObject _bullet;

    // Animator가 없을 때 조준을 기다리는 시간(초).
    [SerializeField, Min(0f)] private float _sideAimFireDelay = 1.3f;
    // Shoot2 클립에서 총을 겨누고 멈춘 자세(1.07~1.18초) 중 반동 직전 지점. 조준 대기는 이 자세로 멈춘다.
    [SerializeField, Min(0f)] private float _aimHoldClipTime = 1.17f;
    // Shoot2 클립에서 반동이 시작되는 지점. 총알은 이 지점을 지나는 순간 생성된다.
    [SerializeField, Min(0f)] private float _recoilClipTime = 1.19f;
    [SerializeField] private float _sideAimYawOffset = -75f;
    // 준비 자세에서 총을 들어 올리는 속도 배율. 주먹·단검 동작 안에 조준이 끝나도록 원래 속도보다 빠르게 올린다.
    [SerializeField, Range(0.1f, 5f)] private float _readyAimSpeedMultiplier = 1.6f;
    // 조준 상태 진입을 기다리는 최대 시간. Animator 전이가 막혀도 발사가 멈추지 않게 하는 안전장치다.
    [SerializeField, Min(0.1f)] private float _aimTimeout = 3f;

    private const string AimStateName = "Shoot2";

    private bool _isAiming;
    private bool _isAimHeld;
    private bool _hasFired;
    private Quaternion _preAimRotation;
    private Coroutine _aimRoutine;

    private void Awake()
    {
        base.Awake();
        if (_bullet != null && _bullet.scene.IsValid())
        {
            _bullet.SetActive(false);
        }
    }

    public override int SimulationPhaseIndex => 3;

    public override bool HasReachedPhaseHandoff => _hasFired || IsActionFinished;

    /// <summary>총을 측면 조준 자세까지 들어 올리고 그 자세로 멈춰 발사 차례를 기다린다.</summary>
    public override void BeginReadyPose()
    {
        if (_isAiming || IsDead)
        {
            return;
        }

        _isAiming = true;
        _isAimHeld = false;
        _hasFired = false;
        _preAimRotation = transform.rotation;

        StageManager.Instance?.PlayGunReadySfx();
        transform.rotation = _preAimRotation * Quaternion.Euler(0f, _sideAimYawOffset, 0f);
        if (_animator != null)
        {
            _animator.speed = _readyAimSpeedMultiplier;
            _animator.SetTrigger(AimStateName);
        }

        _aimRoutine = StartCoroutine(RaiseAimAndHold());
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
        yield return Fire(allPieces, stepDuration);
    }

    /// <summary>외부(스킬 등)에서도 직접 발사 가능.</summary>
    public IEnumerator Fire(IReadOnlyList<PieceBase> allPieces, float stepDuration)
    {
        // 준비 자세 없이 바로 불린 경우(스킬 등)에도 같은 조준 과정을 거친다.
        if (!_isAiming)
        {
            BeginReadyPose();
        }

        while (!_isAimHeld && !IsDead)
        {
            yield return null;
        }

        // 전투 카메라가 권총 기물에 포커스를 잡은 뒤에 발사 모션을 시작한다.
        CombatPresentationDirector presentation = SimulationController.Instance != null
            ? SimulationController.Instance.GetComponent<CombatPresentationDirector>()
            : null;
        while (presentation != null && !presentation.IsGunmanFocusReady && !IsDead)
        {
            yield return null;
        }

        if (IsDead)
        {
            FinishAction();
            yield break;
        }

        Quaternion originalRotation = _preAimRotation;

        // 멈춰 둔 조준 자세에서 모션을 이어 재생하고, 반동이 시작되는 프레임에 총알을 낸다.
        if (_animator != null)
        {
            _animator.speed = 1f;
            float waitLimit = Time.time + 0.2f;
            while (!IsDead &&
                   Time.time < waitLimit &&
                   TryGetAimClipTime(out float clipTime) &&
                   clipTime < _recoilClipTime)
            {
                yield return null;
            }

            if (IsDead)
            {
                FinishAction();
                yield break;
            }
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

        // FlarePoint는 손 본에 붙어 측면 조준 자세를 따라 돌아가 있으므로, 총알 루트는 실제 발사 방향을 바라보게 만든다.
        // Bullet Prefab의 모델은 루트 +Z가 탄두 진행 방향이 되도록 배치돼 있다.
        Quaternion bulletRotation = Quaternion.LookRotation(fireDirection, Vector3.up);
        GameObject bulletInstance = Instantiate(_bullet, selectedFlarePoint.position, bulletRotation);
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
        _hasFired = true;
        SimulationController.Instance?.ReportProjectileSpawned(this, bulletController);
        yield return new WaitUntil(() => bulletController == null || !bulletController.IsFlying);

        if (!IsDead)
        {
            if (_animator != null) _animator.speed = 1f;
            transform.rotation = originalRotation;
            SetAnimatorRootMotion(false);
        }

        _isAiming = false;
        FinishAction();
    }

    private IEnumerator RaiseAimAndHold()
    {
        float startTime = Time.time;
        while (!IsDead && _isAiming && _animator != null && Time.time - startTime < _aimTimeout)
        {
            if (TryGetAimClipTime(out float clipTime) && clipTime >= _aimHoldClipTime)
            {
                break;
            }

            yield return null;
        }

        if (_animator == null && !IsDead)
        {
            // Animator가 없으면 원래 지연 시간만큼 기다린 것으로 본다.
            yield return new WaitForSeconds(_sideAimFireDelay / _readyAimSpeedMultiplier);
        }

        if (!IsDead && _isAiming && _animator != null)
        {
            // 배속 재생으로 한 프레임 넘게 지나쳤을 수 있으므로 정확히 반동 직전 자세로 되돌려 멈춘다.
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (!_animator.IsInTransition(0) && stateInfo.IsName(AimStateName) && stateInfo.length > 0f)
            {
                _animator.Play(AimStateName, 0, _aimHoldClipTime / stateInfo.length);
                _animator.Update(0f);
            }

            _animator.speed = 0f;
        }

        _isAimHeld = true;
        _aimRoutine = null;
    }

    private bool TryGetAimClipTime(out float clipTime)
    {
        clipTime = 0f;

        // Shoot2로 전이 중이면 다음 상태 정보를, 전이가 끝났으면 현재 상태 정보를 읽는다.
        AnimatorStateInfo stateInfo = _animator.IsInTransition(0)
            ? _animator.GetNextAnimatorStateInfo(0)
            : _animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(AimStateName))
        {
            return false;
        }

        clipTime = stateInfo.normalizedTime * stateInfo.length;
        return true;
    }

    private void ClearReadyState()
    {
        if (_aimRoutine != null)
        {
            StopCoroutine(_aimRoutine);
            _aimRoutine = null;
        }

        _isAiming = false;
        _isAimHeld = false;
        _hasFired = false;
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
