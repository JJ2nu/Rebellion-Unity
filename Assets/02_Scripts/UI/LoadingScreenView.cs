using TMPro;
using UnityEngine;

/// <summary>
/// 재사용 가능한 로딩 화면의 표현만 담당하는 Passive View다.
/// Prefab의 아트는 교체할 수 있지만 게이지와 정수 퍼센트는 동일한 단조 증가 진행률을 유지한다.
/// </summary>
public sealed class LoadingScreenView : MonoBehaviour
{
    [Header("Presentation")]
    [SerializeField] private GameObject presentationRoot;
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private TMP_Text percentageText;

    [Header("Transition Timing")]
    [SerializeField, Min(0f)] private float fadeOutHoldSeconds = 0.25f;
    [SerializeField, Min(0f)] private float completedHoldSeconds = 0.5f;

    private SceneTransitionOverlay registeredOverlay;
    private float displayedProgress;

    public float FadeOutHoldSeconds => fadeOutHoldSeconds;
    public float CompletedHoldSeconds => completedHoldSeconds;
    public bool IsVisible => presentationRoot != null && presentationRoot.activeSelf;

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
    }

    public void Hide()
    {
        if (presentationRoot != null)
        {
            presentationRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 공급자가 역행 값을 전달해도 플레이어에게 이미 보인 게이지와 같은 정수 퍼센트는 뒤로 가지 않는다.
    /// </summary>
    public void SetProgress(float normalizedProgress)
    {
        displayedProgress = Mathf.Max(displayedProgress, Mathf.Clamp01(normalizedProgress));

        if (progressFill != null)
        {
            progressFill.anchorMax = new Vector2(displayedProgress, 1f);
        }

        if (percentageText != null)
        {
            percentageText.text = $"{Mathf.RoundToInt(displayedProgress * 100f)}%";
        }
    }

    private void ResetProgress()
    {
        displayedProgress = 0f;

        if (progressFill != null)
        {
            progressFill.anchorMax = new Vector2(0f, 1f);
        }

        if (percentageText != null)
        {
            percentageText.text = "0%";
        }
    }
}
