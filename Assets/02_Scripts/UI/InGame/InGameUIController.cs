// 현재 StageData를 기능별 UI Controller에 전달하는 인게임 UI 구성 루트다.

using UnityEngine;

public sealed class InGameUIController : MonoBehaviour
{
    [SerializeField] private InGameMissionUIController missionController;
    [SerializeField] private InGameStorageUIController storageController;
    [SerializeField] private OrderSkillUIController orderSkillController;

    private void Awake()
    {
        ResolveControllers();
        WarnIfControllerIsMissing(missionController, "Mission");
        WarnIfControllerIsMissing(storageController, "Storage");
        WarnIfControllerIsMissing(orderSkillController, "Order Skill");

        StageManager.StageLoaded += HandleStageLoaded;
    }

    private void Start()
    {
        ResolveControllers();
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

        ResolveControllers();
        missionController?.Bind(data);
        storageController?.Bind(data);
        orderSkillController?.Bind(data);
    }

    private void ResolveControllers()
    {
        // UI 리팩터링 이전 Stage 씬에는 새 기능별 Controller 참조가 직렬화되어 있지 않다.
        // 같은 Canvas 아래의 Controller를 자동으로 찾아 기존 씬도 데이터 바인딩을 계속 받게 한다.
        missionController ??= GetComponentInChildren<InGameMissionUIController>(true);
        storageController ??= GetComponentInChildren<InGameStorageUIController>(true);
        orderSkillController ??= GetComponentInChildren<OrderSkillUIController>(true);
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
