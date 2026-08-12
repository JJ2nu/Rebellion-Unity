using UnityEngine;

/// <summary>
/// 재사용 가능한 로딩 화면의 표현만 담당하는 Passive View다.
/// Task 55에서 게이지·퍼센트 텍스트를 삭제하고 동전 연출(회전 루프 → 감속 정지)로 교체했다.
/// 진행률은 화면에 숫자로 표시하지 않지만, 단조 증가 추적과 완료(1.0) 판정은 유지해 동전 정지 시점을 결정한다.
/// </summary>
public sealed class LoadingScreenView : MonoBehaviour
{
    // 부동소수 합산 오차가 있어도 완료로 인정하는 진행률 경계다.
    private const float CompletedProgressThreshold = 0.999f;

    [Header("Presentation")]
    [SerializeField] private GameObject presentationRoot;
    [SerializeField] private LoadingCoinSpinner coinSpinner;

    [Header("Transition Timing")]
    [SerializeField, Min(0f)] private float fadeOutHoldSeconds = 0.25f;
    [SerializeField, Min(0f)] private float completedHoldSeconds = 0.5f;

    private SceneTransitionOverlay registeredOverlay;
    private float displayedProgress;

    public float FadeOutHoldSeconds => fadeOutHoldSeconds;
    public float CompletedHoldSeconds => completedHoldSeconds;
    public bool IsVisible => presentationRoot != null && presentationRoot.activeSelf;

    /// <summary>
    /// 로딩 완료 뒤 동전 정지 연출까지 끝나 로딩 화면을 걷어도 되는지 여부다.
    /// 동전이 없으면 기다릴 연출이 없으므로 즉시 끝난 것으로 본다.
    /// </summary>
    public bool IsCompletionPresentationFinished => coinSpinner == null || coinSpinner.IsStopFinished;

    private void Awake()
    {
        // Title 재진입 때 새 Prefab 인스턴스가 생겨도 기존 DontDestroyOnLoad View 하나만 유지한다.
        registeredOverlay = SceneTransitionOverlay.Instance;
        if (!registeredOverlay.RegisterLoadingScreen(this))
        {
            Destroy(gameObject);
            return;
        }

        ResetProgress();
        Hide();
    }

    private void OnDestroy()
    {
        // 중복 인스턴스가 파괴될 때 현재 영속 View 등록을 지우지 않도록 자신의 등록만 해제한다.
        if (registeredOverlay != null)
        {
            registeredOverlay.UnregisterLoadingScreen(this);
        }
    }

    /// <summary>
    /// 새 로딩 요청은 항상 0%에서 시작해 이전 흐름의 완료 표시가 다음 요청으로 넘어가지 않게 한다.
    /// </summary>
    public void Show()
    {
        ResetProgress();

        if (presentationRoot != null)
        {
            presentationRoot.SetActive(true);
        }

        // 이미 활성 상태로 재표시될 수도 있으므로 OnEnable에만 의존하지 않고 회전 루프를 명시적으로 되돌린다.
        if (coinSpinner != null)
        {
            coinSpinner.RestartSpin();
        }
    }

    public void Hide()
    {
        if (presentationRoot != null)
        {
            presentationRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 공급자가 역행 값을 전달해도 이미 도달한 진행률은 뒤로 가지 않는다.
    /// 진행률이 완료 경계에 도달하면 동전 회전을 감속 정지 시퀀스로 전환한다.
    /// </summary>
    public void SetProgress(float normalizedProgress)
    {
        displayedProgress = Mathf.Max(displayedProgress, Mathf.Clamp01(normalizedProgress));

        if (displayedProgress >= CompletedProgressThreshold && coinSpinner != null)
        {
            coinSpinner.BeginStop();
        }
    }

    private void ResetProgress()
    {
        displayedProgress = 0f;
    }
}
