using System;
using System.Collections.Generic;
using Rebellion;
using UnityEngine;

public readonly struct InGameStorageSlotViewData
{
    public PieceType UnitType { get; }
    public int DeployableCount { get; }

    public InGameStorageSlotViewData(PieceType unitType, int deployableCount)
    {
        UnitType = unitType;
        DeployableCount = deployableCount;
    }
}

/// <summary>
/// 기존 Storage Prefab의 생성, 정렬, 수량 표시와 잠금 표현만 담당하는 Passive View다.
/// StageData와 게임 Manager를 직접 참조하지 않는다.
/// </summary>
public sealed class InGameStorageView : MonoBehaviour
{
    [Serializable]
    private sealed class UnitStoragePrefabBinding
    {
        public PieceType unitType;
        public InGameUnitStorageSlotUI prefab;
    }

    [SerializeField] private UnitStoragePrefabBinding[] storagePrefabs =
        Array.Empty<UnitStoragePrefabBinding>();

    private readonly List<InGameUnitStorageSlotUI> renderedSlots = new();

    public IReadOnlyList<InGameUnitStorageSlotUI> Render(
        IReadOnlyList<InGameStorageSlotViewData> slotData)
    {
        ClearRenderedSlots();

        if (slotData == null)
        {
            return renderedSlots;
        }

        for (int index = 0; index < slotData.Count; index++)
        {
            InGameStorageSlotViewData data = slotData[index];
            CreateStorage(data.UnitType, data.DeployableCount);
        }

        return renderedSlots;
    }

    public void SetInteractionLocked(bool isLocked)
    {
        for (int index = 0; index < renderedSlots.Count; index++)
        {
            InGameUnitStorageSlotUI slot = renderedSlots[index];
            if (slot != null)
            {
                slot.SetInteractionLocked(isLocked);
            }
        }
    }

    private void CreateStorage(PieceType unitType, int deployableCount)
    {
        if (deployableCount <= 0)
        {
            return;
        }

        InGameUnitStorageSlotUI prefab = FindStoragePrefab(unitType);
        if (prefab == null)
        {
            Debug.LogWarning($"Storage prefab is not assigned. UnitType: {unitType}", this);
            return;
        }

        // 종류별 Prefab의 고정 위치를 유지해 칼은 위, 주먹과 총은 아래에 배치한다.
        // 배치 가능한 종류가 줄어들어도 남은 버튼의 위치는 바뀌지 않는다.
        InGameUnitStorageSlotUI slot = Instantiate(prefab, transform, false);
        slot.Bind(unitType, deployableCount);
        renderedSlots.Add(slot);
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

    private void ClearRenderedSlots()
    {
        renderedSlots.Clear();

        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            GameObject child = transform.GetChild(index).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }
}
