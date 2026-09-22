using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선계산(Task 72) 결과가 완전승리이고 스테이지의 모든 미션이 성공할 때만 전투 중 상·하 레터박스를 표시한다.
/// 바가 내려오는 동안 게임플레이 UI를 숨기고 Skip을 막으며, 결과 확정 뒤에는 바 퇴장과 같은 시간에 걸쳐 UI를 되돌린다.
/// 결과 UI(미션 마크·Retry/Confirm) 알림은 SimulationController가 PlayExit가 끝난 뒤에 보내므로
/// 이 컴포넌트는 화면 표현만 담당하고 승패·미션 판정 자체는 바꾸지 않는다.
/// Simulator Prefab에 SimulationController와 함께 붙는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PerfectWinLetterboxPresentation : MonoBehaviour
{
    private const string CanvasObjectName = "PerfectWinLetterboxCanvas";
    // 로딩 스파이크나 에디터 정지 뒤 첫 프레임에서 바가 한 번에 튀지 않도록 프레임당 진행 시간을 제한한다.
    private const float MaxFrameDelta = 1f / 15f;

    [Header("Bindings")]
    // Stage.unity의 MissionDefinitionRegistry. 스테이지 미션의 성공 조건을 선계산 결과로 미리 평가할 때 쓴다.
    [SerializeField] private MissionDefinitionRegistry missionDefinitionRegistry;
    // Stage.unity 루트 Canvas의 CanvasGroup(인게임 UI 전체). Scene 인스턴스 오버라이드로 연결하며,
    // 비어 있으면 InGameUIController가 붙은 Canvas에서 찾는다.
    [SerializeField] private CanvasGroup gameplayUiCanvasGroup;

    [Header("Letterbox")]
    // 참고 영상(1080 기준 상·하 각 116px) 계측값. 16:9 뷰포트 높이에 대한 비율이다.
    [SerializeField, Range(0.01f, 0.4f)] private float barHeightRatio = 0.107f;
    [SerializeField, Min(0f)] private float enterDuration = 0.7f;
    [SerializeField, Min(0f)] private float exitDuration = 0.8f;
    // 결과 확정 직후 마지막 사망 연출이 조금 보이도록 바를 유지하는 시간이다.
    [SerializeField, Min(0f)] private float resultHoldDuration = 0.2f;
    [SerializeField] private Color barColor = Color.black;
    // 인게임 Canvas(0)보다 위, Opening Shot 스코프 Canvas(20)와 Skip Canvas(50)보다 아래에 그린다.
    [SerializeField] private int canvasSortingOrder = 10;

    private Canvas letterboxCanvas;
    private RectTransform topBar;
    private RectTransform bottomBar;
    private Coroutine enterRoutine;
    private float barProgress;
    private bool hasEntered;
    private bool isExiting;
    private bool isSkipAllowed = true;
    private float capturedUiAlpha = 1f;
    private bool capturedUiInteractable = true;
    private bool capturedUiBlocksRaycasts = true;

    /// <summary>바 등장 시작부터 퇴장 완료(또는 취소)까지 true다.</summary>
    public bool IsActive { get; private set; }

    /// <summary>바가 완전히 내려온 뒤부터 퇴장 시작 전까지만 Skip을 허용한다. 비활성 상태에서는 항상 허용이다.</summary>
    public bool IsSkipAllowed => isSkipAllowed;

    public event Action SkipAvailabilityChanged;

    private void Update()
    {
        // 창 크기·비율이 바뀌어도 바가 항상 16:9 뷰포트 안쪽에 붙어 있게 매 프레임 다시 맞춘다.
        if (IsActive)
        {
            ApplyBarLayout();
        }
    }

    private void OnDisable()
    {
        // Scene 전환이나 비활성화가 숨겨진 UI 상태를 남기지 않게 한다.
        CancelImmediately();
    }

    /// <summary>
    /// 완전승리이면서 스테이지의 주·서브 미션이 모두 성공으로 예측될 때만 true를 반환한다.
    /// 완전승리는 적 전멸·아군 사망 0·중립 사망 0을 뜻하므로 미션 사실은 그 값으로 고정하고
    /// Opening Shot 사용 여부만 선계산 결과에서 읽는다.
    /// </summary>
    public bool ShouldPlay(
        CombatPredictionResult prediction,
        StageData stageData,
        int totalEnemyCount,
        out string skipReason)
    {
        skipReason = null;

        if (prediction == null)
        {
            skipReason = "prediction missing";
            return false;
        }

        if (!prediction.IsPerfectWin)
        {
            skipReason = $"predictedResult={prediction.PredictedResult}";
            return false;
        }

        if (stageData == null)
        {
            skipReason = "stage data missing";
            return false;
        }

        if (missionDefinitionRegistry == null)
        {
            skipReason = "mission registry missing";
            return false;
        }

        var predictedFacts = new SimulationMissionFacts(
            totalEnemyCount,
            totalEnemyCount,
            0,
            0,
            0,
            prediction.OpeningShotExecuted);

        if (!IsMissionPredictedSuccess(stageData.GetPrimaryMission(), predictedFacts, out skipReason))
        {
            return false;
        }

        foreach (StageMissionData subMission in stageData.GetSubMissions())
        {
            if (subMission == null || string.IsNullOrWhiteSpace(subMission.missionId))
            {
                continue;
            }

            if (!IsMissionPredictedSuccess(subMission, predictedFacts, out skipReason))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 바 등장을 시작한다. 게임플레이 UI는 즉시 숨긴다.
    /// Opening Shot 연출이 시작 시점의 UI alpha를 보관했다가 그대로 복구하므로,
    /// 페이드 도중 값이 보관되지 않도록 한 프레임에 0으로 내린다.
    /// </summary>
    public void BeginEnter()
    {
        EnsureGameplayUiCanvasGroup();
        EnsureLetterboxCanvas();
        StopEnterRoutine();

        if (!IsActive)
        {
            CaptureUiState();
        }

        IsActive = true;
        hasEntered = false;
        isExiting = false;
        SetGameplayUiHidden();
        letterboxCanvas.gameObject.SetActive(true);
        ApplyBarLayout();
        UpdateSkipAvailability();

        enterRoutine = StartCoroutine(EnterRoutine());
    }

    /// <summary>
    /// 결과 확정 뒤 호출한다. 짧은 유지 후 바가 걷히는 동안 UI alpha를 함께 올리고, 끝나면 입력 상태까지 복구한다.
    /// SimulationController 코루틴 안에서 yield return으로 완료를 기다린다.
    /// </summary>
    public IEnumerator PlayExit()
    {
        if (!IsActive)
        {
            yield break;
        }

        StopEnterRoutine();
        isExiting = true;
        UpdateSkipAvailability();

        float holdElapsed = 0f;
        while (holdElapsed < resultHoldDuration)
        {
            holdElapsed += GetFrameDelta();
            yield return null;
        }

        float startProgress = barProgress;
        float elapsed = 0f;
        while (exitDuration > 0f && elapsed < exitDuration)
        {
            elapsed += GetFrameDelta();
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / exitDuration));
            barProgress = Mathf.Lerp(startProgress, 0f, eased);
            SetGameplayUiAlpha(Mathf.Lerp(0f, capturedUiAlpha, eased));
            ApplyBarLayout();
            yield return null;
        }

        barProgress = 0f;
        FinishAndRestore();
    }

    /// <summary>
    /// Retry·Reset·Scene 전환에서 진행 중인 연출을 끊고 UI를 연출 전 상태로 즉시 되돌린다.
    /// </summary>
    public void CancelImmediately()
    {
        StopEnterRoutine();

        if (!IsActive)
        {
            return;
        }

        barProgress = 0f;
        FinishAndRestore();
    }

    private IEnumerator EnterRoutine()
    {
        float startProgress = barProgress;
        float remaining = enterDuration * (1f - startProgress);
        float elapsed = 0f;
        while (remaining > 0f && elapsed < remaining)
        {
            elapsed += GetFrameDelta();
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / remaining));
            barProgress = Mathf.Lerp(startProgress, 1f, eased);
            ApplyBarLayout();
            yield return null;
        }

        barProgress = 1f;
        ApplyBarLayout();
        hasEntered = true;
        enterRoutine = null;
        UpdateSkipAvailability();
    }

    private void FinishAndRestore()
    {
        IsActive = false;
        hasEntered = false;
        isExiting = false;
        RestoreUiState();

        if (letterboxCanvas != null)
        {
            ApplyBarLayout();
            letterboxCanvas.gameObject.SetActive(false);
        }

        UpdateSkipAvailability();
    }

    private static float GetFrameDelta()
    {
        return Mathf.Min(Time.unscaledDeltaTime, MaxFrameDelta);
    }

    private void UpdateSkipAvailability()
    {
        bool allowed = !IsActive || (hasEntered && !isExiting);
        if (isSkipAllowed == allowed)
        {
            return;
        }

        isSkipAllowed = allowed;
        SkipAvailabilityChanged?.Invoke();
    }

    private bool IsMissionPredictedSuccess(
        StageMissionData missionData,
        SimulationMissionFacts predictedFacts,
        out string failReason)
    {
        failReason = null;
        string missionId = missionData?.missionId ?? string.Empty;

        if (!missionDefinitionRegistry.TryGetDefinition(missionId, out MissionDefinition definition) ||
            definition == null)
        {
            failReason = $"mission '{missionId}' definition missing";
            return false;
        }

        // 최종 평가 시점 기준으로 미리 판정한다. 즉시 실패형·시작 시점형 미션도 이 시점에는 확정값을 돌려준다.
        if (!MissionEvaluator.TryEvaluate(
                definition.MissionType,
                definition.EvaluationTiming,
                predictedFacts,
                MissionEvaluationMoment.SimulationFinished,
                out MissionEvaluationState state))
        {
            failReason = $"mission '{missionId}' type unsupported";
            return false;
        }

        if (state != MissionEvaluationState.Succeeded)
        {
            failReason = $"mission '{missionId}' predicted {state}";
            return false;
        }

        return true;
    }

    private void StopEnterRoutine()
    {
        if (enterRoutine != null)
        {
            StopCoroutine(enterRoutine);
            enterRoutine = null;
        }
    }

    private void CaptureUiState()
    {
        if (gameplayUiCanvasGroup == null)
        {
            return;
        }

        capturedUiAlpha = gameplayUiCanvasGroup.alpha;
        capturedUiInteractable = gameplayUiCanvasGroup.interactable;
        capturedUiBlocksRaycasts = gameplayUiCanvasGroup.blocksRaycasts;
    }

    private void SetGameplayUiHidden()
    {
        if (gameplayUiCanvasGroup == null)
        {
            return;
        }

        gameplayUiCanvasGroup.alpha = 0f;
        gameplayUiCanvasGroup.interactable = false;
        gameplayUiCanvasGroup.blocksRaycasts = false;
    }

    private void SetGameplayUiAlpha(float alpha)
    {
        if (gameplayUiCanvasGroup != null)
        {
            gameplayUiCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    private void RestoreUiState()
    {
        if (gameplayUiCanvasGroup == null)
        {
            return;
        }

        gameplayUiCanvasGroup.alpha = capturedUiAlpha;
        gameplayUiCanvasGroup.interactable = capturedUiInteractable;
        gameplayUiCanvasGroup.blocksRaycasts = capturedUiBlocksRaycasts;
    }

    private void EnsureGameplayUiCanvasGroup()
    {
        if (gameplayUiCanvasGroup != null)
        {
            return;
        }

        // Prefab 안에서는 Scene의 Canvas를 직접 참조할 수 없으므로, 오버라이드가 비어 있을 때만 인게임 UI 루트에서 찾는다.
        InGameUIController inGameUi = FindFirstObjectByType<InGameUIController>();
        if (inGameUi != null)
        {
            gameplayUiCanvasGroup = inGameUi.GetComponent<CanvasGroup>();
        }

        if (gameplayUiCanvasGroup == null)
        {
            Debug.LogWarning(
                $"{nameof(PerfectWinLetterboxPresentation)} cannot find the gameplay UI CanvasGroup. " +
                "Letterbox bars will show without hiding the UI.",
                this);
        }
    }

    private void EnsureLetterboxCanvas()
    {
        if (letterboxCanvas != null)
        {
            return;
        }

        // Scene/Prefab에 UI 오브젝트를 추가하지 않고 런타임에 만든다. Overlay Canvas는 카메라 위에 그려지므로
        // 별도 Scaler 없이 화면 픽셀 좌표로 바 위치를 직접 계산한다.
        var canvasObject = new GameObject(CanvasObjectName, typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.layer = LayerMask.NameToLayer("UI");

        letterboxCanvas = canvasObject.GetComponent<Canvas>();
        letterboxCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        letterboxCanvas.sortingOrder = canvasSortingOrder;

        topBar = CreateBar("TopBar", canvasObject.transform);
        bottomBar = CreateBar("BottomBar", canvasObject.transform);
        canvasObject.SetActive(false);
    }

    private RectTransform CreateBar(string barName, Transform parent)
    {
        var barObject = new GameObject(barName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barObject.transform.SetParent(parent, false);
        barObject.layer = LayerMask.NameToLayer("UI");

        Image image = barObject.GetComponent<Image>();
        image.color = barColor;
        image.raycastTarget = false;

        RectTransform rectTransform = barObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = Vector2.zero;
        return rectTransform;
    }

    private void ApplyBarLayout()
    {
        if (topBar == null || bottomBar == null)
        {
            return;
        }

        // 16:9 뷰포트 픽셀 영역 기준으로 두 바를 붙여, 좌우 필러박스나 상하 여백과 어긋나지 않게 한다.
        Rect viewport = FixedAspectRatioController.GetCurrentPixelRect();
        float barHeight = viewport.height * barHeightRatio * Mathf.Clamp01(barProgress);
        var size = new Vector2(viewport.width, barHeight);

        topBar.anchoredPosition = new Vector2(viewport.xMin, viewport.yMax - barHeight);
        topBar.sizeDelta = size;
        bottomBar.anchoredPosition = new Vector2(viewport.xMin, viewport.yMin);
        bottomBar.sizeDelta = size;
    }
}
