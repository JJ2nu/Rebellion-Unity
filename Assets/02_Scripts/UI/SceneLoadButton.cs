using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
