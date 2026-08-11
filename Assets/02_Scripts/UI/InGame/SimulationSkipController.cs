using UnityEngine;

/// <summary>
/// 시뮬레이션 실행 상태를 스킵 버튼 표시로 변환하고, 스킵 요청 시 남은 연출을 고속 진행으로 건너뛴다.
/// 전투 판정은 기존 Simulation 코루틴이 같은 경로로 그대로 수행하므로 스킵 여부는 결과를 바꾸지 않는다.
/// </summary>
[RequireComponent(typeof(SimulationSkipView))]
public sealed class SimulationSkipController : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private SimulationController simulationController;
    [SerializeField] private CombatPresentationDirector presentationDirector;
    [SerializeField] private OpeningShotPresentationView openingShotView;

    [Header("Skip")]
    [SerializeField, Min(1f)] private float skipTimeScale = 30f;
    // 기본 Maximum Allowed Timestep보다 크게 잡아 한 프레임에 진행 가능한 게임 시간을 확보한다.
    [SerializeField, Min(0.1f)] private float skipMaximumDeltaTime = 0.5f;

    private SimulationSkipView view;
    // SimulationFinished 이후 결과 확정 대기 동안 _isRunning이 true로 남으므로 실행 구간을 이벤트로 직접 추적한다.
    private bool isSimulationExecuting;
    private bool isSkipActive;
    private float previousTimeScale = 1f;
    private float previousMaximumDeltaTime = 1f / 3f;

    private void Awake()
    {
        view = GetComponent<SimulationSkipView>();
    }

    private void OnEnable()
    {
        EnsureBindings();

        if (simulationController != null)
        {
            simulationController.RunningStateChanged -= HandleRunningStateChanged;
            simulationController.RunningStateChanged += HandleRunningStateChanged;
            simulationController.SimulationFinished -= HandleSimulationFinished;
            simulationController.SimulationFinished += HandleSimulationFinished;
            simulationController.SimulationReset -= HandleSimulationReset;
            simulationController.SimulationReset += HandleSimulationReset;
        }

        if (view != null)
        {
            view.SkipRequested -= HandleSkipRequested;
            view.SkipRequested += HandleSkipRequested;
        }

        ApplyVisibility();
    }

    private void OnDisable()
    {
        if (simulationController != null)
        {
            simulationController.RunningStateChanged -= HandleRunningStateChanged;
            simulationController.SimulationFinished -= HandleSimulationFinished;
            simulationController.SimulationReset -= HandleSimulationReset;
        }

        if (view != null)
        {
            view.SkipRequested -= HandleSkipRequested;
        }

        // Scene 전환이나 비활성화가 고속 진행 상태를 남기지 않게 한다.
        EndSkip();
    }

    private void Update()
    {
        // Opening Shot 연출은 unscaled 시간으로 진행돼 배속이 적용되지 않으므로,
        // 스킵 중 시작되는 연출도 기존 스킵 경로(효과 1회 보장)로 즉시 종료시킨다.
        if (isSkipActive && openingShotView != null && openingShotView.IsPresenting)
        {
            openingShotView.RequestSkip();
        }
    }

    private void HandleRunningStateChanged(bool isRunning)
    {
        if (isRunning)
        {
            isSimulationExecuting = true;
        }
        else
        {
            // Retry/Reset 경로의 실행 종료도 스킵 상태를 함께 정리한다.
            isSimulationExecuting = false;
            EndSkip();
        }

        ApplyVisibility();
    }

    private void HandleSimulationFinished(SimulationController.SimulationResult _)
    {
        // 결과가 나온 순간 고속 진행을 끝내 결과 화면은 정상 속도로 표시한다.
        isSimulationExecuting = false;
        EndSkip();
        ApplyVisibility();
    }

    private void HandleSimulationReset()
    {
        isSimulationExecuting = false;
        EndSkip();
        ApplyVisibility();
    }

    private void HandleSkipRequested()
    {
        if (isSkipActive || !isSimulationExecuting)
        {
            return;
        }

        BeginSkip();
        // 스킵이 걸리면 버튼을 숨겨 중복 입력을 막는다.
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        view?.SetVisible(isSimulationExecuting && !isSkipActive);
    }

    private void BeginSkip()
    {
        isSkipActive = true;

        // 히트스톱이 활성 상태에서 저장해 둔 timeScale을 나중에 복원하며 배속을 덮어쓰지 않도록,
        // 억제(활성 히트스톱 즉시 종료 포함)를 먼저 적용한 뒤 현재 값을 보관하고 배속을 건다.
        presentationDirector?.SetHitStopSuppressed(true);
        // 배속 재생되는 카메라 무빙이 어지럽지 않게 기본 구도로 즉시 복귀시키고 스킵 동안 카메라 연출을 차단한다.
        presentationDirector?.SetCombatCameraSuppressed(true);
        previousTimeScale = Time.timeScale;
        previousMaximumDeltaTime = Time.maximumDeltaTime;
        Time.timeScale = skipTimeScale;
        Time.maximumDeltaTime = skipMaximumDeltaTime;

        if (openingShotView != null && openingShotView.IsPresenting)
        {
            openingShotView.RequestSkip();
        }
    }

    private void EndSkip()
    {
        if (!isSkipActive)
        {
            return;
        }

        isSkipActive = false;
        Time.timeScale = previousTimeScale;
        Time.maximumDeltaTime = previousMaximumDeltaTime;
        presentationDirector?.SetHitStopSuppressed(false);
        presentationDirector?.SetCombatCameraSuppressed(false);
    }

    private void EnsureBindings()
    {
        if (simulationController == null)
        {
            simulationController = SimulationController.Instance;
        }
    }
}
