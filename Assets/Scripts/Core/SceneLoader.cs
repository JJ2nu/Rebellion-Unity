using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rebellion.Core
{
    /// <summary>
    /// Handles scene loading and transitions with optional loading screen support.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Scenes")]
        [SerializeField] private string bootScene = "Boot";
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string gameplayScene = "Gameplay";

        public event System.Action OnSceneLoadStarted;
        public event System.Action OnSceneLoadCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoadMainMenu()
        {
            LoadScene(mainMenuScene);
        }

        public void LoadGameplay()
        {
            LoadScene(gameplayScene);
        }

        public void LoadScene(string sceneName)
        {
            StartCoroutine(LoadSceneAsync(sceneName));
        }

        private IEnumerator LoadSceneAsync(string sceneName)
        {
            OnSceneLoadStarted?.Invoke();

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;

            while (!operation.isDone)
            {
                if (operation.progress >= 0.9f)
                    operation.allowSceneActivation = true;

                yield return null;
            }

            OnSceneLoadCompleted?.Invoke();
        }

        public void ReloadCurrentScene()
        {
            LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
