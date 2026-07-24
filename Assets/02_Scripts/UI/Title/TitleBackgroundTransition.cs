using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title Scene의 기존 배경 위에 엔딩 배경을 겹쳐 표시하고, 현재 실행에서 첫 엔딩 복귀 때만 전환한다.
/// 배경 Sprite와 전환 시간은 Scene의 직렬화 값으로 교체할 수 있다.
/// </summary>
public sealed class TitleBackgroundTransition : MonoBehaviour
{
    [Header("Backgrounds")]
    [SerializeField] private Image beforeBackground;
    [SerializeField] private Image afterBackground;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float transitionDuration = 3.5f;

    public float TransitionDuration => transitionDuration;

    private void Awake()
    {
        if (GameFlowManager.HasCompletedEndingTitleTransition)
        {
            ShowAfterBackgroundImmediate();
        }
        else
        {
            ResetToBeforeBackground();
        }
    }

    private void OnEnable()
    {
        RegisterWithFlowManager();
    }

    private void Start()
    {
        // Title Scene과 DontDestroyOnLoad 매니저의 활성화 순서가 달라도 한 번 더 연결한다.
        RegisterWithFlowManager();
    }

    private void OnDisable()
    {
        if (GameFlowManager.HasInstance)
        {
            GameFlowManager.Instance.UnregisterTitleBackgroundTransition(this);
        }
    }

    public IEnumerator PlayAndWait()
    {
        if (beforeBackground == null || afterBackground == null)
        {
            Debug.LogWarning("[TitleBackgroundTransition] Background Image references are not assigned.", this);
            yield break;
        }

        beforeBackground.gameObject.SetActive(true);
        afterBackground.gameObject.SetActive(true);
        SetImageAlpha(beforeBackground, 1f);
        SetImageAlpha(afterBackground, 0f);

        if (transitionDuration <= 0f)
        {
            SetImageAlpha(afterBackground, 1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / transitionDuration);
            SetImageAlpha(afterBackground, Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }

        SetImageAlpha(afterBackground, 1f);
    }

    public void ShowAfterBackgroundImmediate()
    {
        if (beforeBackground != null)
        {
            beforeBackground.gameObject.SetActive(true);
            beforeBackground.raycastTarget = false;
            SetImageAlpha(beforeBackground, 1f);
        }

        if (afterBackground != null)
        {
            afterBackground.gameObject.SetActive(true);
            afterBackground.raycastTarget = false;
            SetImageAlpha(afterBackground, 1f);
        }
    }

    private void ResetToBeforeBackground()
    {
        if (beforeBackground != null)
        {
            beforeBackground.gameObject.SetActive(true);
            beforeBackground.raycastTarget = false;
            SetImageAlpha(beforeBackground, 1f);
        }

        if (afterBackground != null)
        {
            afterBackground.raycastTarget = false;
            SetImageAlpha(afterBackground, 0f);
            afterBackground.gameObject.SetActive(false);
        }
    }

    private void RegisterWithFlowManager()
    {
        if (GameFlowManager.HasInstance)
        {
            GameFlowManager.Instance.RegisterTitleBackgroundTransition(this);
        }
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
