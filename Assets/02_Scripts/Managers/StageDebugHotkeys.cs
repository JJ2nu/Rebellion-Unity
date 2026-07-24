using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Editor Play Mode와 Development Build에서만 F1~F9를 캠페인 Stage 시작 명령으로,
/// F12를 Title 복귀 명령으로 변환한다.
/// Release Build에는 생성 함수와 입력 감시 코드가 컴파일되지 않는다.
/// </summary>
public sealed class StageDebugHotkeys : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static StageDebugHotkeys instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateForDebugRuntime()
    {
        if (instance != null)
        {
            return;
        }

        GameObject hotkeyObject = new(nameof(StageDebugHotkeys));
        hotkeyObject.AddComponent<StageDebugHotkeys>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.f12Key.wasPressedThisFrame)
        {
            Debug.Log("[StageDebugHotkeys] F12 -> Title", this);
            GameFlowManager.ReturnToTitleForDebug();
            return;
        }

        int stageNumber = GetRequestedStageNumber(keyboard);
        if (stageNumber <= 0)
        {
            return;
        }

        string stageId = $"stage_{stageNumber:000}";
        Debug.Log($"[StageDebugHotkeys] F{stageNumber} -> {stageId}", this);
        GameFlowManager.TryStartDebugCampaign(stageId);
    }

    private static int GetRequestedStageNumber(Keyboard keyboard)
    {
        if (keyboard.f1Key.wasPressedThisFrame)
        {
            return 1;
        }

        if (keyboard.f2Key.wasPressedThisFrame)
        {
            return 2;
        }

        if (keyboard.f3Key.wasPressedThisFrame)
        {
            return 3;
        }

        if (keyboard.f4Key.wasPressedThisFrame)
        {
            return 4;
        }

        if (keyboard.f5Key.wasPressedThisFrame)
        {
            return 5;
        }

        if (keyboard.f6Key.wasPressedThisFrame)
        {
            return 6;
        }

        if (keyboard.f7Key.wasPressedThisFrame)
        {
            return 7;
        }

        if (keyboard.f8Key.wasPressedThisFrame)
        {
            return 8;
        }

        if (keyboard.f9Key.wasPressedThisFrame)
        {
            return 9;
        }

        return 0;
    }
#endif
}
