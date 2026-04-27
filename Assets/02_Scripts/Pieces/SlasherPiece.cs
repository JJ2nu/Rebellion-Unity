using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion
{
    /// <summary>
    /// 슬래셔(돌격 칼) 기물. 정면으로 돌진하며 경로상 적을 공격하고
    /// 빈 셀이면 해당 위치를 점령한다.
    /// </summary>
    public class SlasherPiece : PieceBase
    {
        [Header("Slasher Config")]
        [SerializeField] private float _dashDuration = 0.4f;

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
            var target = FindTarget(allPieces);
            if (target == null)
            {
                FinishAction();
                yield break;
            }

            // 돌진 연출
            yield return WaitForSeconds(_dashDuration);

            target.TakeDamage(1);

            // 대상 칸 점령 (대상이 죽으면 해당 칸으로 이동)
            if (target.IsDead)
            {
                GridX = target.GridX;
                GridY = target.GridY;
            }

            FinishAction();
        }
    }
}
