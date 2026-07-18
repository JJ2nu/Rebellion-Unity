using UnityEngine;

/// <summary>
/// StageData의 미션 문구를 Passive View에 전달하는 Mission UI Controller다.
/// 스테이지 로드 구독은 상위 InGameUIController에 유지해 기능별 Controller가 게임 전역 상태를 직접 찾지 않게 한다.
/// </summary>
[RequireComponent(typeof(InGameMissionView))]
public sealed class InGameMissionUIController : MonoBehaviour
{
    private InGameMissionView view;

    private void Awake()
    {
        // Controller와 View를 같은 Missions 루트에 두어 Scene 참조를 단순하게 유지한다.
        view = GetComponent<InGameMissionView>();
    }

    public void Bind(StageData data)
    {
        if (data == null)
        {
            Debug.LogWarning($"{nameof(InGameMissionUIController)} cannot bind null StageData.", this);
            return;
        }

        if (view == null)
        {
            Debug.LogWarning($"{nameof(InGameMissionUIController)} has no mission view assigned.", this);
            return;
        }

        view.Render(data.mainMission, data.subMission1, data.subMission2);
    }
}
