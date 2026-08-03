using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메뉴 버튼의 씬 전환을 코드에서 연결한다.
/// Campaign은 지속되는 GameSceneManager를 통해 캠페인 상태를 만든 뒤 이동한다.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class SceneLoadButton : MonoBehaviour
{
    private enum LoadMode
    {
        Scene,
        Campaign
    }

    [SerializeField] private LoadMode loadMode;
    [SerializeField] private string sceneName;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        button ??= GetComponent<Button>();

#if UNITY_EDITOR || REBELLION_DEMO_BUILD
        // 이번 시연에서 비활성화한 Challenge는 Bootstrap 설정만으로 Title 버튼 입력도 함께 막는다.
        if (!DemoSessionController.IsSceneSelectionAllowed(sceneName))
        {
            button.interactable = false;
        }
#endif

        button.onClick.AddListener(Load);
    }

    private void OnDisable()
    {
        button?.onClick.RemoveListener(Load);
    }

    private void Load()
    {
        if (GameSceneManager.Instance == null)
        {
            Debug.LogWarning("SceneLoadButton could not find GameSceneManager.Instance.", this);
            return;
        }

        if (loadMode == LoadMode.Campaign)
        {
            GameSceneManager.Instance.StartCampaign();
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("SceneLoadButton scene name is empty.", this);
            return;
        }

#if UNITY_EDITOR || REBELLION_DEMO_BUILD
        if (!DemoSessionController.IsSceneSelectionAllowed(sceneName))
        {
            return;
        }
#endif

        GameSceneManager.Instance.LoadScene(sceneName);
    }
}
