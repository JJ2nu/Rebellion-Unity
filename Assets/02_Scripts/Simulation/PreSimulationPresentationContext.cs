using System.Collections.Generic;

/// <summary>
/// 한 선처리 스킬의 Presentation과 게임 효과 실행 사이에서 공유하는 실행 문맥이다.
/// Presentation Controller는 원하는 연출 시점에 효과를 확정할 수 있고, 중복 요청은 한 번으로 제한된다.
/// </summary>
public sealed class PreSimulationPresentationContext
{
    private readonly SimulationController simulationController;
    private readonly IReadOnlyList<PieceBase> allPieces;
    private bool isEffectApplied;

    public SkillBase Skill { get; }
    public IReadOnlyList<PieceBase> AllPieces => allPieces;
    public bool IsEffectApplied => isEffectApplied;

    public PreSimulationPresentationContext(
        SkillBase skill,
        SimulationController simulationController,
        IReadOnlyList<PieceBase> allPieces)
    {
        Skill = skill;
        this.simulationController = simulationController;
        this.allPieces = allPieces;
    }

    /// <summary>
    /// 스킬 효과와 그 안의 실제 실행 기록을 이 선처리 항목에서 한 번만 적용한다.
    /// </summary>
    public bool TryApplyEffect()
    {
        if (isEffectApplied || Skill == null)
        {
            return false;
        }

        isEffectApplied = true;
        Skill.Execute(simulationController, allPieces);
        return true;
    }
}
