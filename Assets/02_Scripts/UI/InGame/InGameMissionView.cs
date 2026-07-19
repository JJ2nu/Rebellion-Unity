using UnityEngine;

/// <summary>
/// 기존 Mission Prefab의 생성, 텍스트 표시, 간격 적용만 담당하는 Passive View다.
/// StageData와 게임 Manager를 직접 참조하지 않고 Controller가 전달한 문자열만 화면에 반영한다.
/// </summary>
public sealed class InGameMissionView : MonoBehaviour
{
    [SerializeField] private InGameMissionSlotUI mainMissionPrefab;
    [SerializeField] private InGameMissionSlotUI subMissionPrefab;
    [SerializeField] private float subMissionVerticalSpacing = 90f;

    private InGameMissionSlotUI mainMissionSlot;

    public void Render(string mainMission, string subMission1, string subMission2)
    {
        // 같은 StageData를 다시 받아도 기존 슬롯을 먼저 숨기고 제거해 화면 중복을 막는다.
        ClearRenderedMissions();
        mainMissionSlot = CreateMission(mainMissionPrefab, mainMission, 0f);

        int subMissionIndex = 0;
        if (!string.IsNullOrWhiteSpace(subMission1))
        {
            CreateSubMission(subMission1, subMissionIndex++);
        }

        if (!string.IsNullOrWhiteSpace(subMission2))
        {
            CreateSubMission(subMission2, subMissionIndex);
        }
    }

    public void ApplyMainMissionProgress(int deadEnemyCount, int totalEnemyCount)
    {
        mainMissionSlot?.BindEnemyProgress(deadEnemyCount, totalEnemyCount);
    }

    private void CreateSubMission(string mission, int index)
    {
        CreateMission(subMissionPrefab, mission, -subMissionVerticalSpacing * index);
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
        return slot;
    }

    private void ClearRenderedMissions()
    {
        mainMissionSlot = null;

        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            GameObject child = transform.GetChild(index).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }
}
