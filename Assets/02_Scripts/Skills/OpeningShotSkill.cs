using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 시작의 총성 스킬.
/// 시뮬레이션 시작 전, 플레이어가 적 한 명을 선택해 즉시 제거한다.
/// 선택이 완료되면 시뮬레이션이 시작된다.
/// </summary>
[CreateAssetMenu(menuName = "Skills/Opening Shot Skill")]

public class OpeningShotSkill : SkillBase
{
    public override string SkillName => "시작의 총성";
    public override string Description =>
        "시뮬레이션 시작 전, 적 한 명을 선택하여 즉시 제거한다.";

    public override SkillTiming Timing => SkillTiming.PreSimulation;
    public PieceBase Target { get; set; }
    public bool isTargetingMode = false;
    /// <summary>
    /// 살아있는 적이 한 명 이상일 때만 발동 가능.
    /// </summary>
    public override bool CanExecute(IReadOnlyList<PieceBase> allPieces)
    {
        return allPieces.Any(p => p.Faction == Faction.Enemy && !p.IsDead);
    }
    public override IEnumerator TargetMode(SimulationController controller, IReadOnlyList<PieceBase> allPieces)
    {
        Debug.Log("SetTarget Called");
        // 선택 모드 활성화
        isTargetingMode = true;
        if(Target != null)
        {
            Target._isTargeted = false;
        }
        Target = null;

        // 대상이 될 수 있는 적 목록 추출
        var enemies = allPieces.Where(p => p.Faction == Faction.Enemy && !p.IsDead).ToList();

        // 플레이어가 선택할 때까지 대기 (선택되거나, 모드가 종료될 때까지)
        yield return new WaitUntil(() => Target != null );
        // 선택 모드 종료
        isTargetingMode = false;
    }
    public override void ResetTarget()
    {
        Target = null;
    }
    public override void Execute(SimulationController controller, IReadOnlyList<PieceBase> allPieces)
    {
        if (Target != null)
        {
            Debug.Log($"[Simulation] Phase -1: Executing Pre-Simulation Skill on {Target.name}");
            Target.Die();
            Target = null;
        }
        else
        {
            Debug.LogWarning("[Simulation] Phase -1: No target selected for Opening Shot Skill.");
        }
    }
}
