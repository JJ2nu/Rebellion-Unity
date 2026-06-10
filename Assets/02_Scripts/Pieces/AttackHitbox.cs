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

    private bool _isBullet = false;

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
    public void SetAsBullet(bool isBullet)
    {
        _isBullet = isBullet;
    }

    private void OnTriggerEnter(Collider other)
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
        if (!_owner.IsEnemyOf(piece)) return;

        piece.TakeDamage(1);
        if(!_isBullet)
            EndAttack();
    }
}
