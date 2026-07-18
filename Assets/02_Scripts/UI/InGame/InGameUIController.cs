// 현재 StageData를 기능별 UI Controller에 전달하는 인게임 UI 구성 루트다.

using UnityEngine;

public sealed class InGameUIController : MonoBehaviour
{
    [SerializeField] private InGameMissionUIController missionController;
    [SerializeField] private InGameStorageUIController storageController;
    [SerializeField] private OrderSkillUIController orderSkillController;

    private void Awake()
    {
        WarnIfControllerIsMissing(missionController, "Mission");
        WarnIfControllerIsMissing(storageController, "Storage");
        WarnIfControllerIsMissing(orderSkillController, "Order Skill");

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

    private void HandleStageLoaded(StageData data)
    {
        Bind(data);
    }

    private void Bind(StageData data)
    {
        if (data == null)
        {
            Debug.LogWarning($"{nameof(InGameUIController)} cannot bind null StageData.", this);
            return;
        }

        missionController?.Bind(data);
        storageController?.Bind(data);
        orderSkillController?.Bind(data);
    }

    private void WarnIfControllerIsMissing(Object controller, string featureName)
    {
        if (controller == null)
        {
            Debug.LogWarning(
                $"{nameof(InGameUIController)} has no {featureName} controller assigned.",
                this);
        }
    }
}
