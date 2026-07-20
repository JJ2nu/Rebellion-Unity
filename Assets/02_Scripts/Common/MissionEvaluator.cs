/// <summary>
/// Stage 미션 종류와 최종 전투 사실만으로 성공 여부를 계산하는 일반 C# 평가기다.
/// UI와 Unity 생명주기에 의존하지 않아 미션 규칙을 화면 표현과 독립적으로 유지한다.
/// </summary>
public static class MissionEvaluator
{
    /// <summary>
    /// 등록된 미션이면 성공 여부를 반환하고, 알 수 없는 enum 값이면 false를 반환한다.
    /// 호출자는 미지원 미션을 성공으로 오인하지 않도록 별도 오류 상태로 처리해야 한다.
    /// </summary>
    public static bool TryEvaluate(
        MissionType missionType,
        SimulationMissionFacts facts,
        out bool isSuccessful)
    {
        switch (missionType)
        {
            case MissionType.EliminateAllEnemies:
                isSuccessful = EvaluateEliminateAllEnemies(facts);
                return true;
            case MissionType.PreserveAllies:
                isSuccessful = EvaluatePreserveAllies(facts);
                return true;
            case MissionType.PreserveCivilians:
                isSuccessful = EvaluatePreserveCivilians(facts);
                return true;
            case MissionType.PreserveEliza:
                isSuccessful = EvaluatePreserveEliza(facts);
                return true;
            case MissionType.UseOpeningShot:
                isSuccessful = EvaluateUseOpeningShot(facts);
                return true;
            default:
                isSuccessful = false;
                return false;
        }
    }

    private static bool EvaluateEliminateAllEnemies(SimulationMissionFacts facts)
    {
        return facts.DeadEnemyCount >= facts.TotalEnemyCount;
    }

    private static bool EvaluatePreserveAllies(SimulationMissionFacts facts)
    {
        return facts.DeadAllyCount == 0;
    }

    private static bool EvaluatePreserveCivilians(SimulationMissionFacts facts)
    {
        return facts.DeadCivilianCount == 0;
    }

    private static bool EvaluatePreserveEliza(SimulationMissionFacts facts)
    {
        return facts.DeadElizaCount == 0;
    }

    private static bool EvaluateUseOpeningShot(SimulationMissionFacts facts)
    {
        return facts.OpeningShotExecuted;
    }
}
