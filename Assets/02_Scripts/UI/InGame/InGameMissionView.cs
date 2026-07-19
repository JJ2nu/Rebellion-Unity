using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mission Prefab의 생성, 텍스트 표시, 간격과 실패 연출 명령만 담당하는 Passive View다.
/// </summary>
public sealed class InGameMissionView : MonoBehaviour
{
    [SerializeField] private InGameMissionSlotUI mainMissionPrefab;
    [SerializeField] private InGameMissionSlotUI subMissionPrefab;
    [SerializeField] private float subMissionVerticalSpacing = 90f;

    private readonly List<InGameMissionSlotUI> subMissionSlots = new();
    private InGameMissionSlotUI mainMissionSlot;

    public void Render(string stageTitle, IReadOnlyList<StageMissionData> subMissions)
    {
        // 같은 StageData를 다시 받아도 기존 슬롯을 먼저 숨기고 제거해 화면 중복을 막는다.
        ClearRenderedMissions();
        mainMissionSlot = CreateMission(mainMissionPrefab, stageTitle, 0f);

        if (subMissions == null)
        {
            return;
        }

        for (int index = 0; index < subMissions.Count; index++)
        {
            StageMissionData mission = subMissions[index];
            if (mission == null || string.IsNullOrWhiteSpace(mission.text))
            {
                continue;
            }

            InGameMissionSlotUI slot = CreateMission(
                subMissionPrefab,
                mission.text,
                -subMissionVerticalSpacing * subMissionSlots.Count);
            if (slot != null)
            {
                subMissionSlots.Add(slot);
            }
        }
    }

    public void ApplyMainMissionProgress(string missionLabel, int deadEnemyCount, int totalEnemyCount)
    {
        mainMissionSlot?.BindEnemyProgress(missionLabel, deadEnemyCount, totalEnemyCount);
    }

    public void ApplyMissionFailures(bool mainMissionFailed, IReadOnlyList<bool> subMissionFailures)
    {
        mainMissionSlot?.ShowFailure(mainMissionFailed);

        for (int index = 0; index < subMissionSlots.Count; index++)
        {
            bool isFailed = subMissionFailures != null &&
                index < subMissionFailures.Count &&
                subMissionFailures[index];
            subMissionSlots[index].ShowFailure(isFailed);
        }
    }

    public void ResetMissionFailures()
    {
        mainMissionSlot?.ResetFailureImmediate();
        foreach (InGameMissionSlotUI slot in subMissionSlots)
        {
            slot?.ResetFailureImmediate();
        }
    }

    private InGameMissionSlotUI CreateMission(InGameMissionSlotUI prefab, string mission, float yOffset)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"{nameof(InGameMissionView)} has no mission prefab assigned.", this);
            return null;
        }

        InGameMissionSlotUI slot = Instantiate(prefab, transform, false);
        RectTransform slotTransform = slot.transform as RectTransform;
        if (slotTransform != null && !Mathf.Approximately(yOffset, 0f))
        {
            slotTransform.anchoredPosition += new Vector2(0f, yOffset);
        }

        slot.Bind(mission);
        slot.ResetFailureImmediate();
        return slot;
    }

    private void ClearRenderedMissions()
    {
        mainMissionSlot = null;
        subMissionSlots.Clear();

        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            GameObject child = transform.GetChild(index).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }
}
