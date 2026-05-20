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

    private void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
        _col.enabled = false;
    }

    public void Initialize(PieceBase owner)
    {
        _owner = owner;
    }

    public void BeginAttack()
    {
        _col.enabled = true;
    }

    public void EndAttack()
    {
        _col.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_owner == null) return;

        if (other.transform.root == transform.root) return;

        var piece = other.GetComponentInParent<PieceBase>();
        if (piece == null || piece.IsDead) return;
        if (!_owner.IsEnemyOf(piece)) return;

        piece.TakeDamage(1);
        EndAttack();
    }
}
