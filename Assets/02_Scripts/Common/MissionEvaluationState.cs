/// <summary>
/// 시뮬레이션 도중에는 아직 확정되지 않은 상태와 최종 성공·실패를 구분한다.
/// </summary>
public enum MissionEvaluationState
{
    InProgress,
    Succeeded,
    Failed,
}

/// <summary>
/// 미션이 조건 위반 순간 실패하는지, 시뮬레이션 종료 시 확정되는지 지정한다.
/// </summary>
public enum MissionEvaluationTiming
{
    SimulationFinished,
    ImmediateOnFailure,
    SimulationStarted,
}

/// <summary>
/// 같은 전투 사실이라도 미션 정의가 어느 실행 경계에서 확정할지 구분한다.
/// </summary>
public enum MissionEvaluationMoment
{
    FactsChanged,
    SimulationStarted,
    SimulationFinished,
}
