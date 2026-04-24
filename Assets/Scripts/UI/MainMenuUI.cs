using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Rebellion.UI
{
    /// <summary>
    /// Controls the main menu UI: start, options, and quit buttons.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject optionsPanel;

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button optionsBackButton;

        [Header("Volume Sliders")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        private void Start()
        {
            startButton?.onClick.AddListener(OnStartClicked);
            optionsButton?.onClick.AddListener(OnOptionsClicked);
            quitButton?.onClick.AddListener(OnQuitClicked);
            optionsBackButton?.onClick.AddListener(OnOptionsBack);

            bgmSlider?.onValueChanged.AddListener(OnBGMVolumeChanged);
            sfxSlider?.onValueChanged.AddListener(OnSFXVolumeChanged);

            ShowPanel(mainPanel);
        }

        private void OnDestroy()
        {
            startButton?.onClick.RemoveAllListeners();
            optionsButton?.onClick.RemoveAllListeners();
            quitButton?.onClick.RemoveAllListeners();
            optionsBackButton?.onClick.RemoveAllListeners();
            bgmSlider?.onValueChanged.RemoveAllListeners();
            sfxSlider?.onValueChanged.RemoveAllListeners();
        }

        private void OnStartClicked()
        {
            Core.SceneLoader.Instance?.LoadGameplay();
        }

        private void OnOptionsClicked()
        {
            ShowPanel(optionsPanel);
        }

        private void OnOptionsBack()
        {
            ShowPanel(mainPanel);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnBGMVolumeChanged(float value)
        {
            Core.AudioManager.Instance.BGMVolume = value;
        }

        private void OnSFXVolumeChanged(float value)
        {
            Core.AudioManager.Instance.SFXVolume = value;
        }

        private void ShowPanel(GameObject panel)
        {
            mainPanel?.SetActive(mainPanel == panel);
            optionsPanel?.SetActive(optionsPanel == panel);
        }
    }
}
