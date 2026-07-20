/// <summary>
/// 한 번의 시뮬레이션이 끝났을 때 미션 판정에 필요한 최종 전투 사실을 보관한다.
/// 종합 승패인 SimulationResult와 분리해 미션이 늘어나도 승패 enum 조합이 증가하지 않게 한다.
/// </summary>
public readonly struct SimulationMissionFacts
{
    public SimulationMissionFacts(
        int totalEnemyCount,
        int deadEnemyCount,
        int deadAllyCount,
        int deadCivilianCount,
        int deadElizaCount,
        bool openingShotExecuted)
    {
        TotalEnemyCount = totalEnemyCount;
        DeadEnemyCount = deadEnemyCount;
        DeadAllyCount = deadAllyCount;
        DeadCivilianCount = deadCivilianCount;
        DeadElizaCount = deadElizaCount;
        OpeningShotExecuted = openingShotExecuted;
    }

    public int TotalEnemyCount { get; }
    public int DeadEnemyCount { get; }
    public int DeadAllyCount { get; }
    public int DeadCivilianCount { get; }
    public int DeadElizaCount { get; }
    public bool OpeningShotExecuted { get; }
}
