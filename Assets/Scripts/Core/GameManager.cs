using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion.Core
{
    /// <summary>
    /// Manages global game state and coordinates between major systems.
    /// Acts as the central hub for game flow (boot, main menu, gameplay, pause, game over).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        [SerializeField] private GameState currentState = GameState.Boot;

        public GameState CurrentState => currentState;

        public event System.Action<GameState> OnGameStateChanged;

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

        private void Start()
        {
            ChangeState(GameState.MainMenu);
        }

        public void ChangeState(GameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            OnGameStateChanged?.Invoke(currentState);

            switch (currentState)
            {
                case GameState.Boot:
                    HandleBoot();
                    break;
                case GameState.MainMenu:
                    HandleMainMenu();
                    break;
                case GameState.Playing:
                    HandlePlaying();
                    break;
                case GameState.Paused:
                    HandlePaused();
                    break;
                case GameState.GameOver:
                    HandleGameOver();
                    break;
            }
        }

        private void HandleBoot() { }

        private void HandleMainMenu()
        {
            Time.timeScale = 1f;
        }

        private void HandlePlaying()
        {
            Time.timeScale = 1f;
        }

        private void HandlePaused()
        {
            Time.timeScale = 0f;
        }

        private void HandleGameOver()
        {
            Time.timeScale = 0f;
        }

        public void StartGame()
        {
            ChangeState(GameState.Playing);
        }

        public void PauseGame()
        {
            if (currentState == GameState.Playing)
                ChangeState(GameState.Paused);
        }

        public void ResumeGame()
        {
            if (currentState == GameState.Paused)
                ChangeState(GameState.Playing);
        }

        public void TriggerGameOver()
        {
            ChangeState(GameState.GameOver);
        }
    }

    public enum GameState
    {
        Boot,
        MainMenu,
        Playing,
        Paused,
        GameOver
    }
}
