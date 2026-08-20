using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Stage의 missionId를 정의 에셋과 평가 상태로 변환해 Mission View에 전달한다.
/// </summary>
[RequireComponent(typeof(InGameMissionView))]
public sealed class InGameMissionUIController : MonoBehaviour
{
    [SerializeField] private SimulationController simulationController;
    [SerializeField] private MissionDefinitionRegistry missionDefinitionRegistry;

    private readonly List<BoundMission> boundMissions = new();
    private InGameMissionView view;
    private int currentStageEnemyCount;

    private sealed class BoundMission
    {
        public string MissionId;
        public MissionDefinition Definition;
        public MissionEvaluationState State = MissionEvaluationState.InProgress;

        public string DisplayText => Definition != null &&
            !string.IsNullOrWhiteSpace(Definition.DisplayText)
                ? Definition.DisplayText
                : MissionId;
    }

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

        boundMissions.Clear();
        boundMissions.Add(BindMission(data.GetPrimaryMission(), data.GetStageTitle()));
        boundMissions.AddRange(data.GetSubMissions()
            .Where(mission => mission != null && !string.IsNullOrWhiteSpace(mission.missionId))
            .Select(mission => BindMission(mission, data.GetStageTitle())));

        BoundMission primaryMission = boundMissions[0];
        string[] subMissionTexts = boundMissions
            .Skip(1)
            .Select(mission => mission.DisplayText)
            .ToArray();

        view.Render(data.GetStageTitle(), subMissionTexts);
        view.ApplyMainMissionProgress(primaryMission.DisplayText, 0, currentStageEnemyCount);
        view.ResetMissionResultMarks();
    }

    private void HandleMissionFactsChanged(SimulationMissionFacts facts)
    {
        EvaluateMissions(facts, MissionEvaluationMoment.FactsChanged);
    }

    private void HandleMissionStartFactsFinalized(SimulationMissionFacts facts)
    {
        EvaluateMissions(facts, MissionEvaluationMoment.SimulationStarted);
    }

    private void HandleSimulationFinished(SimulationController.SimulationResult _)
    {
        if (simulationController == null || boundMissions.Count == 0)
        {
            return;
        }

        SimulationMissionFacts facts = simulationController.CurrentMissionFacts;
        BoundMission primaryMission = boundMissions[0];

        // 사용자에게 최종 처치 수를 먼저 적용한 뒤 아직 확정되지 않은 미션을 최종 평가한다.
        view?.ApplyMainMissionProgress(
            primaryMission.DisplayText,
            facts.DeadEnemyCount,
            facts.TotalEnemyCount);
        EvaluateMissions(facts, MissionEvaluationMoment.SimulationFinished);

        // 클리어 동그라미는 도중에 성공이 확정된 미션도 시뮬레이션이 완전히 끝난 뒤에만 표시한다.
        for (int index = 0; index < boundMissions.Count; index++)
        {
            if (boundMissions[index].State == MissionEvaluationState.Succeeded)
            {
                view?.ShowMissionSuccess(index);
            }
        }
    }

    private void HandleSimulationReset()
    {
        foreach (BoundMission mission in boundMissions)
        {
            mission.State = MissionEvaluationState.InProgress;
        }

        BoundMission primaryMission = boundMissions.Count > 0 ? boundMissions[0] : null;
        view?.ApplyMainMissionProgress(primaryMission?.DisplayText, 0, currentStageEnemyCount);
        view?.ResetMissionResultMarks();
    }

    private void EvaluateMissions(
        SimulationMissionFacts facts,
        MissionEvaluationMoment evaluationMoment)
    {
        for (int index = 0; index < boundMissions.Count; index++)
        {
            BoundMission mission = boundMissions[index];
            if (mission.State != MissionEvaluationState.InProgress)
            {
                continue;
            }

            MissionEvaluationState nextState = EvaluateMission(
                mission,
                facts,
                evaluationMoment);
            mission.State = nextState;

            if (nextState == MissionEvaluationState.Failed)
            {
                // View의 0번은 주 미션이고 이후 인덱스는 화면의 서브 미션 순서와 같다.
                view?.ShowMissionFailure(index);
            }
        }
    }

    private MissionEvaluationState EvaluateMission(
        BoundMission mission,
        SimulationMissionFacts facts,
        MissionEvaluationMoment evaluationMoment)
    {
        if (mission.Definition == null)
        {
            return evaluationMoment == MissionEvaluationMoment.SimulationFinished
                ? MissionEvaluationState.Failed
                : MissionEvaluationState.InProgress;
        }

        if (MissionEvaluator.TryEvaluate(
            mission.Definition.MissionType,
            mission.Definition.EvaluationTiming,
            facts,
            evaluationMoment,
            out MissionEvaluationState state))
        {
            return state;
        }

        Debug.LogWarning(
            $"{nameof(InGameMissionUIController)} cannot evaluate missionId " +
            $"'{mission.MissionId}' with mission type {(int)mission.Definition.MissionType}.",
            this);
        return MissionEvaluationState.Failed;
    }

    private BoundMission BindMission(StageMissionData missionData, string stageTitle)
    {
        string missionId = missionData?.missionId ?? string.Empty;
        MissionDefinition definition = null;

        if (missionDefinitionRegistry == null)
        {
            Debug.LogWarning(
                $"{nameof(InGameMissionUIController)} has no MissionDefinitionRegistry " +
                $"for stage '{stageTitle}'.",
                this);
        }
        else if (!missionDefinitionRegistry.TryGetDefinition(missionId, out definition))
        {
            Debug.LogWarning(
                $"{nameof(InGameMissionUIController)} cannot resolve missionId '{missionId}' " +
                $"for stage '{stageTitle}'.",
                this);
        }

        return new BoundMission
        {
            MissionId = missionId,
            Definition = definition,
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

        // OnEnable과 StageData Bind가 모두 호출돼도 각 이벤트가 중복 연결되지 않게 한다.
        simulationController.MissionFactsChanged -= HandleMissionFactsChanged;
        simulationController.MissionFactsChanged += HandleMissionFactsChanged;
        simulationController.MissionStartFactsFinalized -= HandleMissionStartFactsFinalized;
        simulationController.MissionStartFactsFinalized += HandleMissionStartFactsFinalized;
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

        simulationController.MissionFactsChanged -= HandleMissionFactsChanged;
        simulationController.MissionStartFactsFinalized -= HandleMissionStartFactsFinalized;
        simulationController.SimulationFinished -= HandleSimulationFinished;
        simulationController.SimulationReset -= HandleSimulationReset;
    }
}
