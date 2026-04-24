using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Manages the health, damage, and death for any entity (player or enemy).
    /// Broadcasts events for UI and game logic to react to.
    /// </summary>
    public class HealthSystem : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private bool isInvincibleOnHit = true;
        [SerializeField] private float invincibilityDuration = 1f;

        private int currentHealth;
        private bool isInvincible;
        private bool isDead;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public bool IsDead => isDead;
        public float HealthPercent => (float)currentHealth / maxHealth;

        public event System.Action<int, int> OnHealthChanged;  // current, max
        public event System.Action<int> OnDamageTaken;         // damage amount
        public event System.Action OnDeath;
        public event System.Action OnRevive;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void TakeDamage(int damage)
        {
            if (isDead || isInvincible || damage <= 0) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            OnDamageTaken?.Invoke(damage);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0)
                Die();
            else if (isInvincibleOnHit)
                StartCoroutine(InvincibilityCoroutine());
        }

        public void Heal(int amount)
        {
            if (isDead || amount <= 0) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void SetMaxHealth(int newMax, bool healToFull = false)
        {
            maxHealth = Mathf.Max(1, newMax);

            if (healToFull)
                currentHealth = maxHealth;
            else
                currentHealth = Mathf.Min(currentHealth, maxHealth);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Die()
        {
            isDead = true;
            OnDeath?.Invoke();
        }

        public void Revive(int healthAmount = -1)
        {
            isDead = false;
            currentHealth = healthAmount < 0 ? maxHealth : Mathf.Clamp(healthAmount, 1, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnRevive?.Invoke();
        }

        private IEnumerator InvincibilityCoroutine()
        {
            isInvincible = true;
            yield return new WaitForSeconds(invincibilityDuration);
            isInvincible = false;
        }
    }
}
