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
    [SerializeField] private Vector2 rightAnchoredPosition = new(-50f, 50f);
    [SerializeField] private float horizontalSpacing = 180f;

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

        AlignStoragesFromRight();
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

        InGameUnitStorageSlotUI slot = Instantiate(prefab, transform, false);
        slot.Bind(unitType, deployableCount);
        renderedSlots.Add(slot);
    }

    private void AlignStoragesFromRight()
    {
        int slotCount = renderedSlots.Count;
        for (int index = 0; index < slotCount; index++)
        {
            RectTransform slotTransform = renderedSlots[index].transform as RectTransform;
            if (slotTransform == null)
            {
                continue;
            }

            float xOffset = horizontalSpacing * (slotCount - 1 - index);
            slotTransform.anchoredPosition = rightAnchoredPosition - new Vector2(xOffset, 0f);
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
