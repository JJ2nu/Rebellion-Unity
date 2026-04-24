using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Projectile behaviour: travels in a direction, deals damage on contact,
    /// and returns itself to the object pool after a set lifetime.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private float speed = 15f;
        [SerializeField] private int damage = 15;
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private string poolTag = "Projectile";
        [SerializeField] private LayerMask hitLayers;

        [Header("Effects")]
        [SerializeField] private GameObject hitEffectPrefab;

        private Rigidbody2D rb;
        private float lifetimeTimer;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            rb.linearVelocity = transform.right * speed;
            lifetimeTimer = lifetime;
        }

        private void Update()
        {
            lifetimeTimer -= Time.deltaTime;
            if (lifetimeTimer <= 0f)
                Despawn();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (((1 << other.gameObject.layer) & hitLayers) == 0) return;

            HealthSystem health = other.GetComponent<HealthSystem>();
            health?.TakeDamage(damage);

            if (hitEffectPrefab != null)
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);

            Despawn();
        }

        private void Despawn()
        {
            Core.ObjectPool.Instance?.Despawn(poolTag, gameObject);
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
