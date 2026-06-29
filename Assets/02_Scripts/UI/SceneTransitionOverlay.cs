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

    public static SceneTransitionOverlay Instance
    {
        get
        {
            return GetOrCreateInstance();
        }
    }
    public bool IsFullyOpaque => canvasGroup != null && canvasGroup.alpha >= 0.99f;

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
        LoadScene(sceneName, null);
    }

    public void LoadScene(string sceneName, Action beforeLoad)
    {
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
        yield return FadeOut();
        beforeLoad?.Invoke();
        yield return LoadSceneOnly(sceneName);
        yield return WaitForSceneSettled();
        yield return FadeIn();
    }

    public IEnumerator LoadSceneOnly(string sceneName)
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

        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f)
        {
            yield return null;
        }

        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            yield return null;
        }
    }

    public IEnumerator RunCovered(Action coveredAction)
    {
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
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
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

    private static float SmoothStep(float value)
    {
        return value * value * (3f - 2f * value);
    }
}
