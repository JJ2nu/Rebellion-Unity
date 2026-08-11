using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메뉴 버튼의 씬 전환과 게임 종료를 코드에서 연결한다.
/// Campaign은 지속되는 GameSceneManager를 통해 캠페인 상태를 만든 뒤 이동한다.
/// Quit은 Inspector persistent call 대신 클릭 시점에 살아있는 GameSceneManager 싱글톤으로 종료한다.
/// Title 재진입 시 씬 로컬 GameSceneManager가 중복 파괴되어 persistent call 참조가 끊기는 문제를 피하기 위함이다.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class SceneLoadButton : MonoBehaviour
{
    private enum LoadMode
    {
        Scene,
        Campaign,
        Quit
    }

    [SerializeField] private LoadMode loadMode;
    [SerializeField] private string sceneName;

    // 현재 빌드에 포함하지 않는 콘텐츠(예: Challenge 스테이지 미포함) 버튼을 빌드 종류와 무관하게 막는다.
    // Demo 빌드의 DemoSessionController 차단과 달리 Release 빌드에서도 동작한다.
    [Tooltip("현재 빌드에서 이 버튼의 진입을 차단한다. 버튼은 비활성 표시되고 클릭해도 동작하지 않는다.")]
    [SerializeField] private bool entryBlocked;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        button ??= GetComponent<Button>();

        // 진입 차단 버튼은 DisabledSprite 상태로 보여 사용자가 누르기 전에 막힌 것을 알 수 있게 한다.
        if (entryBlocked)
        {
            button.interactable = false;
        }

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
        // interactable이 다른 경로로 다시 켜져도 진입 차단 버튼은 클릭 동작을 거부한다.
        if (entryBlocked)
        {
            return;
        }

        if (GameSceneManager.Instance == null)
        {
            Debug.LogWarning("SceneLoadButton could not find GameSceneManager.Instance.", this);
            return;
        }

        // Quit은 씬 전환 없이 종료 경로(PlaytestLogger 기록 포함)만 실행한다.
        if (loadMode == LoadMode.Quit)
        {
            GameSceneManager.Instance.QuitGame();
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
