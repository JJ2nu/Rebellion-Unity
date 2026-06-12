using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Inspector의 Scene 이름으로 단순 메뉴 버튼 전환을 연결한다.
/// 캠페인 상태가 필요한 Campaign 버튼에는 사용하지 않는다.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class SceneLoadButton : MonoBehaviour
{
    [SerializeField] private string sceneName;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        button ??= GetComponent<Button>();
        button.onClick.AddListener(LoadScene);
    }

    private void OnDisable()
    {
        button?.onClick.RemoveListener(LoadScene);
    }

    private void LoadScene()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("SceneLoadButton scene name is empty.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
