// 인게임 미션 슬롯 프리팹의 텍스트를 갱신한다.

using TMPro;
using UnityEngine;

public sealed class InGameMissionSlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text missionText;
    [SerializeField] private TMP_Text missionResultText;

    public void Bind(string mission)
    {
        if (missionText == null)
        {
            Debug.LogWarning($"{nameof(InGameMissionSlotUI)} has no mission text assigned.", this);
            return;
        }

        missionText.text = mission;
    }

    public void BindEnemyProgress(int deadEnemyCount, int totalEnemyCount)
    {
        if (missionResultText == null)
        {
            Debug.LogWarning($"{nameof(InGameMissionSlotUI)} has no mission result text assigned.", this);
            return;
        }

        int safeTotalEnemyCount = Mathf.Max(0, totalEnemyCount);
        int safeDeadEnemyCount = Mathf.Clamp(deadEnemyCount, 0, safeTotalEnemyCount);

        // View가 전달한 최종 표시값만 사용해 시뮬레이션 상태를 Slot에서 해석하지 않는다.
        missionResultText.text = $"모든 적 처치 ({safeDeadEnemyCount}/{safeTotalEnemyCount})";
    }
}
