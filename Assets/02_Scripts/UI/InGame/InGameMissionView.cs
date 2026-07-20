using System.Collections;
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
    private readonly Queue<InGameMissionSlotUI> failureQueue = new();
    private readonly HashSet<InGameMissionSlotUI> failedSlots = new();
    private InGameMissionSlotUI mainMissionSlot;
    private Coroutine failureSequenceCoroutine;

    private void OnDisable()
    {
        StopFailureSequence();
    }

    public void Render(string stageTitle, IReadOnlyList<string> subMissions)
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
            string missionText = subMissions[index];
            if (string.IsNullOrWhiteSpace(missionText))
            {
                continue;
            }

            InGameMissionSlotUI slot = CreateMission(
                subMissionPrefab,
                missionText,
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

    public void ShowMissionFailure(int missionIndex)
    {
        InGameMissionSlotUI slot = GetMissionSlot(missionIndex);
        if (slot == null || !failedSlots.Add(slot))
        {
            return;
        }

        // 새 실패가 연출 중 발생해도 현재 실패선을 중단하지 않고 대기열 뒤에서 이어서 재생한다.
        failureQueue.Enqueue(slot);
        if (failureSequenceCoroutine == null)
        {
            failureSequenceCoroutine = StartCoroutine(PlayFailureSequence());
        }
    }

    public void ResetMissionFailures()
    {
        StopFailureSequence();
        failureQueue.Clear();
        failedSlots.Clear();
        mainMissionSlot?.ResetFailureImmediate();
        foreach (InGameMissionSlotUI slot in subMissionSlots)
        {
            slot?.ResetFailureImmediate();
        }
    }

    private IEnumerator PlayFailureSequence()
    {
        while (failureQueue.Count > 0)
        {
            InGameMissionSlotUI slot = failureQueue.Dequeue();
            if (slot == null)
            {
                continue;
            }

            slot.ShowFailure(true);
            yield return new WaitForSecondsRealtime(slot.FailSequenceDuration);
        }

        failureSequenceCoroutine = null;
    }

    private void StopFailureSequence()
    {
        if (failureSequenceCoroutine == null)
        {
            return;
        }

        StopCoroutine(failureSequenceCoroutine);
        failureSequenceCoroutine = null;
    }

    private InGameMissionSlotUI GetMissionSlot(int missionIndex)
    {
        if (missionIndex == 0)
        {
            return mainMissionSlot;
        }

        int subMissionIndex = missionIndex - 1;
        return subMissionIndex >= 0 && subMissionIndex < subMissionSlots.Count
            ? subMissionSlots[subMissionIndex]
            : null;
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
        StopFailureSequence();
        failureQueue.Clear();
        failedSlots.Clear();
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
