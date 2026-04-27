using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion
{
    /// <summary>
    /// 건맨(총) 기물. 정면 직선 사정거리 내 가장 가까운 적을 사격한다.
    /// 시작의 총성 스킬의 주체.
    /// </summary>
    public class GunmanPiece : PieceBase
    {
        [Header("Gunman Config")]
        [SerializeField] private float _aimDelay = 0.5f;
        [SerializeField] private float _fireDelay = 0.2f;

        protected override PieceBase FindTarget(IReadOnlyList<PieceBase> allPieces)
        {
            var (dx, dy) = GetFacingDelta();
            PieceBase closest = null;
            int minDist = int.MaxValue;

            foreach (var piece in allPieces)
            {
                if (piece.IsDead || piece.Faction == Faction) continue;
                if (!IsInLineOfFire(piece)) continue;

                int dist = ManhattanDistanceTo(piece);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = piece;
                }
            }
            return closest;
        }

        public override IEnumerator ExecuteAction(IReadOnlyList<PieceBase> allPieces)
        {
            yield return Fire(allPieces);
        }

        /// <summary>
        /// 스킬 등 외부에서도 발사 동작을 직접 호출할 수 있다.
        /// </summary>
        public IEnumerator Fire(IReadOnlyList<PieceBase> allPieces)
        {
            var target = FindTarget(allPieces);
            if (target == null)
            {
                FinishAction();
                yield break;
            }

            yield return WaitForSeconds(_aimDelay);
            yield return WaitForSeconds(_fireDelay);

            target.TakeDamage(1);
            FinishAction();
        }
    }
}
