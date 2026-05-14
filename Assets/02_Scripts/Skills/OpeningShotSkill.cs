using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Rebellion
{
    /// <summary>
    /// 시작의 총성 스킬.
    /// 시뮬레이션 시작 전, 플레이어가 적 한 명을 선택해 즉시 제거한다.
    /// 선택이 완료되면 시뮬레이션이 시작된다.
    /// </summary>
    public class OpeningShotSkill : SkillBase
    {
        public override string SkillName => "시작의 총성";
        public override string Description =>
            "시뮬레이션 시작 전, 적 한 명을 선택하여 즉시 제거한다.";

        public override SkillTiming Timing => SkillTiming.PreSimulation;

        /// <summary>
        /// 살아있는 적이 한 명 이상일 때만 발동 가능.
        /// </summary>
        public override bool CanExecute(IReadOnlyList<PieceBase> allPieces)
        {
            return allPieces.Any(p => p.Faction == Faction.Enemy && !p.IsDead);
        }

        // public override IEnumerator Execute(SimulationController controller, IReadOnlyList<PieceBase> allPieces)
        // {
        //     // 플레이어가 적을 선택할 때까지 대기
        //     PieceBase selectedTarget = null;

        //     var enemies = allPieces
        //         .Where(p => p.Faction == Faction.Enemy && !p.IsDead)
        //         .ToList();

        //     // SimulationController를 통해 선택 UI를 열고 결과를 받아온다
        //     yield return controller.WaitForPlayerSelection(enemies, picked => selectedTarget = picked);

        //     if (selectedTarget != null)
        //     {
        //         selectedTarget.Die();
        //         UnityEngine.Debug.Log($"[시작의 총성] {selectedTarget.name} 제거");
        //     }
        // }
    }
}
