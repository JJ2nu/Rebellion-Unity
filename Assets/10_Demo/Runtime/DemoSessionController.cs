#if UNITY_EDITOR || REBELLION_DEMO_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 시연 한 라운드의 실제 경과시간, TimeOver 홍보 페이지와 명시적인 초기화 시점을 관리한다.
/// DemoBootstrap에서 한 번 생성되어 Campaign, Dialogue와 향후 Challenge Scene 사이에 유지된다.
/// </summary>
public sealed class DemoSessionController : MonoBehaviour
{
    private const string BootstrapSceneName = "DemoBootstrap";
    private const string TimeOverSceneName = "DemoTimeOver";
    private const string TitleSceneName = "Title";
    private const string ChallengeSceneName = "Challenge";

    private enum DemoSessionState
    {
        Idle,
        Running,
        EndingGrace,
        LoadingTimeOver,
        TimeOver,
        ReturningToTitle,
    }

    private static DemoSessionController instance;

    [Header("Demo Runtime")]
    [SerializeField] private bool allowEditorPreview = true;
    [SerializeField] private bool enableChallengeMode;

    [Header("Session Time")]
    [SerializeField, Min(0.1f)] private float sessionDurationMinutes = 30f;
    [SerializeField, Min(0f)] private float timeOverDurationSeconds = 10f;

    [Header("Timer Warning")]
    [SerializeField, Min(0f)] private float warningThresholdMinutes = 5f;
    [SerializeField, Min(0f)] private float criticalThresholdMinutes = 1f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new(1f, 0.75f, 0.15f, 1f);
    [SerializeField] private Color criticalColor = new(1f, 0.2f, 0.2f, 1f);
    [SerializeField, Min(0.1f)] private float criticalBlinkFrequency = 2f;
    [SerializeField, Range(0f, 1f)] private float criticalBlinkMinimumAlpha = 0.2f;

    [Header("View and Input")]
    [SerializeField] private DemoTimerView timerView;
    [SerializeField] private InputActionReference cancelAction;

    private DemoSessionState state = DemoSessionState.Idle;
    private double sessionDeadline;
    private double timeOverDeadline;
    private bool resetWhenTitleLoads;
    private bool cancelActionEnabledByController;

    public static void NotifyCampaignContentVisible()
    {
        instance?.BeginOrContinue();
    }

    public static bool IsSceneSelectionAllowed(string sceneName)
    {
        if (instance == null || sceneName != ChallengeSceneName)
        {
            return true;
        }

        return instance.enableChallengeMode;
    }

    public static void NotifyEndingStarted()
    {
        instance?.EnterEndingGrace();
    }

    public static void NotifyEndingTitleTransitionCompleted()
    {
        instance?.CompleteEndingRound();
    }

    public static bool TryRequestOperatorReset()
    {
        if (instance == null)
        {
            return false;
        }

        instance.RequestOperatorReset();
        return true;
    }

    // 인게임 타이틀 복귀 버튼이 씬 전환 전에 호출해 Title 로드 완료 시 세션 타이머가 초기화되게 한다.
    // 실제 씬 전환은 호출자(GameFlowManager.ReturnToTitleFromInGameButton)가 수행한다.
    public static void NotifyPlayerReturnToTitleRequested()
    {
        instance?.MarkReturningToTitle();
    }

    private bool IsRuntimeEnabled
    {
        get
        {
#if REBELLION_DEMO_BUILD
            return true;
#else
            return allowEditorPreview;
#endif
        }
    }

    private static double RealtimeNow => Time.realtimeSinceStartupAsDouble;

    private void Awake()
    {
        if (!IsRuntimeEnabled)
        {
            Destroy(gameObject);
            return;
        }

        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        // Alt+Tab 중에도 화면 갱신과 만료 Scene 전환이 멈추지 않게 한다.
        Application.runInBackground = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (timerView == null || !timerView.HasRequiredReferences)
        {
            Debug.LogWarning("[DemoSessionController] Timer View reference is incomplete.", this);
        }

        ResetRound();
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == BootstrapSceneName)
        {
            // Demo 전용 첫 Scene은 영속 타이머만 준비하고 기존 Title 흐름으로 진입한다.
            SceneTransitionOverlay.Instance.LoadScene(TitleSceneName);
        }
    }

    private void Update()
    {
        switch (state)
        {
            case DemoSessionState.Running:
                UpdateRunningTimer();
                break;
            case DemoSessionState.TimeOver:
                if (RealtimeNow >= timeOverDeadline)
                {
                    ReturnToTitleFromTimeOver();
                }
                break;
        }
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribeCancelAction();
        instance = null;
    }

    private void OnValidate()
    {
        if (sessionDurationMinutes < 0.1f)
        {
            Debug.LogWarning(
                "[DemoSessionController] Session duration must be at least 0.1 minute.",
                this);
        }

        if (timeOverDurationSeconds < 0f)
        {
            Debug.LogWarning(
                "[DemoSessionController] TimeOver duration cannot be negative.",
                this);
        }

        if (warningThresholdMinutes < 0f)
        {
            Debug.LogWarning(
                "[DemoSessionController] Warning threshold cannot be negative.",
                this);
        }

        if (criticalThresholdMinutes < 0f ||
            criticalThresholdMinutes > Mathf.Max(0f, warningThresholdMinutes))
        {
            Debug.LogWarning(
                "[DemoSessionController] Critical threshold must be between zero and the warning threshold.",
                this);
        }

        if (criticalBlinkFrequency < 0.1f)
        {
            Debug.LogWarning(
                "[DemoSessionController] Critical blink frequency must be at least 0.1 Hz.",
                this);
        }

        sessionDurationMinutes = Mathf.Max(0.1f, sessionDurationMinutes);
        timeOverDurationSeconds = Mathf.Max(0f, timeOverDurationSeconds);
        warningThresholdMinutes = Mathf.Max(0f, warningThresholdMinutes);
        criticalThresholdMinutes = Mathf.Clamp(
            criticalThresholdMinutes,
            0f,
            warningThresholdMinutes);
        criticalBlinkFrequency = Mathf.Max(0.1f, criticalBlinkFrequency);
    }

    private void BeginOrContinue()
    {
        if (state != DemoSessionState.Idle)
        {
            // Title을 경유한 모드 전환은 같은 종료 시각을 유지해 제한 시간이 늘어나지 않게 한다.
            return;
        }

        sessionDeadline = RealtimeNow + sessionDurationMinutes * 60d;
        state = DemoSessionState.Running;
        timerView?.SetVisible(true);
        UpdateRunningTimer();
    }

    private void UpdateRunningTimer()
    {
        double remainingSeconds = System.Math.Max(0d, sessionDeadline - RealtimeNow);
        RenderTimer(remainingSeconds);

        if (remainingSeconds <= 0d)
        {
            RequestTimeOver();
        }
    }

    private void RenderTimer(double remainingSeconds)
    {
        float warningSeconds = warningThresholdMinutes * 60f;
        float criticalSeconds = criticalThresholdMinutes * 60f;
        Color color = normalColor;
        float alpha = 1f;

        if (remainingSeconds <= criticalSeconds)
        {
            color = criticalColor;
            double blinkPhase = RealtimeNow * criticalBlinkFrequency % 1d;
            if (blinkPhase >= 0.5d)
            {
                alpha = criticalBlinkMinimumAlpha;
            }
        }
        else if (remainingSeconds <= warningSeconds)
        {
            color = warningColor;
        }

        timerView?.Render(remainingSeconds, color, alpha);
    }

    private void RequestTimeOver()
    {
        if (state != DemoSessionState.Running)
        {
            return;
        }

        state = DemoSessionState.LoadingTimeOver;
        timerView?.SetVisible(false);
        UnsubscribeCancelAction();
        GameFlowManager.ReturnToDemoTimeOver();
    }

    private void EnterEndingGrace()
    {
        if (state != DemoSessionState.Running)
        {
            return;
        }

        // 엔딩에 도달한 참가자는 제한 시간이 끝나도 오디오드라마와 Title 배경 전환을 끝까지 본다.
        state = DemoSessionState.EndingGrace;
        timerView?.SetVisible(false);
    }

    private void CompleteEndingRound()
    {
        if (state != DemoSessionState.EndingGrace &&
            state != DemoSessionState.Running)
        {
            return;
        }

        ResetRound();
    }

    private void RequestOperatorReset()
    {
        // 이미 복귀 중이면 중복 씬 전환 요청을 만들지 않는다.
        if (!MarkReturningToTitle())
        {
            return;
        }

        GameFlowManager.ReturnToTitleForDebug();
    }

    // 복귀 상태 표시와 타이머 정리만 수행하는 공용 경로. F12 오퍼레이터 리셋과 인게임 복귀 버튼이 공유한다.
    private bool MarkReturningToTitle()
    {
        if (state == DemoSessionState.ReturningToTitle)
        {
            return false;
        }

        state = DemoSessionState.ReturningToTitle;
        resetWhenTitleLoads = true;
        timerView?.SetVisible(false);
        UnsubscribeCancelAction();
        return true;
    }

    private void ReturnToTitleFromTimeOver()
    {
        if (state != DemoSessionState.TimeOver)
        {
            return;
        }

        state = DemoSessionState.ReturningToTitle;
        resetWhenTitleLoads = true;
        UnsubscribeCancelAction();
        SceneTransitionOverlay.Instance.LoadScene(TitleSceneName);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (scene.name == ChallengeSceneName && enableChallengeMode)
        {
            // 향후 Challenge 최초 진입도 버튼 클릭이 아니라 Scene 로드 완료부터 시간을 계산한다.
            BeginOrContinue();
        }

        if (scene.name == TimeOverSceneName &&
            state == DemoSessionState.LoadingTimeOver)
        {
            state = DemoSessionState.TimeOver;
            timeOverDeadline = RealtimeNow + timeOverDurationSeconds;
            SubscribeCancelAction();
            return;
        }

        if (scene.name == TitleSceneName && resetWhenTitleLoads)
        {
            ResetRound();
        }
    }

    private void SubscribeCancelAction()
    {
        if (cancelAction == null || cancelAction.action == null)
        {
            Debug.LogWarning("[DemoSessionController] UI/Cancel action is missing.", this);
            return;
        }

        InputAction action = cancelAction.action;
        action.performed -= HandleCancelPerformed;
        action.performed += HandleCancelPerformed;
        if (!action.enabled)
        {
            action.Enable();
            cancelActionEnabledByController = true;
        }
    }

    private void UnsubscribeCancelAction()
    {
        if (cancelAction == null || cancelAction.action == null)
        {
            cancelActionEnabledByController = false;
            return;
        }

        InputAction action = cancelAction.action;
        action.performed -= HandleCancelPerformed;
        if (cancelActionEnabledByController && action.enabled)
        {
            action.Disable();
        }

        cancelActionEnabledByController = false;
    }

    private void HandleCancelPerformed(InputAction.CallbackContext _)
    {
        ReturnToTitleFromTimeOver();
    }

    private void ResetRound()
    {
        state = DemoSessionState.Idle;
        sessionDeadline = 0d;
        timeOverDeadline = 0d;
        resetWhenTitleLoads = false;
        timerView?.SetVisible(false);
        UnsubscribeCancelAction();
    }
}
#endif
