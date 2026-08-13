using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fist/Knife 오브젝트에 붙이는 근접 공격 판정 컴포넌트.
/// BeginAttack/EndAttack으로 판정 윈도우를 제어하며,
/// 충돌한 적 PieceBase에 데미지를 1 준다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AttackHitbox : MonoBehaviour
{
    private PieceBase _owner;
    private Collider _col;
    private BladeMeshTrail[] _bladeTrails;
    private readonly HashSet<PieceBase> _hitPieces = new();

    private bool _isBullet = false;

    private void Awake()
    {

    }

    public void Initialize(PieceBase owner)
    {
        _owner = owner;
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
        _col.enabled = false;
        _bladeTrails = GetComponentsInChildren<BladeMeshTrail>(true);
        if ((_bladeTrails == null || _bladeTrails.Length == 0) && _owner != null)
        {
            _bladeTrails = _owner.GetComponentsInChildren<BladeMeshTrail>(true);
        }
        SetBladeTrailsActive(false);
    }

    public void BeginAttack()
    {
        _hitPieces.Clear();
        _col.enabled = true;
        SetBladeTrailsActive(true);
    }

    public void EndAttack()
    {
        _col.enabled = false;
        _hitPieces.Clear();
        SetBladeTrailsActive(false);
    }
    public void SetAsBullet(bool isBullet)
    {
        _isBullet = isBullet;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryHit(other);
    }

    private void TryHit(Collider other)
    {
        if (_owner == null) return;

        if(other.CompareTag("Wall"))
        {
            //TODO: 총알이 벽에 맞았을 때 효과음, 파티클 등 추가 가능
            EndAttack();
            return;
        }

        var piece = other.GetComponentInParent<PieceBase>();
        if (piece == null || piece.IsDead) return;
        if (piece == _owner) return; // 자기 자신은 공격하지 않음
        if (_hitPieces.Contains(piece)) return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 impactDirection = GetImpactDirection(hitPoint, piece);
        HitImpactAttackType attackType = GetAttackType();

        _hitPieces.Add(piece);
        Direction hitDirection = GetIncomingDirection(impactDirection);
        bool isLethal = piece.CurrentHealth <= 1;
        StageManager.Instance?.PlayPieceHitSfx(piece);
        SimulationController.Instance?.ReportHitConfirmed(
            _owner,
            piece,
            hitPoint,
            impactDirection,
            attackType,
            damage: 1,
            isLethal: isLethal);

        // PieceBase.Die에서 전환 시점을 계산하기 전에 피격 종류와 방향을 넘긴다.
        // 사망 이벤트 이후에만 전달하면 둔기 전용 전환 시점을 고르지 못하고,
        // 일부 배치에서는 발이 바닥 안쪽인 이른 자세에서 물리가 시작된다.
        if (isLethal)
        {
            piece.GetComponent<HumanoidRagdollController>()
                ?.SetPendingImpact(hitPoint, impactDirection, attackType);
        }

        piece.TakeDamage(1,hitDirection);
        StageManager.Instance?.PlayHitImpact(hitPoint, impactDirection, attackType);
    }

    private Direction GetIncomingDirection(Vector3 impactDirection)
    {
        // impactDirection은 공격자에서 피해자 쪽으로 향한다.
        // 사망 모션은 공격이 들어온 쪽을 바라보도록 반대 벡터를 사용한다.
        Vector3 incomingDirection = -impactDirection;
        incomingDirection.y = 0f;
        if (incomingDirection.sqrMagnitude <= 0.0001f && _owner != null)
        {
            incomingDirection = _owner.transform.position - transform.position;
            incomingDirection.y = 0f;
        }

        if (Mathf.Abs(incomingDirection.x) > Mathf.Abs(incomingDirection.z))
        {
            return incomingDirection.x >= 0f ? Direction.East : Direction.West;
        }

        return incomingDirection.z >= 0f ? Direction.North : Direction.South;
    }

    private void SetBladeTrailsActive(bool isActive)
    {
        if (_bladeTrails == null)
        {
            return;
        }

        foreach (var bladeTrail in _bladeTrails)
        {
            if (bladeTrail == null)
            {
                continue;
            }

            if (isActive)
            {
                bladeTrail.ResetTrail();
            }

            bladeTrail.Emitting = isActive;
            bladeTrail.SetVisible(isActive);
        }
    }

    private Vector3 GetImpactDirection(Vector3 hitPoint, PieceBase hitPiece)
    {
        if (_isBullet)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f)
            {
                return rb.linearVelocity.normalized;
            }

            return transform.forward.sqrMagnitude > 0.0001f
                ? transform.forward.normalized
                : Vector3.forward;
        }

        // 주먹은 휘두르는 손의 접선 방향을 쓰면 공격 진행 방향 기준 좌우로 크게 샐 수 있다.
        // 둔기는 공격자 중심에서 피해자 중심으로 향하는 수평 방향을 사용해 뒤로 밀리게 한다.
        if (_owner is BrawlerPiece && hitPiece != null)
        {
            Vector3 ownerToTarget = hitPiece.transform.position - _owner.transform.position;
            ownerToTarget.y = 0f;
            if (ownerToTarget.sqrMagnitude > 0.0001f)
            {
                return ownerToTarget.normalized;
            }
        }

        Vector3 collisionVector = hitPoint - transform.position;
        if (collisionVector.sqrMagnitude > 0.0001f)
        {
            return (collisionVector + Vector3.up * 0.15f).normalized;
        }

        if (_owner != null && hitPiece != null)
        {
            Vector3 ownerToTarget = hitPiece.transform.position - _owner.transform.position;
            if (ownerToTarget.sqrMagnitude > 0.0001f)
            {
                return (ownerToTarget + Vector3.up * 0.15f).normalized;
            }
        }

        return _owner != null && _owner.transform.forward.sqrMagnitude > 0.0001f
            ? (_owner.transform.forward + Vector3.up * 0.15f).normalized
            : Vector3.forward;
    }

    private HitImpactAttackType GetAttackType()
    {
        if (_isBullet)
        {
            return HitImpactAttackType.Projectile;
        }

        if (_owner is BrawlerPiece)
        {
            return HitImpactAttackType.Blunt;
        }

        return HitImpactAttackType.Slash;
    }
}
