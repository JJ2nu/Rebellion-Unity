// Adapted from Client/Contents/GameObjects/Map/Characters/Slasher/Slasher.cpp
// and Slasher.h of 4Q-Rebellion (C++).
//
// Key adaptations:
//  - range = 2  (same as C++ Slasher).
//  - The "rush" mechanic (TakeOverTargetCell) is preserved: when the
//    target is exactly 1 cell away the Slasher moves into the target cell
//    after the slash.  When the target is 2 cells away the normal slash
//    animation is used and the Slasher stays in place.
//  - Knife collider activation window:
//      close-range (distance 1): 0.208 – 0.271 of action1 animation.
//      far-range  (distance 2): 0.313 – 0.375 of action2 animation.
//  - Collision window is managed via coroutine (same timing approach as Brawler).
//  - "arm" / "fire" Animator bool variables translated to Unity hash params.

using System.Collections;
using UnityEngine;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Melee slasher character.  Range = 2 cells; rushes to target at range 1.
    /// Adapted from 4Q-Rebellion's Slasher class.
    /// </summary>
    public class Slasher : Character
    {
        // ── Animator hashes ───────────────────────────────────────────────
        private static readonly int AnimFire = Animator.StringToHash("fire");
        private static readonly int AnimArmParam = Animator.StringToHash("arm");

        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Slasher — Knife Weapon")]
        [Tooltip("Child GameObject that acts as the knife hitbox (tagged 'Weapon').")]
        [SerializeField] private GameObject knifeHitbox;

        // Normalised-time collision windows, per distance (from C++ Slasher).
        [Tooltip("Knife active start fraction — close-range slash (distance 1).")]
        [SerializeField] [Range(0f, 1f)] private float closeStart = 0.208f;
        [Tooltip("Knife active end fraction — close-range slash (distance 1).")]
        [SerializeField] [Range(0f, 1f)] private float closeEnd   = 0.271f;
        [Tooltip("Knife active start fraction — far-range slash (distance 2).")]
        [SerializeField] [Range(0f, 1f)] private float farStart   = 0.3125f;
        [Tooltip("Knife active end fraction — far-range slash (distance 2).")]
        [SerializeField] [Range(0f, 1f)] private float farEnd     = 0.375f;

        // ── Runtime ───────────────────────────────────────────────────────
        private Collider _knifeCollider;
        private float _activeStart;
        private float _activeEnd;
        private Coroutine _slashRoutine;

        // ── Unity lifecycle ───────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            Type  = CharacterType.Slasher;
            range = 2;

            if (knifeHitbox != null)
            {
                _knifeCollider = knifeHitbox.GetComponent<Collider>();
                SetKnifeCollider(false);
            }

            // Default to far-range window.
            _activeStart = farStart;
            _activeEnd   = farEnd;
        }

        // ── TriggerAction override ─────────────────────────────────────────

        /// <summary>
        /// Select the correct animation window based on target distance then
        /// trigger.  Mirrors Slasher::TriggerAction.
        /// </summary>
        public override void TriggerAction()
        {
            if (IsTargetInRange && DistanceToTarget < range)
            {
                // Close-range slash uses action1 window.
                _activeStart = closeStart;
                _activeEnd   = closeEnd;
            }
            else
            {
                _activeStart = farStart;
                _activeEnd   = farEnd;
            }

            base.TriggerAction();

            if (IsTargetInRange && _slashRoutine == null)
                _slashRoutine = StartCoroutine(SlashRoutine());
        }

        // ── Slash coroutine ───────────────────────────────────────────────

        private IEnumerator SlashRoutine()
        {
            // Activate knife when animation reaches the window start.
            yield return new WaitUntil(() =>
                IsAnimNormalisedTimeAtLeast(_activeStart));

            SetKnifeCollider(true);

            yield return new WaitUntil(() =>
                IsAnimNormalisedTimeAtLeast(_activeEnd));

            SetKnifeCollider(false);

            // Rush: move into the target cell if it was at distance 1.
            // Mirrors Slasher::TakeOverTargetCell.
            if (IsTargetInRange && DistanceToTarget == 1)
                TakeOverTargetCell();

            _slashRoutine = null;
        }

        // ── Rush movement ─────────────────────────────────────────────────

        /// <summary>
        /// Move this Slasher into the cell previously occupied by its target.
        /// Mirrors Slasher::TakeOverTargetCell.
        /// </summary>
        private void TakeOverTargetCell()
        {
            if (grid == null || !IsTargetInRange) return;

            var (dw, dh) = GetGridFrontDirection();
            int targetW = GridW + DistanceToTarget * dw;
            int targetH = GridH + DistanceToTarget * dh;

            SetGridLocation(targetW, targetH);
        }

        // ── Helper ────────────────────────────────────────────────────────

        private void SetKnifeCollider(bool on)
        {
            if (_knifeCollider != null)
                _knifeCollider.enabled = on;
        }

        private bool IsAnimNormalisedTimeAtLeast(float threshold)
        {
            if (_animator == null) return false;
            var info = _animator.GetCurrentAnimatorStateInfo(0);
            return info.normalizedTime % 1f >= threshold;
        }
    }
}
