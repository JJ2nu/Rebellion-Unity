using System.Linq;
using Rebellion;
using UnityEngine;

/// <summary>
/// OpeningShot, Placement, Simulation 상태를 OrderSkillViewState로 변환하는 기능 Controller다.
/// </summary>
[RequireComponent(typeof(OrderSkillView))]
public sealed class OrderSkillUIController : MonoBehaviour
{
    [SerializeField] private PlacementController placementController;
    [SerializeField] private SimulationController simulationController;

    private OrderSkillView view;
    private OpeningShotSkill openingShotSkill;
    private bool hasOrder;
    private bool lastPlacementState;

    private void Awake()
    {
        view = GetComponent<OrderSkillView>();
        EnsureSimulationController();
    }

    private void OnEnable()
    {
        SubscribeViewEvents();
        SubscribeSimulationState();
        SubscribeOpeningShotSkillState();
        ApplyViewState();
    }

    private void Start()
    {
        SubscribeSimulationState();
        SubscribeOpeningShotSkillState();
        ApplyViewState();
    }

    private void Update()
    {
        bool isPlacing = placementController != null && placementController.IsPlacing;
        if (isPlacing != lastPlacementState)
        {
            ApplyViewState();
        }
    }

    private void OnDisable()
    {
        UnsubscribeViewEvents();
        UnsubscribeSimulationState();
        UnsubscribeOpeningShotSkillState();
        view?.Apply(OrderSkillViewState.Hidden);
    }

    private void OnDestroy()
    {
        UnsubscribeViewEvents();
        UnsubscribeSimulationState();
        UnsubscribeOpeningShotSkillState();
    }

    public void Bind(StageData data)
    {
        if (data == null)
        {
            Debug.LogWarning($"{nameof(OrderSkillUIController)} cannot bind null StageData.", this);
            return;
        }

        if (view == null)
        {
            Debug.LogWarning($"{nameof(OrderSkillUIController)} has no Order Skill view assigned.", this);
            return;
        }

        hasOrder = data.hasOrder;
        view.Render(hasOrder);
        SubscribeOpeningShotSkillState();
        ApplyViewState();
    }

    private void SubscribeViewEvents()
    {
        if (view == null)
        {
            return;
        }

        view.OrderRequested -= HandleOrderRequested;
        view.OrderRequested += HandleOrderRequested;
        view.CancelRequested -= HandleCancelRequested;
        view.CancelRequested += HandleCancelRequested;
    }

    private void UnsubscribeViewEvents()
    {
        if (view == null)
        {
            return;
        }

        view.OrderRequested -= HandleOrderRequested;
        view.CancelRequested -= HandleCancelRequested;
    }

    private void HandleOrderRequested()
    {
        if (!hasOrder || IsOrderLocked())
        {
            return;
        }

        EnsureSimulationController();
        SubscribeOpeningShotSkillState();
        simulationController?.SetTargetForPreSimulation(openingShotSkill);
    }

    private void HandleCancelRequested()
    {
        if (openingShotSkill != null && openingShotSkill.isTargetingMode)
        {
            openingShotSkill.ResetTarget();
            return;
        }

        ApplyViewState();
    }

    private void SubscribeOpeningShotSkillState()
    {
        EnsureSimulationController();

        OpeningShotSkill nextSkill = simulationController != null
            ? simulationController.GetStageSkills().OfType<OpeningShotSkill>().FirstOrDefault()
            : null;

        if (ReferenceEquals(openingShotSkill, nextSkill))
        {
            return;
        }

        UnsubscribeOpeningShotSkillState();
        openingShotSkill = nextSkill;

        if (openingShotSkill != null)
        {
            openingShotSkill.TargetStateChanged += HandleOpeningShotTargetStateChanged;
        }
    }

    private void UnsubscribeOpeningShotSkillState()
    {
        if (openingShotSkill == null)
        {
            return;
        }

        openingShotSkill.TargetStateChanged -= HandleOpeningShotTargetStateChanged;
        openingShotSkill = null;
    }

    private void HandleOpeningShotTargetStateChanged()
    {
        ApplyViewState();
    }

    private void EnsureSimulationController()
    {
        if (simulationController == null)
        {
            simulationController = SimulationController.Instance;
        }

    }

    private void SubscribeSimulationState()
    {
        EnsureSimulationController();
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
        ApplyViewState();
    }

    private void ApplyViewState()
    {
        bool isTargeting = hasOrder
            && openingShotSkill != null
            && openingShotSkill.isTargetingMode;

        if (isTargeting && placementController != null && placementController.IsPlacing)
        {
            placementController.CancelPlacement();
        }

        bool openingShotLocked = hasOrder
            && openingShotSkill != null
            && (openingShotSkill.isTargetingMode || openingShotSkill.HasConfirmedTarget);

        OrderSkillViewState state = new(
            hasOrder,
            !openingShotLocked,
            GetLockMode(openingShotLocked),
            isTargeting,
            isTargeting);

        view?.Apply(state);
        lastPlacementState = placementController != null && placementController.IsPlacing;
    }

    private UIButtonLockMode GetLockMode(bool openingShotLocked)
    {
        if (simulationController != null && simulationController._isRunning)
        {
            return UIButtonLockMode.VisualDisabled;
        }

        if (openingShotLocked || (placementController != null && placementController.IsPlacing))
        {
            return UIButtonLockMode.InteractionOnly;
        }

        return UIButtonLockMode.None;
    }

    private bool IsOrderLocked()
    {
        bool openingShotLocked = openingShotSkill != null
            && (openingShotSkill.isTargetingMode || openingShotSkill.HasConfirmedTarget);
        bool placementLocked = placementController != null && placementController.IsPlacing;
        bool simulationLocked = simulationController != null && simulationController._isRunning;

        return openingShotLocked || placementLocked || simulationLocked;
    }
}
