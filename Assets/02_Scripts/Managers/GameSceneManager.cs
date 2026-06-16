using UnityEngine;

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }


    #region 씬 전환
    public void StartCampaign()
    {
        // Campaign 버튼은 Scene을 직접 열지 않고 지속되는 캠페인 상태를 먼저 생성한다.
        GameFlowManager.StartNewCampaign();
    }

    public void LoadScene(string sceneName)
    {
        // todo : 다음 씬에 필요한 에셋 로드

        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
    #endregion


    #region 현재 씬 다시 로드
    public void RestartCurrentScene()
    {
        // 현재 활성화된 씬 이름 가져오기
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // todo : 저장 된 게임 데이터 불러오기

        // 현재 씬 다시 로드
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentSceneName);

    }
    #endregion


    #region 게임종료
    public void QuitGame()
    {
        Debug.Log("게임 종료");

        // 빌드된 게임에서 종료
        Application.Quit();

#if UNITY_EDITOR
        // 유니티 에디터에서 플레이 모드 종료
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    #endregion
}
