// 스테이지 JSON에서 현재 레벨의 UI 데이터를 읽고 미션, 대원 배치 버튼, 스킬 버튼을 생성한다.

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

    #endregion

    #region Unity Events

    private void Awake()
    {
        if (placementController == null)
        {
            placementController = FindObjectOfType<PlacementController>();
        }

        StageManager.StageLoaded += HandleStageLoaded;
    }

    private void Start()
    {
        StageData current = StageManager.Instance?.CurrentStageData;
        if (current != null)
        {
            Bind(current);
        }
    }

    private void OnDestroy()
    {
        StageManager.StageLoaded -= HandleStageLoaded;
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
        ClearChildren(missionRoot);
        ClearChildren(storageRoot);
        ClearChildren(skillRoot);

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
