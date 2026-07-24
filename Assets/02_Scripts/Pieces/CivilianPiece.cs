using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 민간인 기물. 적대 행동 없이 생존 여부만 판정에 영향을 준다.
/// </summary>
public class CivilianPiece : PieceBase
{
    public override void OnSimulationStart()
    {
        _animator?.SetTrigger("SimulationStart");

        base.OnSimulationStart();
    }
                protected override PieceBase FindTarget(IReadOnlyList<PieceBase> allPieces) => null;

    public override IEnumerator ExecuteAction(IReadOnlyList<PieceBase> allPieces, float stepDuration)
    {
        // 민간인은 행동 없이 대기
        FinishAction();
        yield break;
    }
}
