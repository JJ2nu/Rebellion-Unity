// 인게임 미션 슬롯 프리팹의 텍스트를 갱신한다.

using TMPro;
using UnityEngine;

public sealed class InGameMissionSlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text missionText;

    public void Bind(string mission)
    {
        if (missionText == null)
        {
            Debug.LogWarning($"{nameof(InGameMissionSlotUI)} has no mission text assigned.", this);
            return;
        }

        missionText.text = mission;
    }
}
