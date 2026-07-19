using UnityEngine;

/// <summary>
/// StageData의 미션 문구를 Passive View에 전달하는 Mission UI Controller다.
/// 스테이지 로드 구독은 상위 InGameUIController에 유지해 기능별 Controller가 게임 전역 상태를 직접 찾지 않게 한다.
/// </summary>
[RequireComponent(typeof(InGameMissionView))]
public sealed class InGameMissionUIController : MonoBehaviour
{
    [SerializeField] private SimulationController simulationController;

    private InGameMissionView view;
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
        view.Render(data.mainMission, data.subMission1, data.subMission2);
        view.ApplyMainMissionProgress(0, currentStageEnemyCount);
    }

    private void HandleSimulationFinished(SimulationController.SimulationResult _)
    {
        int deadEnemyCount = simulationController != null
            ? simulationController.CurrentDeadEnemyCount
            : 0;
        view?.ApplyMainMissionProgress(deadEnemyCount, currentStageEnemyCount);
    }

    private void HandleSimulationReset()
    {
        view?.ApplyMainMissionProgress(0, currentStageEnemyCount);
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
