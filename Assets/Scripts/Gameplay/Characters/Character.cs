// Adapted from Client/Contents/GameObjects/Map/Characters/Character.h and
// Character.cpp of 4Q-Rebellion (C++).
//
// Key adaptations:
//  - C++ AnimatorComponent variables ("dead", "triggered", "done") →
//    Unity Animator hash parameters (same names).
//  - C++ PhysX RigidbodyComponent overlap → Unity OnTriggerEnter with a
//    Collider on the weapon child.
//  - Static model/skeleton handles → Unity [SerializeField] Animator /
//    SpriteRenderer; the concrete subclasses assign them via prefabs.
//  - Health-point damage via OnTriggerEnter("weapon" tag) is preserved.
//  - FindTargetInRange logic is translated verbatim from C++:
//      scan forward cell-by-cell up to `range`, honour faction rules and
//      the Gunman-specific "skip obstacles" rule.
//  - TriggerAction / Die / ApplyChangedGridLocation all mirror the
//    original method semantics.

using UnityEngine;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Base class for all tactical characters (Brawler, Slasher, Gunman, Civilian).
    /// Mirrors the C++ Character class: grid position, faction, range,
    /// direction, target-detection, action trigger, and death.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public abstract class Character : MonoBehaviour
    {
        // ── Animator parameter hashes (mirrors C++ animator variables) ────
        protected static readonly int AnimDead      = Animator.StringToHash("dead");
        protected static readonly int AnimTriggered = Animator.StringToHash("triggered");
        protected static readonly int AnimDone      = Animator.StringToHash("done");

        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Character Stats")]
        [SerializeField] protected int startingHealth = 1;
        [SerializeField] protected int range = 1;

        [Header("References — assigned by BattleManager at spawn")]
        [SerializeField] public GridMap grid;
        [SerializeField] public BattleManager battleManager;

        // ── Runtime state (mirrors Character fields) ──────────────────────

        /// <summary>Character type: Brawler, Slasher, Gunman, Civilian …</summary>
        public CharacterType Type { get; protected set; }

        /// <summary>Which side the character belongs to.</summary>
        public Faction Faction { get; private set; }

        /// <summary>Facing direction in the four cardinal directions.</summary>
        public Direction Dir { get; private set; } = Direction.East;

        /// <summary>Grid column.</summary>
        public int GridW { get; private set; }
        /// <summary>Grid row.</summary>
        public int GridH { get; private set; }

        /// <summary>Remaining hit points.</summary>
        public int Health { get; private set; }

        public bool IsDead             { get; private set; }
        public bool IsActionTriggered  { get; private set; }
        public bool IsActionFinished   { get; protected set; }
        public bool IsPlacementModeOn  { get; private set; }

        /// <summary>True when the character has a valid target within its attack range.</summary>
        public bool IsTargetInRange  { get; protected set; }
        /// <summary>How many cells away the nearest target is (-1 = none).</summary>
        public int  DistanceToTarget { get; protected set; } = -1;

        // Dirty flags (mirrors bGridLocationChanged / bDirectionChanged)
        private bool _dirtyGridLocation;
        private bool _dirtyDirection;

        protected Animator _animator;

        // ── Unity lifecycle ───────────────────────────────────────────────

        protected virtual void Awake()
        {
            _animator = GetComponent<Animator>();
            Health    = startingHealth;
        }

        protected virtual void Start()
        {
            // Locate grid/battleManager if not assigned in Inspector.
            if (grid == null)
                grid = FindFirstObjectByType<GridMap>();
            if (battleManager == null)
                battleManager = FindFirstObjectByType<BattleManager>();

            if (_dirtyGridLocation) ApplyChangedGridLocation();
            if (_dirtyDirection)    ApplyChangedDirection();
        }

        protected virtual void Update()
        {
            if (battleManager != null && battleManager.IsPaused) return;

            // Mirror: if (animator->GetVariable<bool>("done")) isActionFinished = true;
            if (_animator != null)
                IsActionFinished = _animator.GetBool(AnimDone);

            if (IsDead) return;

            if (_dirtyGridLocation) ApplyChangedGridLocation();
            if (_dirtyDirection)    ApplyChangedDirection();

            if (!IsActionTriggered && !IsPlacementModeOn)
                FindTargetInRange();
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Start this character's action.
        /// Mirrors Character::TriggerAction.
        /// </summary>
        public virtual void TriggerAction()
        {
            IsActionTriggered = true;

            if (IsTargetInRange)
                _animator?.SetBool(AnimTriggered, true);
            else
                _animator?.SetBool(AnimDone, true);
        }

        public bool IsFinishedAction() => IsActionFinished;

        /// <summary>Set the faction (Ally/Enemy/Neutral).</summary>
        public void SetFaction(Faction faction)
        {
            Faction = faction;
            gameObject.tag = FactionTag(faction);
        }

        /// <summary>
        /// Change the facing direction.
        /// Mirrors Character::SetDirection — sets the dirty flag so the
        /// rotation is applied in Update.
        /// </summary>
        public void SetDirection(Direction direction)
        {
            Dir = direction;
            _dirtyDirection = true;
        }

        /// <summary>
        /// Move this character to a new grid cell.
        /// Mirrors Character::SetGridLocation.
        /// </summary>
        public void SetGridLocation(int w, int h)
        {
            GridW = w;
            GridH = h;
            _dirtyGridLocation = true;
        }

        public (int w, int h) GetGridLocation() => (GridW, GridH);

        /// <summary>
        /// Returns the (dW, dH) offset in the direction this character faces.
        /// Mirrors Character::GetGridFrontDirection.
        /// </summary>
        public (int dw, int dh) GetGridFrontDirection()
        {
            return Dir switch
            {
                Direction.North => (0,  1),
                Direction.East  => (1,  0),
                Direction.South => (0, -1),
                Direction.West  => (-1, 0),
                _               => (0,  0),
            };
        }

        public void SetPlacementMode(bool on) => IsPlacementModeOn = on;

        /// <summary>
        /// Kill this character.
        /// Mirrors Character::Die — triggers the death animation and
        /// disables physics interaction.
        /// </summary>
        public virtual void Die()
        {
            if (IsDead) return;
            IsDead = true;
            _animator?.SetBool(AnimDead, true);

            // Disable any Collider so weapons pass through dead bodies.
            var col = GetComponent<Collider>();
            if (col) col.enabled = false;
        }

        /// <summary>
        /// Deal damage from a ranged weapon (bullet).
        /// Distinct from melee collision so future implementations can add
        /// separate hit effects.
        /// </summary>
        public void TakeDamageFromBullet(int amount)
        {
            if (IsDead || IsPlacementModeOn) return;
            Health -= amount;
            if (Health <= 0)
                Die();
        }

        // ── Unity collision: weapon hit (mirrors OnBeginOverlap) ──────────

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (battleManager != null && battleManager.IsPaused) return;
            if (IsPlacementModeOn || IsDead) return;

            if (other.CompareTag("Weapon"))
            {
                Health -= 1;
                if (Health <= 0)
                    Die();
            }
        }

        // ── Target-finding (mirrors Character::FindTargetInRange verbatim) ─

        /// <summary>
        /// Scan forward (in the character's facing direction) up to
        /// <c>range</c> cells. Sets <see cref="IsTargetInRange"/> and
        /// <see cref="DistanceToTarget"/>.
        /// </summary>
        protected virtual void FindTargetInRange()
        {
            if (grid == null) { IsTargetInRange = false; DistanceToTarget = -1; return; }

            var (dw, dh) = GetGridFrontDirection();
            int searchW = GridW;
            int searchH = GridH;

            for (int i = 1; i <= range; i++)
            {
                searchW += dw;
                searchH += dh;

                if (!grid.InBounds(searchW, searchH)) break;

                Character target = grid.GetCharacterAt(searchW, searchH);

                if (target == null) continue;

                // Don't attack neutrals — stop searching.
                if (target.Faction == Faction.Neutral)
                {
                    IsTargetInRange  = false;
                    DistanceToTarget = -1;
                    return;
                }

                // Obstacle check (mirrors "Obstacle" tag logic).
                // Gunman can shoot through obstacles; others cannot.
                if (target.CompareTag("Obstacle"))
                {
                    if (Type == CharacterType.Gunman) continue;
                    IsTargetInRange  = false;
                    DistanceToTarget = -1;
                    return;
                }

                // Hostile faction found.
                if (target.Faction != Faction)
                {
                    IsTargetInRange  = true;
                    DistanceToTarget = i;
                    return;
                }

                // Friendly faction — stop searching.
                IsTargetInRange  = false;
                DistanceToTarget = -1;
                return;
            }

            IsTargetInRange  = false;
            DistanceToTarget = -1;
        }

        // ── Private helpers ───────────────────────────────────────────────

        /// <summary>
        /// Snap the GameObject to the world position of its grid cell and
        /// register with the GridMap.  Mirrors Character::ApplyChangedGridLocation.
        /// </summary>
        private void ApplyChangedGridLocation()
        {
            if (grid == null) return;
            grid.MoveCharacterTo(this, GridW, GridH);
            transform.position = grid.WorldPositionAt(GridW, GridH);
            _dirtyGridLocation = false;
        }

        /// <summary>
        /// Apply a Y-axis rotation matching the character's facing direction.
        /// Mirrors Character::ApplyChangedDirection (XM_PIDIV2 / XM_PI angles).
        /// North=180°, East=270°, South=0°, West=90°.
        /// </summary>
        private void ApplyChangedDirection()
        {
            float yAngle = Dir switch
            {
                Direction.North => 180f,
                Direction.East  => 270f,
                Direction.South => 0f,
                Direction.West  => 90f,
                _               => 0f,
            };
            transform.rotation = Quaternion.Euler(0f, yAngle, 0f);
            _dirtyDirection = false;
        }

        // ── Static utility ────────────────────────────────────────────────

        /// <summary>GameObject tag string for a faction. Mirrors kFactionTags[].</summary>
        public static string FactionTag(Faction f) => f switch
        {
            Faction.Ally    => "Ally",
            Faction.Enemy   => "Enemy",
            Faction.Neutral => "Neutral",
            _               => "Untagged",
        };
    }
}
