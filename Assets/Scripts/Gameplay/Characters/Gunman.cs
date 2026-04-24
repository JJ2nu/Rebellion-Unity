// Adapted from Client/Contents/GameObjects/Map/Characters/Gunman/Gunman.cpp
// and Gunman.h of 4Q-Rebellion (C++).
//
// Key adaptations:
//  - range = 5 and "skip obstacles" rule (carried over via base-class
//    FindTargetInRange which checks CharacterType.Gunman).
//  - Bullet is spawned at the muzzle's world position when the fire
//    animation reaches normalised time 0.65 — mirrors the C++ timing check.
//  - The Unity Projectile component (Projectile.cs from PR#1) is reused
//    for the bullet; the Gunman just instantiates it from a prefab.
//  - Animator variables: "arm" (bool) to show gun, "fire" (bool) to shoot.

using System.Collections;
using UnityEngine;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Ranged gunman character.  Range = 5 cells, shoots through obstacles.
    /// Adapted from 4Q-Rebellion's Gunman class.
    /// </summary>
    public class Gunman : Character
    {
        // ── Animator hashes ───────────────────────────────────────────────
        private static readonly int AnimArmParam  = Animator.StringToHash("arm");
        private static readonly int AnimFireParam = Animator.StringToHash("fire");

        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Gunman — Projectile")]
        [Tooltip("Prefab for the bullet / projectile to spawn. " +
                 "Should have a TacticalBullet component and a Rigidbody.")]
        [SerializeField] private TacticalBullet bulletPrefab;

        [Tooltip("Transform that marks where the bullet spawns (gun muzzle).")]
        [SerializeField] private Transform muzzlePoint;

        [Tooltip("Speed applied to the spawned bullet.")]
        [SerializeField] private float bulletSpeed = 20f;

        [Tooltip("Normalised animation time at which the bullet is fired. " +
                 "Matches 0.65 from the original C++ Gunman::Update.")]
        [SerializeField] [Range(0f, 1f)] private float fireAnimTime = 0.65f;

        [Tooltip("Damage dealt to the target.")]
        [SerializeField] private int bulletDamage = 1;

        // ── Runtime ───────────────────────────────────────────────────────
        private Coroutine _fireRoutine;
        private bool _bulletFired;

        // ── Unity lifecycle ───────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            Type  = CharacterType.Gunman;
            range = 5;
        }

        // ── TriggerAction override ─────────────────────────────────────────

        /// <summary>
        /// Begin the fire sequence.
        /// Mirrors Gunman::Update — the bullet is spawned at fireAnimTime.
        /// </summary>
        public override void TriggerAction()
        {
            base.TriggerAction();
            _bulletFired = false;

            if (IsTargetInRange && _fireRoutine == null)
                _fireRoutine = StartCoroutine(FireRoutine());
        }

        // ── Fire coroutine ────────────────────────────────────────────────

        /// <summary>
        /// Waits for the fire animation to reach <see cref="fireAnimTime"/>
        /// then spawns a bullet from the muzzle.
        /// Mirrors the C++ timing: 0.65 of gunFireAnimation.
        /// </summary>
        private IEnumerator FireRoutine()
        {
            // Wait for the fire variable to be set by the animator state machine.
            yield return new WaitUntil(() =>
                _animator != null && _animator.GetBool(AnimFireParam));

            // Wait for the animation to reach the fire point.
            yield return new WaitUntil(() => IsAnimNormalisedTimeAtLeast(fireAnimTime));

            if (!_bulletFired)
            {
                SpawnBullet();
                _bulletFired = true;
            }

            _fireRoutine = null;
        }

        private void SpawnBullet()
        {
            if (bulletPrefab == null) return;

            Transform spawnPoint = muzzlePoint != null ? muzzlePoint : transform;

            TacticalBullet bullet = Instantiate(bulletPrefab,
                                            spawnPoint.position,
                                            spawnPoint.rotation);

            // Direction is negated because the character models face -Z in local space
            // (matching the C++ original: bullet->SetDirection(-GetGlobalFront())).
            // At rotation 180° (North-facing), transform.forward = world (0,0,-1),
            // so -transform.forward = world (0,0,1) = North — the correct fire direction.
            bullet.Init(-transform.forward, bulletSpeed, bulletDamage, gameObject);
        }

        // ── Helper ────────────────────────────────────────────────────────

        private bool IsAnimNormalisedTimeAtLeast(float threshold)
        {
            if (_animator == null) return false;
            var info = _animator.GetCurrentAnimatorStateInfo(0);
            return info.normalizedTime % 1f >= threshold;
        }
    }
}
