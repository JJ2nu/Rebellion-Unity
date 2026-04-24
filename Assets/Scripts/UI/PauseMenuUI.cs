using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Rebellion.UI
{
    /// <summary>
    /// Handles the pause menu panel: resume, restart, and main menu navigation.
    /// Listens to GameManager state changes to show/hide itself.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject pausePanel;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        private void Start()
        {
            resumeButton?.onClick.AddListener(OnResumeClicked);
            restartButton?.onClick.AddListener(OnRestartClicked);
            mainMenuButton?.onClick.AddListener(OnMainMenuClicked);

            if (Core.GameManager.Instance != null)
                Core.GameManager.Instance.OnGameStateChanged += OnGameStateChanged;

            pausePanel?.SetActive(false);
        }

        private void OnDestroy()
        {
            resumeButton?.onClick.RemoveAllListeners();
            restartButton?.onClick.RemoveAllListeners();
            mainMenuButton?.onClick.RemoveAllListeners();

            if (Core.GameManager.Instance != null)
                Core.GameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(Core.GameState newState)
        {
            pausePanel?.SetActive(newState == Core.GameState.Paused);
        }

        private void OnResumeClicked()
        {
            Core.GameManager.Instance?.ResumeGame();
        }

        private void OnRestartClicked()
        {
            Core.GameManager.Instance?.StartGame();
            Core.SceneLoader.Instance?.ReloadCurrentScene();
        }

        private void OnMainMenuClicked()
        {
            Core.SceneLoader.Instance?.LoadMainMenu();
        }
    }
}
