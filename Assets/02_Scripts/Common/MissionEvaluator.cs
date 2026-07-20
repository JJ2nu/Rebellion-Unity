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
        MissionEvaluationTiming evaluationTiming,
        SimulationMissionFacts facts,
        MissionEvaluationMoment evaluationMoment,
        out MissionEvaluationState state)
    {
        bool isSuccessful;
        switch (missionType)
        {
            case MissionType.EliminateAllEnemies:
                isSuccessful = EvaluateEliminateAllEnemies(facts);
                break;
            case MissionType.PreserveAllies:
                isSuccessful = EvaluatePreserveAllies(facts);
                break;
            case MissionType.PreserveCivilians:
                isSuccessful = EvaluatePreserveCivilians(facts);
                break;
            case MissionType.PreserveEliza:
                isSuccessful = EvaluatePreserveEliza(facts);
                break;
            case MissionType.UseOpeningShot:
                isSuccessful = EvaluateUseOpeningShot(facts);
                break;
            default:
                state = MissionEvaluationState.Failed;
                return false;
        }

        // 즉시 실패 미션은 사실이 바뀌는 매 순간 조건 위반을 확인한다.
        if (evaluationTiming == MissionEvaluationTiming.ImmediateOnFailure && !isSuccessful)
        {
            state = MissionEvaluationState.Failed;
            return true;
        }

        bool shouldFinalize = evaluationTiming switch
        {
            MissionEvaluationTiming.SimulationStarted =>
                evaluationMoment == MissionEvaluationMoment.SimulationStarted ||
                evaluationMoment == MissionEvaluationMoment.SimulationFinished,
            MissionEvaluationTiming.SimulationFinished =>
                evaluationMoment == MissionEvaluationMoment.SimulationFinished,
            MissionEvaluationTiming.ImmediateOnFailure =>
                evaluationMoment == MissionEvaluationMoment.SimulationFinished,
            _ => false,
        };
        if (!shouldFinalize)
        {
            state = MissionEvaluationState.InProgress;
            return true;
        }

        state = isSuccessful
            ? MissionEvaluationState.Succeeded
            : MissionEvaluationState.Failed;
        return true;
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
