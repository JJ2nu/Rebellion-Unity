// Adapted from Client/Contents/GameObjects/Map/Characters/Brawler/Brawler.cpp
// and Brawler.h of 4Q-Rebellion (C++).
//
// Key adaptations:
//  - range = 1  (melee punch, same as C++ Brawler).
//  - The fist weapon (collision window 0.542 – 0.625 of the punch animation)
//    is represented as a child GameObject with a Collider tagged "Weapon".
//    Enable/disable the collider via Animator events or the coroutine below.
//  - Animation state machine: Idle → Action → Idle  (mirror of BrawlerActionState).

using System.Collections;
using UnityEngine;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Melee brawler character.  Range = 1 cell (punch).
    /// Adapted from 4Q-Rebellion's Brawler class.
    /// </summary>
    public class Brawler : Character
    {
        // ── Animator hashes (mirrors C++ animator state variables) ─────────
        private static readonly int AnimArm = Animator.StringToHash("triggered");

        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Brawler — Fist Weapon")]
        [Tooltip("Child GameObject that acts as the fist hitbox (tagged 'Weapon').")]
        [SerializeField] private GameObject fistHitbox;

        [Tooltip("Fraction of the punch animation when the fist collider activates.")]
        [SerializeField] [Range(0f, 1f)] private float fistActiveStart = 0.542f;
        [Tooltip("Fraction of the punch animation when the fist collider deactivates.")]
        [SerializeField] [Range(0f, 1f)] private float fistActiveEnd   = 0.625f;

        // ── Runtime ───────────────────────────────────────────────────────
        private Collider _fistCollider;
        private bool _fistEnabled;
        private Coroutine _punchRoutine;

        // ── Unity lifecycle ───────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            Type  = CharacterType.Brawler;
            range = 1;

            if (fistHitbox != null)
            {
                _fistCollider = fistHitbox.GetComponent<Collider>();
                SetFistCollider(false);
            }
        }

        // ── TriggerAction override ─────────────────────────────────────────

        /// <summary>
        /// Start the punch action.
        /// Mirrors Brawler's contribution to TriggerAction — enables the
        /// fist collider during the correct animation window.
        /// </summary>
        public override void TriggerAction()
        {
            base.TriggerAction();

            if (IsTargetInRange && _punchRoutine == null)
                _punchRoutine = StartCoroutine(PunchColliderRoutine());
        }

        // ── Weapon collider timing coroutine ──────────────────────────────

        /// <summary>
        /// Waits for the punch animation to reach the active window then
        /// enables/disables the fist collider.
        /// Mirrors the Brawler::Update check on action->GetCurrentAnimationTime().
        /// </summary>
        private IEnumerator PunchColliderRoutine()
        {
            // Wait until animation reaches fistActiveStart.
            yield return new WaitUntil(() =>
                IsAnimationNormalisedTimeAtLeast(fistActiveStart));

            SetFistCollider(true);

            // Wait until animation passes fistActiveEnd.
            yield return new WaitUntil(() =>
                IsAnimationNormalisedTimeAtLeast(fistActiveEnd));

            SetFistCollider(false);
            _punchRoutine = null;
        }

        private void SetFistCollider(bool on)
        {
            _fistEnabled = on;
            if (_fistCollider != null)
                _fistCollider.enabled = on;
        }

        /// <summary>
        /// Returns the current normalised time of the base layer animation.
        /// Used to replicate the C++ animation-time collision window check.
        /// </summary>
        private bool IsAnimationNormalisedTimeAtLeast(float threshold)
        {
            if (_animator == null) return false;
            var info = _animator.GetCurrentAnimatorStateInfo(0);
            return info.normalizedTime % 1f >= threshold;
        }
    }
}
