using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD || REBELLION_DEMO_BUILD
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Editor Play Mode와 Development Build에서는 F1~F9를 캠페인 Stage 시작 명령으로 사용한다.
/// F12 Title 복귀는 시연 빌드에도 포함해 관람자 라운드와 작성 중 로그를 안전하게 정리한다.
/// </summary>
public sealed class StageDebugHotkeys : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || REBELLION_DEMO_BUILD
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

#if UNITY_EDITOR || REBELLION_DEMO_BUILD
            if (DemoSessionController.TryRequestOperatorReset())
            {
                return;
            }
#endif

            GameFlowManager.ReturnToTitleForDebug();
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int stageNumber = GetRequestedStageNumber(keyboard);
        if (stageNumber <= 0)
        {
            return;
        }

        string stageId = $"stage_{stageNumber:000}";
        Debug.Log($"[StageDebugHotkeys] F{stageNumber} -> {stageId}", this);
        GameFlowManager.TryStartDebugCampaign(stageId);
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
#endif
}
