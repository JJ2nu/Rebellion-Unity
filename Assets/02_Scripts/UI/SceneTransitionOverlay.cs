using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene 전환과 무거운 런타임 로드 구간을 검은 UI로 가려 미완성 렌더링 노출을 막는다.
/// </summary>
public sealed class SceneTransitionOverlay : MonoBehaviour
{
    private const int SortingOrder = 32767;
    private const int DefaultSettleFrameCount = 2;

    private static SceneTransitionOverlay instance;

    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private Color overlayColor = Color.black;

    private CanvasGroup canvasGroup;
    private Coroutine transitionRoutine;
    private LoadingScreenView loadingScreenView;

    public static SceneTransitionOverlay Instance
    {
        get
        {
            return GetOrCreateInstance();
        }
    }
    public bool IsFullyOpaque => canvasGroup != null && canvasGroup.alpha >= 0.99f;
    // 로딩 View가 화면에 남아 있는지 노출한다. Binder 늦은 등록 재개처럼 호출자가 로딩 연출을 이어갈지 판단할 때 사용한다.
    public bool IsLoadingVisible => loadingScreenView != null && loadingScreenView.IsVisible;

    public static SceneTransitionOverlay GetOrCreateInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject overlayObject = new GameObject(nameof(SceneTransitionOverlay));
        instance = overlayObject.AddComponent<SceneTransitionOverlay>();
        return instance;
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
        BuildOverlay();
        SetAlpha(0f);
    }

    public void LoadScene(string sceneName)
    {
        // 일반 전환은 범용 로딩 화면을 자동 표시하지 않으며, 남은 표시만 정리한다.
        HideLoading();
        LoadScene(sceneName, null);
    }

    public void LoadScene(string sceneName, Action beforeLoad)
    {
        HideLoading();
        StartTransition(LoadSceneWithFade(sceneName, beforeLoad));
    }

    public void RestartActiveScene()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    public Coroutine StartTransition(IEnumerator routine)
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(RunTransition(routine));
        return transitionRoutine;
    }

    public IEnumerator LoadSceneWithFade(string sceneName)
    {
        yield return LoadSceneWithFade(sceneName, null);
    }

    public IEnumerator LoadSceneWithFade(string sceneName, Action beforeLoad)
    {
        HideLoading();
        yield return FadeOut();
        beforeLoad?.Invoke();
        yield return LoadSceneOnly(sceneName);
        yield return WaitForSceneSettled();
        yield return FadeIn();
    }

    public IEnumerator LoadSceneOnly(string sceneName)
    {
        yield return LoadSceneOnly(sceneName, null);
    }

    /// <summary>
    /// 기존 Scene 로드 호출을 유지하면서, 필요한 흐름만 활성화 전 정규화한 비동기 진행률을 받을 수 있게 한다.
    /// </summary>
    public IEnumerator LoadSceneOnly(string sceneName, Action<float> onProgress)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneTransitionOverlay] Scene name is empty.", this);
            yield break;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogWarning($"[SceneTransitionOverlay] Could not start async scene load: {sceneName}", this);
            yield break;
        }

        onProgress?.Invoke(0f);
        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f)
        {
            // Unity는 Scene 활성화 전 progress를 0.9에서 멈추므로 이 구간만 0~1로 정규화한다.
            onProgress?.Invoke(Mathf.Clamp01(operation.progress / 0.9f));
            yield return null;
        }

        // 0.9 도달은 Scene 데이터 준비 완료일 뿐, 활성화와 Stage 준비 완료는 이후 흐름이 별도로 판단한다.
        onProgress?.Invoke(1f);
        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            yield return null;
        }

        onProgress?.Invoke(1f);
    }

    public IEnumerator RunCovered(Action coveredAction)
    {
        HideLoading();
        yield return FadeOut();
        coveredAction?.Invoke();
        yield return WaitForSceneSettled();
        yield return FadeIn();
    }

    public IEnumerator FadeOut()
    {
        BuildOverlay();
        gameObject.SetActive(true);
        yield return FadeTo(1f);
    }

    public IEnumerator FadeIn()
    {
        BuildOverlay();
        yield return FadeTo(0f);
        HideLoading();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    /// <summary>
    /// 호출자가 필요한 전환에서만 로딩 화면을 표시한다. Prefab이 누락돼도 기존 검정 Fade 흐름은 계속된다.
    /// </summary>
    public bool ShowLoading()
    {
        BuildOverlay();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        if (loadingScreenView == null)
        {
            Debug.LogWarning(
                "[SceneTransitionOverlay] LoadingScreenView is not registered. Continuing with the fade overlay.",
                this);
            return false;
        }

        loadingScreenView.Show();
        return true;
    }

    public void SetLoadingProgress(float normalizedProgress)
    {
        loadingScreenView?.SetProgress(normalizedProgress);
    }

    public void HideLoading()
    {
        loadingScreenView?.Hide();
    }

    /// <summary>
    /// Prefab Inspector 값으로 조정하는 FadeOut 뒤의 검정 홀드다. Time.timeScale 영향 없이 연출 순서만 보장한다.
    /// </summary>
    public IEnumerator WaitForLoadingFadeOutHold()
    {
        yield return WaitForRealtimeSeconds(
            loadingScreenView != null ? loadingScreenView.FadeOutHoldSeconds : 0f);
    }

    /// <summary>
    /// 100%가 한 프레임 이상 표시된 뒤 다음 콘텐츠를 시작하기 전에 사용하는 완료 홀드다.
    /// </summary>
    public IEnumerator WaitForLoadingCompletedHold()
    {
        yield return WaitForRealtimeSeconds(
            loadingScreenView != null ? loadingScreenView.CompletedHoldSeconds : 0f);
    }

    /// <summary>
    /// Title의 Prefab은 Awake에서 자신을 등록한다. 기존 영속 View가 있으면 새 Scene 인스턴스는 스스로 파괴한다.
    /// </summary>
    public bool RegisterLoadingScreen(LoadingScreenView view)
    {
        if (view == null)
        {
            return false;
        }

        if (loadingScreenView != null && loadingScreenView != view)
        {
            return false;
        }

        loadingScreenView = view;
        view.transform.SetParent(transform, false);
        view.transform.SetAsLastSibling();
        return true;
    }

    public void UnregisterLoadingScreen(LoadingScreenView view)
    {
        if (loadingScreenView == view)
        {
            loadingScreenView = null;
        }
    }

    public IEnumerator WaitForSceneSettled(int frameCount = DefaultSettleFrameCount)
    {
        int frames = Mathf.Max(1, frameCount);
        for (int index = 0; index < frames; index++)
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();
    }

    private IEnumerator RunTransition(IEnumerator routine)
    {
        yield return routine;
        transitionRoutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        if (Mathf.Approximately(fadeDuration, 0f))
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, SmoothStep(t)));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = alpha;
        canvasGroup.blocksRaycasts = alpha > 0.01f;
        canvasGroup.interactable = alpha > 0.01f;
    }

    private void BuildOverlay()
    {
        if (canvasGroup != null)
        {
            return;
        }

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        GameObject panelObject = new GameObject("FadePanel");
        panelObject.transform.SetParent(transform, false);

        RectTransform panelTransform = panelObject.AddComponent<RectTransform>();
        panelTransform.anchorMin = Vector2.zero;
        panelTransform.anchorMax = Vector2.one;
        panelTransform.offsetMin = Vector2.zero;
        panelTransform.offsetMax = Vector2.zero;

        Image image = panelObject.AddComponent<Image>();
        image.color = overlayColor;
        image.raycastTarget = true;

    }

    private static IEnumerator WaitForRealtimeSeconds(float seconds)
    {
        if (seconds > 0f)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }
    }

    private static float SmoothStep(float value)
    {
        return value * value * (3f - 2f * value);
    }
}
