using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Handles attack logic for the player: melee combo and ranged attacks.
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
        [Header("Melee Attack")]
        [SerializeField] private Transform meleeHitPoint;
        [SerializeField] private float meleeRange = 1.2f;
        [SerializeField] private int meleeDamage = 20;
        [SerializeField] private float meleeAttackRate = 2f;
        [SerializeField] private LayerMask enemyLayer;

        [Header("Ranged Attack")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float rangedAttackRate = 1f;

        [Header("Audio")]
        [SerializeField] private AudioClip meleeSwingClip;
        [SerializeField] private AudioClip rangedFireClip;

        private Animator animator;
        private float nextMeleeTime;
        private float nextRangedTime;

        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimRangedAttack = Animator.StringToHash("RangedAttack");

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        public void TryMeleeAttack()
        {
            if (Time.time < nextMeleeTime) return;

            nextMeleeTime = Time.time + 1f / meleeAttackRate;
            PerformMeleeAttack();
        }

        public void TryRangedAttack()
        {
            if (Time.time < nextRangedTime) return;

            nextRangedTime = Time.time + 1f / rangedAttackRate;
            PerformRangedAttack();
        }

        private void PerformMeleeAttack()
        {
            animator?.SetTrigger(AnimAttack);

            if (meleeHitPoint == null) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(meleeHitPoint.position, meleeRange, enemyLayer);
            foreach (Collider2D hit in hits)
            {
                HealthSystem health = hit.GetComponent<HealthSystem>();
                health?.TakeDamage(meleeDamage);
            }

            Core.AudioManager.Instance?.PlaySFX(meleeSwingClip);
        }

        private void PerformRangedAttack()
        {
            if (projectilePrefab == null || firePoint == null) return;

            animator?.SetTrigger(AnimRangedAttack);

            GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

            Core.AudioManager.Instance?.PlaySFX(rangedFireClip);
        }

        private void OnDrawGizmosSelected()
        {
            if (meleeHitPoint == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(meleeHitPoint.position, meleeRange);
        }
    }
}
