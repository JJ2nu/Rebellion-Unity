// CSV에서 현재 레벨의 UI 데이터를 읽고 미션, 대원 배치 버튼, 스킬 버튼을 생성한다.

using System;
using System.Collections.Generic;
using Rebellion;
using UnityEngine;

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

    [Header("Data")]
    [SerializeField] private TextAsset uiDataCsv;
    [SerializeField] private string currentLevel = "Campaign 1";

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

    [Header("Placement")]
    [SerializeField] private PlacementController placementController;

    private InGameUIDataTable dataTable;
    private bool isInitialized;

    #endregion

    #region Unity Events

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        ApplyLevel(currentLevel);
    }

    #endregion

    #region Public Methods

    public void ApplyLevel(string level)
    {
        Initialize();

        if (!dataTable.TryGet(level, out InGameUIData data))
        {
            Debug.LogError($"InGame UI data was not found. Level: {level}", this);
            return;
        }

        currentLevel = level;
        Bind(data);
    }

    #endregion

    #region Initialization

    private void Initialize()
    {
        if (isInitialized)
        {
            return;
        }

        dataTable = InGameUIDataTable.FromCsv(uiDataCsv);
        if (placementController == null)
        {
            placementController = FindObjectOfType<PlacementController>();
        }
        isInitialized = true;
    }

    #endregion

    #region Binding

    private void Bind(InGameUIData data)
    {
        ClearChildren(missionRoot);
        ClearChildren(storageRoot);
        ClearChildren(skillRoot);

        CreateMission(mainMissionPrefab, data.MainMission, 0f);

        int subMissionIndex = 0;
        if (data.HasSubMission1)
        {
            CreateSubMission(data.SubMission1, subMissionIndex++);
        }

        if (data.HasSubMission2)
        {
            CreateSubMission(data.SubMission2, subMissionIndex);
        }

        CreateStorage(PieceType.Brawler, data.Brawler);
        CreateStorage(PieceType.Slasher, data.Slasher);
        CreateStorage(PieceType.Gunman, data.Gunman);
        AlignStoragesFromRight();

        if (data.CanUseOrder)
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
        if (placementController == null)
        {
            Debug.LogWarning("Placement controller is not assigned.", this);
            return;
        }

        placementController.BeginPlacement(slot);
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

        Instantiate(orderSkillButtonPrefab, skillRoot, false);
    }

    #endregion

    #region Helpers

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
