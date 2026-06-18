// 스테이지 JSON에서 현재 레벨의 UI 데이터를 읽고 미션, 대원 배치 버튼, 스킬 버튼을 생성한다.

using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion;
using UnityEngine;
using UnityEngine.UI;

public sealed class InGameUIController : MonoBehaviour
{
    #region Types

    [Serializable]
    private sealed class UnitStoragePrefabBinding
    {
        public PieceType unitType;
        public InGameUnitStorageSlotUI prefab;
    }

    #endregion

    #region Fields

    [Header("Missions")]
    [SerializeField] private Transform missionRoot;
    [SerializeField] private InGameMissionSlotUI mainMissionPrefab;
    [SerializeField] private InGameMissionSlotUI subMissionPrefab;
    [SerializeField] private float subMissionVerticalSpacing = 90f;

    [Header("Storages")]
    [SerializeField] private Transform storageRoot;
    [SerializeField] private UnitStoragePrefabBinding[] storagePrefabs = Array.Empty<UnitStoragePrefabBinding>();
    [SerializeField] private Vector2 storageRightAnchoredPosition = new(-50f, 50f);
    [SerializeField] private float storageHorizontalSpacing = 180f;

    [Header("Skills")]
    [SerializeField] private Transform skillRoot;
    [SerializeField] private GameObject orderSkillButtonPrefab;
    [SerializeField] private OpeningShotScopePreview openingShotScopePreview;

    [Header("Placement")]
    [SerializeField] private PlacementController placementController;
    [SerializeField] private SimulationController simulationController;

    private readonly List<InGameUnitStorageSlotUI> storageSlots = new();
    private Button orderSkillButton;
    private UIButtonLockView orderSkillButtonLockView;
    private OpeningShotSkill openingShotSkill;
    private OpeningShotScopePreview subscribedScopePreview;
    private GameObject targetingUiClickBlocker;
    private bool lastOrderSkillPlacementState;

    #endregion

    #region Unity Events

    private void Awake()
    {
        if (placementController == null)
        {
            placementController = FindObjectOfType<PlacementController>();
        }

        EnsureSimulationController();
        StageManager.StageLoaded += HandleStageLoaded;
    }

    private void OnEnable()
    {
        SubscribeSimulationState();
        SubscribeOpeningShotSkillState();
        SubscribeScopeCancelRequest(openingShotScopePreview);
        ApplyStorageInteractionState();
        ApplyOrderSkillButtonState();
    }

    private void Start()
    {
        EnsureSimulationController();
        SubscribeSimulationState();

        StageData current = StageManager.Instance?.CurrentStageData;
        if (current != null)
        {
            Bind(current);
        }

        ApplyStorageInteractionState();
    }

    private void Update()
    {
        // PlacementController에는 상태 변경 이벤트가 없어서 배치 시작/취소 변화만 가볍게 감시한다.
        RefreshOrderSkillPlacementStateIfNeeded();
    }

    private void OnDisable()
    {
        if (simulationController != null)
        {
            simulationController.RunningStateChanged -= HandleSimulationRunningStateChanged;
        }

        UnsubscribeOpeningShotSkillState();
        UnsubscribeScopeCancelRequest();
        SetTargetingUiClickBlocked(false);
        SetOrderSkillButtonClickBlocked(false);
    }

    private void OnDestroy()
    {
        StageManager.StageLoaded -= HandleStageLoaded;
        UnsubscribeOpeningShotSkillState();
        UnsubscribeScopeCancelRequest();
        SetTargetingUiClickBlocked(false);
        SetOrderSkillButtonClickBlocked(false);
    }

    #endregion

    #region Stage Events

    private void HandleStageLoaded(StageData data)
    {
        Bind(data);
    }

    #endregion

    #region Binding

    private void Bind(StageData data)
    {
        storageSlots.Clear();
        ClearChildren(missionRoot);
        ClearChildren(storageRoot);
        ClearChildren(skillRoot);
        ClearOrderSkillButtonCache();

        CreateMission(mainMissionPrefab, data.mainMission, 0f);

        int subMissionIndex = 0;
        if (!string.IsNullOrWhiteSpace(data.subMission1))
        {
            CreateSubMission(data.subMission1, subMissionIndex++);
        }

        if (!string.IsNullOrWhiteSpace(data.subMission2))
        {
            CreateSubMission(data.subMission2, subMissionIndex);
        }

        if (data.allySlots != null)
        {
            foreach (AllySlotData slot in data.allySlots)
            {
                if (slot != null && slot.count > 0)
                {
                    CreateStorage((PieceType)slot.pieceType, slot.count);
                }
            }
        }

        AlignStoragesFromRight();
        ApplyStorageInteractionState();

        if (data.hasOrder)
        {
            CreateOrderSkillButton();
        }
    }

    #endregion

    #region Mission Creation

    private void CreateSubMission(string mission, int index)
    {
        if (string.IsNullOrWhiteSpace(mission))
        {
            return;
        }

        CreateMission(subMissionPrefab, mission, -subMissionVerticalSpacing * index);
    }

    private void CreateMission(InGameMissionSlotUI prefab, string mission, float yOffset)
    {
        if (prefab == null || missionRoot == null)
        {
            Debug.LogWarning("Mission prefab or mission root is not assigned.", this);
            return;
        }

        InGameMissionSlotUI slot = Instantiate(prefab, missionRoot, false);
        RectTransform slotTransform = slot.transform as RectTransform;
        if (slotTransform != null && !Mathf.Approximately(yOffset, 0f))
        {
            slotTransform.anchoredPosition += new Vector2(0f, yOffset);
        }

        slot.Bind(mission);
    }

    #endregion

    #region Storage Creation

    private void CreateStorage(PieceType unitType, int deployableCount)
    {
        if (deployableCount <= 0)
        {
            return;
        }

        if (storageRoot == null)
        {
            Debug.LogWarning("Storage root is not assigned.", this);
            return;
        }

        InGameUnitStorageSlotUI prefab = FindStoragePrefab(unitType);
        if (prefab == null)
        {
            Debug.LogWarning($"Storage prefab is not assigned. UnitType: {unitType}", this);
            return;
        }

        InGameUnitStorageSlotUI slot = Instantiate(prefab, storageRoot, false);
        slot.Bind(unitType, deployableCount);
        slot.Clicked += HandleStorageSlotClicked;
        storageSlots.Add(slot);
        placementController?.RegisterSlot(slot);
    }

    private void AlignStoragesFromRight()
    {
        if (storageRoot == null)
        {
            return;
        }

        List<RectTransform> storageSlots = new();
        for (int index = 0; index < storageRoot.childCount; index++)
        {
            RectTransform slotTransform = storageRoot.GetChild(index) as RectTransform;
            if (slotTransform != null)
            {
                storageSlots.Add(slotTransform);
            }
        }

        int slotCount = storageSlots.Count;
        for (int index = 0; index < slotCount; index++)
        {
            float xOffset = storageHorizontalSpacing * (slotCount - 1 - index);
            storageSlots[index].anchoredPosition = storageRightAnchoredPosition - new Vector2(xOffset, 0f);
        }
    }

    private InGameUnitStorageSlotUI FindStoragePrefab(PieceType unitType)
    {
        for (int index = 0; index < storagePrefabs.Length; index++)
        {
            UnitStoragePrefabBinding binding = storagePrefabs[index];
            if (binding != null && binding.unitType == unitType)
            {
                return binding.prefab;
            }
        }

        return null;
    }

    #endregion

    #region Placement

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
        ApplyOrderSkillButtonState();
    }

    #endregion

    #region Skill Creation

    private void CreateOrderSkillButton()
    {
        if (skillRoot == null || orderSkillButtonPrefab == null)
        {
            Debug.LogWarning("Order skill root or prefab is not assigned.", this);
            return;
        }

        GameObject obj = Instantiate(orderSkillButtonPrefab, skillRoot, false);
        Button btn = obj.GetComponent<Button>();
        if (btn == null)
        {
            Debug.LogWarning("Order skill button prefab has no Button component.", this);
            return;
        }

        UIButtonLockView lockView = obj.GetComponent<UIButtonLockView>();
        if (lockView == null)
        {
            lockView = obj.AddComponent<UIButtonLockView>();
        }

        CacheOrderSkillButton(btn, lockView);

        btn.onClick.AddListener(() => 
        {
            // 배치/시뮬레이션/타겟 확정 상태에서는 이미지가 Act여도 기능과 클릭음을 막는다.
            if (IsOrderSkillButtonLocked())
            {
                return;
            }

            EnsureSimulationController();
            SubscribeOpeningShotSkillState();
            simulationController?.SetTargetForPreSimulation(openingShotSkill);
        });

        ResolveOpeningShotScopePreview();
        SubscribeOpeningShotSkillState();
        ApplyOrderSkillButtonState();
    }

    #endregion

    #region Helpers

    private void ResolveOpeningShotScopePreview()
    {
        if (openingShotScopePreview == null)
        {
            // Stage에서는 스코프가 버튼 클릭이 아니라 OpeningShotSkill.isTargetingMode 상태에만 반응한다.
            openingShotScopePreview = FindAnyObjectByType<OpeningShotScopePreview>(FindObjectsInactive.Include);
        }

        if (openingShotScopePreview == null)
        {
            return;
        }

        SubscribeScopeCancelRequest(openingShotScopePreview);
    }

    private void CacheOrderSkillButton(Button button, UIButtonLockView lockView)
    {
        // Order 버튼의 이미지 전환과 클릭 차단은 Prefab에 붙은 공용 잠금 컴포넌트가 담당한다.
        orderSkillButton = button;
        orderSkillButtonLockView = lockView;

        ApplyOrderSkillButtonVisualState(false);
    }

    private void ClearOrderSkillButtonCache()
    {
        orderSkillButton = null;
        orderSkillButtonLockView = null;
    }

    private void SubscribeOpeningShotSkillState()
    {
        EnsureSimulationController();

        // Stage 스킬 순서가 바뀌어도 OpeningShotSkill 인스턴스 자체를 구독해 UI 상태를 동기화한다.
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

    private void SubscribeScopeCancelRequest(OpeningShotScopePreview preview)
    {
        if (ReferenceEquals(subscribedScopePreview, preview))
        {
            return;
        }

        UnsubscribeScopeCancelRequest();
        if (preview == null)
        {
            return;
        }

        subscribedScopePreview = preview;
        subscribedScopePreview.CancelRequested += HandleScopeCancelRequested;
    }

    private void UnsubscribeScopeCancelRequest()
    {
        if (subscribedScopePreview == null)
        {
            return;
        }

        subscribedScopePreview.CancelRequested -= HandleScopeCancelRequested;
        subscribedScopePreview = null;
    }

    private void HandleScopeCancelRequested()
    {
        // 스코프 우클릭 취소는 시각 효과뿐 아니라 실제 타겟팅 모드도 함께 해제해야 한다.
        if (openingShotSkill != null && openingShotSkill.isTargetingMode)
        {
            openingShotSkill.ResetTarget();
            return;
        }

        ApplyOrderSkillButtonState();
    }

    private void HandleOpeningShotTargetStateChanged()
    {
        ApplyOrderSkillButtonState();
    }

    private void ApplyOrderSkillButtonState()
    {
        // 버튼 이미지는 OpeningShot 상태만 따르고, 배치/시뮬레이션 잠금은 별도 차단막으로 처리한다.
        ApplyOpeningShotTargetingUiBlockState();

        if (orderSkillButton == null)
        {
            CacheOrderSkillLockStates();
            return;
        }

        bool openingShotLocked = IsOrderSkillButtonLockedByOpeningShot();
        bool inputStateLocked = IsOrderSkillButtonLockedByInputState();

        ApplyOrderSkillButtonVisualState(openingShotLocked);
        SetOrderSkillButtonClickBlocked(!openingShotLocked && inputStateLocked);
        CacheOrderSkillLockStates();

        if (openingShotSkill != null && openingShotSkill.HasConfirmedTarget)
        {
            openingShotScopePreview?.HideScope();
        }
    }

    private void ApplyOrderSkillButtonVisualState(bool openingShotLocked)
    {
        if (orderSkillButton == null)
        {
            return;
        }

        if (orderSkillButtonLockView != null)
        {
            orderSkillButtonLockView.SetVisualLocked(openingShotLocked);
            return;
        }

        orderSkillButton.interactable = !openingShotLocked;
    }

    private void ApplyOpeningShotTargetingUiBlockState()
    {
        bool isTargeting = openingShotSkill != null && openingShotSkill.isTargetingMode;
        SetTargetingUiClickBlocked(isTargeting);

        if (openingShotSkill == null || openingShotScopePreview == null)
        {
            return;
        }

        if (isTargeting)
        {
            // 스코프 조준과 배치 프리뷰가 동시에 보이지 않도록 타겟팅 진입 시 배치를 먼저 취소한다.
            if (placementController != null && placementController.IsPlacing)
            {
                placementController.CancelPlacement();
            }

            openingShotScopePreview.ShowScope();
            return;
        }

        openingShotScopePreview.HideScope();
    }

    private bool IsOrderSkillButtonLocked()
    {
        return IsOrderSkillButtonLockedByOpeningShot() || IsOrderSkillButtonLockedByInputState();
    }

    private bool IsOrderSkillButtonLockedByOpeningShot()
    {
        return openingShotSkill != null
            && (openingShotSkill.isTargetingMode || openingShotSkill.HasConfirmedTarget);
    }

    private bool IsOrderSkillButtonLockedByInputState()
    {
        bool placementLocked = placementController != null && placementController.IsPlacing;
        bool simulationLocked = simulationController != null && simulationController._isRunning;

        return placementLocked || simulationLocked;
    }

    private void RefreshOrderSkillPlacementStateIfNeeded()
    {
        bool isPlacing = placementController != null && placementController.IsPlacing;
        if (isPlacing == lastOrderSkillPlacementState)
        {
            return;
        }

        ApplyOrderSkillButtonState();
    }

    private void CacheOrderSkillLockStates()
    {
        lastOrderSkillPlacementState = placementController != null && placementController.IsPlacing;
    }

    private void SetOrderSkillButtonClickBlocked(bool blocked)
    {
        if (orderSkillButtonLockView != null)
        {
            orderSkillButtonLockView.SetClickBlocked(blocked);
        }
    }

    private void SetTargetingUiClickBlocked(bool blocked)
    {
        Canvas canvas = ResolveTargetingUiCanvas();
        Transform parent = canvas != null ? canvas.transform : null;
        targetingUiClickBlocker = SetTransparentClickBlocker(
            targetingUiClickBlocker,
            parent,
            "OpeningShotUiClickBlocker",
            blocked);
    }

    private GameObject SetTransparentClickBlocker(
        GameObject blocker,
        Transform parent,
        string blockerName,
        bool blocked)
    {
        // 같은 패턴의 UI 잠금을 한 곳에서 만들고 켜서 버튼별 중복 생성 코드를 줄인다.
        if (blocker == null)
        {
            if (parent == null)
            {
                return null;
            }

            blocker = CreateTransparentClickBlocker(parent, blockerName);
        }

        blocker.SetActive(blocked);
        if (blocked)
        {
            blocker.transform.SetAsLastSibling();
        }

        return blocker;
    }

    private GameObject CreateTransparentClickBlocker(Transform parent, string blockerName)
    {
        GameObject blocker = new(blockerName, typeof(RectTransform), typeof(Image));
        blocker.transform.SetParent(parent, false);

        RectTransform rectTransform = blocker.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = blocker.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        blocker.SetActive(false);
        return blocker;
    }

    private Canvas ResolveTargetingUiCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            return canvas;
        }

        if (skillRoot != null)
        {
            canvas = skillRoot.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                return canvas;
            }
        }

        if (storageRoot != null)
        {
            canvas = storageRoot.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                return canvas;
            }
        }

        if (missionRoot != null)
        {
            canvas = missionRoot.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                return canvas;
            }
        }

        return null;
    }

    private void EnsureSimulationController()
    {
        if (simulationController == null)
        {
            simulationController = SimulationController.Instance;
        }

        if (simulationController == null)
        {
            simulationController = FindAnyObjectByType<SimulationController>();
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

    private void HandleSimulationRunningStateChanged(bool _)
    {
        ApplyStorageInteractionState();
        ApplyOrderSkillButtonState();
    }

    private void ApplyStorageInteractionState()
    {
        // 실행 중에도 남은 수량과 Act/Deact 이미지는 유지하고 배치 입력만 잠근다.
        bool isLocked = simulationController != null && simulationController._isRunning;

        for (int index = 0; index < storageSlots.Count; index++)
        {
            InGameUnitStorageSlotUI slot = storageSlots[index];
            if (slot != null)
            {
                slot.SetInteractionLocked(isLocked);
            }
        }
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int index = root.childCount - 1; index >= 0; index--)
        {
            Destroy(root.GetChild(index).gameObject);
        }
    }

    #endregion
}
