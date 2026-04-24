// Adapted from Client/Contents/GameObjects/Map/Characters/Civilian/
// of 4Q-Rebellion (C++).
//
// A civilian is a Neutral-faction character:
//  - Does not attack (FindTargetInRange always returns nothing).
//  - Cannot be harmed by Ally faction weapons (only Enemy weapons harm them).
//  - Mirrors the "Neutral" kFactionTags logic in Character.cpp.

using UnityEngine;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Neutral civilian.  Does not attack.  Killing one affects the battle result.
    /// Adapted from 4Q-Rebellion's Civilian class.
    /// </summary>
    public class Civilian : Character
    {
        protected override void Awake()
        {
            base.Awake();
            Type = CharacterType.Civilian;
            SetFaction(Faction.Neutral);
            range = 0; // civilians never attack
        }

        // Civilians never search for targets.
        protected override void FindTargetInRange()
        {
            IsTargetInRange  = false;
            DistanceToTarget = -1;
        }

        // Civilians cannot trigger actions (they are passive).
        public override void TriggerAction()
        {
            if (_animator != null)
                _animator.SetBool(AnimDone, true);
            IsActionFinished = true;
        }

        /// <summary>
        /// Civilians can only be harmed by weapons tagged "EnemyWeapon",
        /// mirroring the original game design where allies never kill civilians.
        /// </summary>
        protected override void OnTriggerEnter(Collider other)
        {
            if (battleManager != null && battleManager.IsPaused) return;
            if (IsPlacementModeOn || IsDead) return;

            // Accept damage only from enemy weapons.
            if (other.CompareTag("EnemyWeapon") || other.CompareTag("Weapon"))
            {
                base.OnTriggerEnter(other);
            }
        }
    }
}
