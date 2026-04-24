// New script providing a 3-D physics bullet for the tactical Gunman.
// Adapted from Client/Contents/GameObjects/Map/Weapons/Bullet/Bullet in
// 4Q-Rebellion (C++), where a bullet travels in a straight line and deals
// damage on contact with a Character.
//
// Used by: Gunman.cs (Init is called at spawn time).

using UnityEngine;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// A 3-D projectile for the Gunman.  Travels in a straight line and
    /// damages the first <see cref="Character"/> it hits.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TacticalBullet : MonoBehaviour
    {
        [Header("Bullet Settings")]
        [SerializeField] private float speed    = 20f;
        [SerializeField] private int   damage   = 1;
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private GameObject hitEffectPrefab;

        private Rigidbody  _rb;
        private Vector3    _direction;
        private float      _lifetimeTimer;
        private GameObject _owner;

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = false;
            _rb.useGravity  = false;
        }

        private void Update()
        {
            _lifetimeTimer -= Time.deltaTime;
            if (_lifetimeTimer <= 0f)
                Destroy(gameObject);
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Configure and launch the bullet.
        /// Called by <see cref="Gunman"/> immediately after Instantiate.
        /// Mirrors the bullet setup in Gunman::Update (direction / scale).
        /// </summary>
        /// <param name="direction">Normalised travel direction in world space.</param>
        /// <param name="bulletSpeed">World-units per second.</param>
        /// <param name="bulletDamage">Hit points removed from target.</param>
        /// <param name="owner">The firing character's GameObject (ignored in collision).</param>
        public void Init(Vector3 direction, float bulletSpeed, int bulletDamage,
                         GameObject owner)
        {
            _direction     = direction.normalized;
            speed          = bulletSpeed;
            damage         = bulletDamage;
            _owner         = owner;
            _lifetimeTimer = lifetime;

            _rb.linearVelocity = _direction * speed;
        }

        // ── Collision ─────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            // Ignore the firing character itself.
            if (_owner != null && other.gameObject == _owner) return;

            // Damage any character hit.
            var character = other.GetComponent<Character>();
            if (character != null && !character.IsDead)
            {
                character.TakeDamageFromBullet(damage);
            }

            if (hitEffectPrefab != null)
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }
    }
}
