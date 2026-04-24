using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Rebellion.UI
{
    /// <summary>
    /// Manages all in-game HUD elements: health bar, score display, and boss health bar.
    /// Subscribes to gameplay events and updates UI accordingly.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [Header("Health Bar")]
        [SerializeField] private Slider healthBar;
        [SerializeField] private TextMeshProUGUI healthText;

        [Header("Score")]
        [SerializeField] private TextMeshProUGUI scoreText;

        [Header("Boss Health Bar")]
        [SerializeField] private GameObject bossHealthPanel;
        [SerializeField] private Slider bossHealthBar;
        [SerializeField] private TextMeshProUGUI bossNameText;

        private Gameplay.HealthSystem playerHealth;
        private int score;

        private void Start()
        {
            FindPlayerHealth();
            UpdateScore(0);
            ShowBossHealth(false);
        }

        private void FindPlayerHealth()
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null) return;

            playerHealth = playerObj.GetComponent<Gameplay.HealthSystem>();
            if (playerHealth == null) return;

            playerHealth.OnHealthChanged += UpdateHealthBar;
            UpdateHealthBar(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
                playerHealth.OnHealthChanged -= UpdateHealthBar;
        }

        private void UpdateHealthBar(int current, int max)
        {
            if (healthBar != null)
                healthBar.value = (float)current / max;

            if (healthText != null)
                healthText.text = $"{current} / {max}";
        }

        public void UpdateScore(int newScore)
        {
            score = newScore;
            if (scoreText != null)
                scoreText.text = $"Score: {score:N0}";
        }

        public void AddScore(int amount)
        {
            UpdateScore(score + amount);
        }

        public void ShowBossHealth(bool show, string bossName = "", Gameplay.HealthSystem bossHealth = null)
        {
            if (bossHealthPanel != null)
                bossHealthPanel.SetActive(show);

            if (!show) return;

            if (bossNameText != null)
                bossNameText.text = bossName;

            if (bossHealth != null)
            {
                bossHealth.OnHealthChanged += UpdateBossHealthBar;
                UpdateBossHealthBar(bossHealth.CurrentHealth, bossHealth.MaxHealth);
            }
        }

        private void UpdateBossHealthBar(int current, int max)
        {
            if (bossHealthBar != null)
                bossHealthBar.value = (float)current / max;
        }
    }
}
