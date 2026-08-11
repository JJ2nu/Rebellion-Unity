using System.Collections.Generic;
using Rebellion;
using UnityEngine;

/// <summary>
/// StageData의 Storage 정보를 Passive View에 전달하고 Placement 및 Simulation 상태를 연결한다.
/// </summary>
[RequireComponent(typeof(InGameStorageView))]
public sealed class InGameStorageUIController : MonoBehaviour
{
    [SerializeField] private PlacementController placementController;
    [SerializeField] private SimulationController simulationController;

    private readonly List<InGameUnitStorageSlotUI> subscribedSlots = new();
    private InGameStorageView view;
    // 가이드 표시 이벤트를 구독한 StageManager 인스턴스다. Scene 전환으로 인스턴스가 바뀌면 다시 구독한다.
    private StageManager subscribedStageManager;

    private void Awake()
    {
        view = GetComponent<InGameStorageView>();
    }

    private void OnEnable()
    {
        SubscribeSimulationState();
        SubscribeTutorialGhostState();
        ApplyInteractionState();
        ApplyGuideHighlights();
    }

    private void Start()
    {
        SubscribeSimulationState();
        SubscribeTutorialGhostState();
        ApplyInteractionState();
        ApplyGuideHighlights();
    }

    private void OnDisable()
    {
        UnsubscribeSimulationState();
        UnsubscribeTutorialGhostState();
        view?.SetInteractionLocked(false);
    }

    private void OnDestroy()
    {
        ReleasePlacementReferences();
        UnsubscribeSlotEvents();
        UnsubscribeSimulationState();
        UnsubscribeTutorialGhostState();
    }

    public void Bind(StageData data)
    {
        if (data == null)
        {
            Debug.LogWarning($"{nameof(InGameStorageUIController)} cannot bind null StageData.", this);
            return;
        }

        if (view == null)
        {
            Debug.LogWarning($"{nameof(InGameStorageUIController)} has no storage view assigned.", this);
            return;
        }

        // 기존 슬롯이 제거되기 전에 배치를 끝내고 등록 맵을 비워 파괴 예정 인스턴스가 남지 않게 한다.
        ReleasePlacementReferences();
        UnsubscribeSlotEvents();

        List<InGameStorageSlotViewData> slotData = new();
        if (data.allySlots != null)
        {
            for (int index = 0; index < data.allySlots.Length; index++)
            {
                AllySlotData allySlot = data.allySlots[index];
                if (allySlot != null && allySlot.count > 0)
                {
                    slotData.Add(new InGameStorageSlotViewData(
                        (PieceType)allySlot.pieceType,
                        allySlot.count));
                }
            }
        }

        IReadOnlyList<InGameUnitStorageSlotUI> renderedSlots = view.Render(slotData);
        for (int index = 0; index < renderedSlots.Count; index++)
        {
            InGameUnitStorageSlotUI slot = renderedSlots[index];
            if (slot == null)
            {
                continue;
            }

            slot.Clicked += HandleStorageSlotClicked;
            subscribedSlots.Add(slot);
            placementController?.RegisterSlot(slot);
        }

        ApplyInteractionState();

        // Bind 시점에는 StageManager가 이미 존재할 수 있으므로 구독을 보충하고 현재 가이드 상태를 즉시 반영한다.
        SubscribeTutorialGhostState();
        ApplyGuideHighlights();
    }

    private void ReleasePlacementReferences()
    {
        if (placementController == null)
        {
            return;
        }

        placementController.CancelPlacement();
        placementController.ClearRegisteredSlots();
    }

    private void HandleStorageSlotClicked(InGameUnitStorageSlotUI slot)
    {
        if (simulationController != null && simulationController._isRunning)
        {
            return;
        }

        if (placementController == null)
        {
            Debug.LogWarning("Placement controller is not assigned.", this);
            return;
        }

        placementController.BeginPlacement(slot);
    }

    private void UnsubscribeSlotEvents()
    {
        for (int index = 0; index < subscribedSlots.Count; index++)
        {
            InGameUnitStorageSlotUI slot = subscribedSlots[index];
            if (slot != null)
            {
                slot.Clicked -= HandleStorageSlotClicked;
            }
        }

        subscribedSlots.Clear();
    }

    private void SubscribeSimulationState()
    {
        if (simulationController == null)
        {
            return;
        }

        simulationController.RunningStateChanged -= HandleSimulationRunningStateChanged;
        simulationController.RunningStateChanged += HandleSimulationRunningStateChanged;
    }

    private void UnsubscribeSimulationState()
    {
        if (simulationController != null)
        {
            simulationController.RunningStateChanged -= HandleSimulationRunningStateChanged;
        }
    }

    private void HandleSimulationRunningStateChanged(bool _)
    {
        ApplyInteractionState();
    }

    private void ApplyInteractionState()
    {
        bool isLocked = simulationController != null && simulationController._isRunning;
        view?.SetInteractionLocked(isLocked);
    }

    private void SubscribeTutorialGhostState()
    {
        StageManager stageManager = StageManager.Instance;
        if (stageManager == null || stageManager == subscribedStageManager)
        {
            return;
        }

        UnsubscribeTutorialGhostState();
        subscribedStageManager = stageManager;
        stageManager.TutorialGhostPiecesChanged += HandleTutorialGhostPiecesChanged;
    }

    private void UnsubscribeTutorialGhostState()
    {
        if (subscribedStageManager != null)
        {
            subscribedStageManager.TutorialGhostPiecesChanged -= HandleTutorialGhostPiecesChanged;
            subscribedStageManager = null;
        }
    }

    private void HandleTutorialGhostPiecesChanged()
    {
        ApplyGuideHighlights();
    }

    /// <summary>
    /// 화면에 표시 중인 가이드 기물 종류와 일치하는 슬롯에만 가이드 하이라이트를 켠다.
    /// </summary>
    private void ApplyGuideHighlights()
    {
        StageManager stageManager = StageManager.Instance != null ? StageManager.Instance : subscribedStageManager;

        for (int index = 0; index < subscribedSlots.Count; index++)
        {
            InGameUnitStorageSlotUI slot = subscribedSlots[index];
            if (slot == null)
            {
                continue;
            }

            bool shouldHighlight = stageManager != null && stageManager.HasVisibleTutorialGhostOfType(slot.UnitType);
            slot.SetGuideHighlight(shouldHighlight);
        }
    }
}
