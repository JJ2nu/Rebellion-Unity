using UnityEngine;

/// <summary>
/// 총알 충돌 데미지 처리. 관통형 — 피격 후에도 계속 날아감.
/// GunmanPiece에서 Faction을 설정한 뒤 발사해야 한다.
/// </summary>
public class BulletController : MonoBehaviour
{
    /// <summary>발사한 기물의 진영. 같은 진영은 데미지 무시.</summary>
    public Faction ShooterFaction { get; set; }

    private void OnTriggerEnter(Collider other)
    {
        // 데미지는 GunmanPiece에서 셀 기반으로 처리
    }
}
