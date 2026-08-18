using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mission Prefab의 생성, 텍스트 표시, 간격과 실패·클리어 연출 명령만 담당하는 Passive View다.
/// </summary>
public sealed class InGameMissionView : MonoBehaviour
{
    [SerializeField] private InGameMissionSlotUI mainMissionPrefab;
    [SerializeField] private InGameMissionSlotUI subMissionPrefab;
    [SerializeField] private float subMissionVerticalSpacing = 90f;

    // 실패 취소선과 클리어 동그라미를 하나의 대기열로 묶어 슬롯별 연출이 겹치지 않게 순차 재생한다.
    private readonly struct ResultMark
    {
        public ResultMark(InGameMissionSlotUI slot, bool isSuccess)
        {
            Slot = slot;
            IsSuccess = isSuccess;
        }

        public InGameMissionSlotUI Slot { get; }
        public bool IsSuccess { get; }
    }

    private readonly List<InGameMissionSlotUI> subMissionSlots = new();
    private readonly Queue<ResultMark> resultMarkQueue = new();
    private readonly HashSet<InGameMissionSlotUI> markedSlots = new();
    private InGameMissionSlotUI mainMissionSlot;
    private Coroutine resultMarkSequenceCoroutine;

    private void OnDisable()
    {
        StopResultMarkSequence();
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
        EnqueueResultMark(missionIndex, isSuccess: false);
    }

    public void ShowMissionSuccess(int missionIndex)
    {
        EnqueueResultMark(missionIndex, isSuccess: true);
    }

    public void ResetMissionResultMarks()
    {
        StopResultMarkSequence();
        resultMarkQueue.Clear();
        markedSlots.Clear();
        mainMissionSlot?.ResetFailureImmediate();
        mainMissionSlot?.ResetSuccessImmediate();
        foreach (InGameMissionSlotUI slot in subMissionSlots)
        {
            slot?.ResetFailureImmediate();
            slot?.ResetSuccessImmediate();
        }
    }

    private void EnqueueResultMark(int missionIndex, bool isSuccess)
    {
        InGameMissionSlotUI slot = GetMissionSlot(missionIndex);
        if (slot == null || !markedSlots.Add(slot))
        {
            return;
        }

        // 새 연출이 재생 중 발생해도 현재 연출을 중단하지 않고 대기열 뒤에서 이어서 재생한다.
        resultMarkQueue.Enqueue(new ResultMark(slot, isSuccess));
        if (resultMarkSequenceCoroutine == null)
        {
            resultMarkSequenceCoroutine = StartCoroutine(PlayResultMarkSequence());
        }
    }

    private IEnumerator PlayResultMarkSequence()
    {
        while (resultMarkQueue.Count > 0)
        {
            ResultMark mark = resultMarkQueue.Dequeue();
            if (mark.Slot == null)
            {
                continue;
            }

            if (mark.IsSuccess)
            {
                mark.Slot.ShowSuccess(true);
                yield return new WaitForSecondsRealtime(mark.Slot.ClearSequenceDuration);
            }
            else
            {
                mark.Slot.ShowFailure(true);
                yield return new WaitForSecondsRealtime(mark.Slot.FailSequenceDuration);
            }
        }

        resultMarkSequenceCoroutine = null;
    }

    private void StopResultMarkSequence()
    {
        if (resultMarkSequenceCoroutine == null)
        {
            return;
        }

        StopCoroutine(resultMarkSequenceCoroutine);
        resultMarkSequenceCoroutine = null;
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
        StopResultMarkSequence();
        resultMarkQueue.Clear();
        markedSlots.Clear();
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
