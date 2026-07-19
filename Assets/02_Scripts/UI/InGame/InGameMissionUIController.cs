using System.Linq;
using UnityEngine;

/// <summary>
/// StageData와 시뮬레이션 사실을 미션별 화면 상태로 변환하는 Mission UI Controller다.
/// </summary>
[RequireComponent(typeof(InGameMissionView))]
public sealed class InGameMissionUIController : MonoBehaviour
{
    [SerializeField] private SimulationController simulationController;

    private InGameMissionView view;
    private StageMissionData currentPrimaryMission;
    private StageMissionData[] currentSubMissions = System.Array.Empty<StageMissionData>();
    private int currentStageEnemyCount;

    private void Awake()
    {
        // Controller와 View를 같은 Missions 루트에 두어 Scene 참조를 단순하게 유지한다.
        view = GetComponent<InGameMissionView>();
    }

    private void OnEnable()
    {
        SubscribeSimulationEvents();
    }

    private void OnDisable()
    {
        UnsubscribeSimulationEvents();
    }

    public void Bind(StageData data)
    {
        if (data == null)
        {
            Debug.LogWarning($"{nameof(InGameMissionUIController)} cannot bind null StageData.", this);
            return;
        }

        if (view == null)
        {
            Debug.LogWarning($"{nameof(InGameMissionUIController)} has no mission view assigned.", this);
            return;
        }

        SubscribeSimulationEvents();
        currentStageEnemyCount = simulationController != null
            ? simulationController.CurrentStageEnemyCount
            : 0;
        currentPrimaryMission = data.GetPrimaryMission();
        currentSubMissions = data.GetSubMissions()
            .Where(mission => mission != null && !string.IsNullOrWhiteSpace(mission.text))
            .ToArray();

        view.Render(data.GetStageTitle(), currentSubMissions);
        view.ApplyMainMissionProgress(currentPrimaryMission.text, 0, currentStageEnemyCount);
        view.ResetMissionFailures();
    }

    private void HandleSimulationFinished(SimulationController.SimulationResult _)
    {
        if (simulationController == null)
        {
            return;
        }

        SimulationMissionFacts facts = simulationController.CurrentMissionFacts;

        // 사용자에게 최종 처치 수를 먼저 적용한 뒤 같은 프레임에 실패선 연출을 시작한다.
        view?.ApplyMainMissionProgress(
            currentPrimaryMission?.text,
            facts.DeadEnemyCount,
            facts.TotalEnemyCount);

        bool mainMissionFailed = IsMissionFailed(
            currentPrimaryMission?.type ?? MissionType.EliminateAllEnemies,
            facts);
        bool[] subMissionFailures = currentSubMissions
            .Select(mission => IsMissionFailed(mission.type, facts))
            .ToArray();
        view?.ApplyMissionFailures(mainMissionFailed, subMissionFailures);
    }

    private void HandleSimulationReset()
    {
        view?.ApplyMainMissionProgress(currentPrimaryMission?.text, 0, currentStageEnemyCount);
        view?.ResetMissionFailures();
    }

    private static bool IsMissionFailed(MissionType missionType, SimulationMissionFacts facts)
    {
        return missionType switch
        {
            MissionType.EliminateAllEnemies => facts.DeadEnemyCount < facts.TotalEnemyCount,
            MissionType.PreserveAllies => facts.DeadAllyCount > 0,
            MissionType.PreserveCivilians => facts.DeadCivilianCount > 0,
            MissionType.PreserveEliza => facts.DeadElizaCount > 0,
            MissionType.UseOpeningShot => !facts.OpeningShotExecuted,
            _ => false,
        };
    }

    private void SubscribeSimulationEvents()
    {
        if (simulationController == null)
        {
            simulationController = SimulationController.Instance;
        }

        if (simulationController == null)
        {
            return;
        }

        // OnEnable과 StageData Bind가 모두 호출돼도 결과 이벤트가 중복 연결되지 않게 한다.
        simulationController.SimulationFinished -= HandleSimulationFinished;
        simulationController.SimulationFinished += HandleSimulationFinished;
        simulationController.SimulationReset -= HandleSimulationReset;
        simulationController.SimulationReset += HandleSimulationReset;
    }

    private void UnsubscribeSimulationEvents()
    {
        if (simulationController == null)
        {
            return;
        }

        simulationController.SimulationFinished -= HandleSimulationFinished;
        simulationController.SimulationReset -= HandleSimulationReset;
    }
}
