using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion
{
    /// <summary>
    /// 브롤러(근접 강타) 기물. 정면 1칸 적을 공격한다.
    /// </summary>
    public class BrawlerPiece : PieceBase
    {
        [Header("Brawler Config")]
        [SerializeField] private float _punchDelay = 0.3f;

        protected override PieceBase FindTarget(IReadOnlyList<PieceBase> allPieces)
        {
            foreach (var piece in allPieces)
            {
                if (piece.IsDead || piece.Faction == Faction) continue;
                if (IsInLineOfFire(piece)) return piece;
            }
            return null;
        }

        public override IEnumerator ExecuteAction(IReadOnlyList<PieceBase> allPieces)
        {
            var target = FindTarget(allPieces);
            if (target == null)
            {
                FinishAction();
                yield break;
            }

            yield return WaitForSeconds(_punchDelay);

            target.TakeDamage(1);
            FinishAction();
        }
    }
}
