// Adapted from Client/Contents/GameObjects/Map/Map.h, Map.cpp and
// Client/Contents/GameObjects/GameManager/GameManager.h of 4Q-Rebellion (C++).
//
// Key adaptations:
//  - Map::CreateEnemyAt / CreateAllyAt / CreateCivillianAt →
//      BattleManager.SpawnCharacter (generic) + type-specific spawn methods.
//  - Map::TriggerAction → BattleManager.TriggerAction (starts the battle phase,
//    records initial roster for ResetGame).
//  - Map::ResetGame → BattleManager.ResetGame (destroys live characters,
//    replays record to restore the initial layout).
//  - Map::IsGameFinished → BattleManager.IsGameFinished.
//  - Map::GetNumDeadAllies/GetNumDeadCivilians etc. → BattleManager helpers.
//  - BattleResult evaluation logic mirrors GameLevel::SetBattleResult.
//  - Placement mode and assassination mode are stubbed for future implementation.
//  - C++ World::CreateGameObjectFromModel<T> → Unity Instantiate<T>(prefab).

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rebellion.Utils;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Central coordinator for the tactical battle map.
    /// Manages character spawning, turn triggering, battle-result evaluation,
    /// and game-reset (retry).  Replaces the C++ Map + GameManager duo.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Grid")]
        [SerializeField] private GridMap grid;

        [Header("Character Prefabs")]
        [SerializeField] private Brawler  allyBrawlerPrefab;
        [SerializeField] private Slasher  allySlasherPrefab;
        [SerializeField] private Gunman   allyGunmanPrefab;
        [SerializeField] private Brawler  enemyBrawlerPrefab;
        [SerializeField] private Gunman   enemyGunmanPrefab;
        [SerializeField] private Civilian civilianPrefab;

        [Header("Delays")]
        [Tooltip("Seconds to wait after TriggerAction before characters execute (mirrors snippingDelay).")]
        [SerializeField] private float actionStartDelay = 1f;

        // ── Events ────────────────────────────────────────────────────────
        // Battle outcome is broadcast on the shared EventBus so that
        // Rebellion.Core.GameManager can react without a direct assembly
        // dependency on Rebellion.Gameplay (mirrors the C++ callback chain).
        // Subscribe to Rebellion.Utils.BattleFinishedEvent to receive results.

        // ── Runtime state (mirrors Map fields) ────────────────────────────
        public bool IsPaused            { get; private set; }
        public bool IsActionTriggered   { get; private set; }

        private List<Character> _enemies   = new List<Character>();
        private List<Character> _allies    = new List<Character>();
        private List<Character> _civilians = new List<Character>();

        // Roster snapshot for ResetGame (mirrors Map::record).
        private struct CharacterRecord
        {
            public Faction       faction;
            public CharacterType type;
            public Direction     dir;
            public int           w, h;
        }
        private List<CharacterRecord> _record = new List<CharacterRecord>();

        private BattleResult _battleResult;
        private Coroutine    _actionCoroutine;

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (grid == null)
                grid = GetComponentInChildren<GridMap>();
        }

        // ── Public spawn API (mirrors Map::CreateEnemyAt / CreateAllyAt) ──

        /// <summary>Spawn an ally character at (w, h) facing dir.</summary>
        public void CreateAllyAt(CharacterType type, int w, int h,
                                 Direction dir = Direction.North)
        {
            Character character = SpawnAllyPrefab(type);
            if (character == null) return;

            character.SetFaction(Faction.Ally);
            character.SetDirection(dir);
            character.SetGridLocation(w, h);
            character.grid         = grid;
            character.battleManager = this;

            _allies.Add(character);
        }

        /// <summary>Spawn an enemy character at (w, h) facing dir.</summary>
        public void CreateEnemyAt(CharacterType type, int w, int h,
                                  Direction dir = Direction.North,
                                  bool isBoss   = false)
        {
            Character character = SpawnEnemyPrefab(type);
            if (character == null) return;

            character.SetFaction(Faction.Enemy);
            character.SetDirection(dir);
            character.SetGridLocation(w, h);
            character.grid          = grid;
            character.battleManager = this;

            _enemies.Add(character);
        }

        /// <summary>Spawn a civilian at (w, h).</summary>
        public void CreateCivilianAt(int w, int h, Direction dir = Direction.North)
        {
            if (civilianPrefab == null) return;

            Civilian civ = Instantiate(civilianPrefab);
            civ.SetFaction(Faction.Neutral);
            civ.SetDirection(dir);
            civ.SetGridLocation(w, h);
            civ.grid          = grid;
            civ.battleManager = this;

            _civilians.Add(civ);
        }

        // ── Battle control ────────────────────────────────────────────────

        /// <summary>
        /// Begin the action phase: snapshot the roster for potential reset,
        /// then trigger all characters after a short delay.
        /// Mirrors Map::TriggerAction + the bStartAction / snippingDelay flow.
        /// </summary>
        public void TriggerAction()
        {
            if (IsActionTriggered) return;
            IsActionTriggered = true;

            // Snapshot initial roster so ResetGame can restore it.
            RecordRoster();

            grid.TurnOffSelectionMode();
            grid.TurnOffGridHover();

            _actionCoroutine = StartCoroutine(ActionSequence());
        }

        private IEnumerator ActionSequence()
        {
            yield return new WaitForSeconds(actionStartDelay);

            // Trigger all characters simultaneously (mirrors the C++ loop).
            foreach (Character e in _enemies)   e.TriggerAction();
            foreach (Character a in _allies)    a.TriggerAction();
            foreach (Character c in _civilians) c.TriggerAction();

            // Wait until every character has finished its action.
            yield return new WaitUntil(AllActionsFinished);

            EvaluateBattleResult();
        }

        private bool AllActionsFinished()
        {
            foreach (Character e in _enemies)   if (!e.IsFinishedAction()) return false;
            foreach (Character a in _allies)    if (!a.IsFinishedAction()) return false;
            foreach (Character c in _civilians) if (!c.IsFinishedAction()) return false;
            return true;
        }

        /// <summary>
        /// Restore the map to its initial layout and allow the player to
        /// retry their placement.  Mirrors Map::ResetGame.
        /// </summary>
        public void ResetGame()
        {
            if (!IsActionTriggered) return;
            if (_actionCoroutine != null) StopCoroutine(_actionCoroutine);

            DestroyAllCharacters();
            grid.ClearGrid();

            IsActionTriggered = false;
            IsPaused          = false;

            // Re-create characters from the snapshot.
            foreach (CharacterRecord rec in _record)
            {
                switch (rec.faction)
                {
                    case Faction.Ally:
                        CreateAllyAt(rec.type, rec.w, rec.h, rec.dir);
                        break;
                    case Faction.Enemy:
                        CreateEnemyAt(rec.type, rec.w, rec.h, rec.dir,
                                      rec.type == CharacterType.Boss);
                        break;
                    case Faction.Neutral:
                        CreateCivilianAt(rec.w, rec.h, rec.dir);
                        break;
                }
            }

            _record.Clear();
        }

        // ── Pause / resume (mirrors Map::PauseGame / ResumeGame) ──────────

        public void PauseGame()  => IsPaused = true;
        public void ResumeGame() => IsPaused = false;

        // ── Query helpers (mirror Map::GetNum* methods) ───────────────────

        public int GetNumEnemies()        => _enemies.Count;
        public int GetNumAllies()         => _allies.Count;
        public int GetNumCivilians()      => _civilians.Count;
        public int GetNumDeadEnemies()    => CountDead(_enemies);
        public int GetNumDeadAllies()     => CountDead(_allies);
        public int GetNumDeadCivilians()  => CountDead(_civilians);

        /// <summary>
        /// Returns true when the battle is over (all enemies dead or all allies dead).
        /// Mirrors Map::IsGameFinished.
        /// </summary>
        public bool IsGameFinished()
        {
            if (_enemies.Count == 0) return true;
            if (_allies.Count  == 0) return false;
            return GetNumDeadEnemies()  == _enemies.Count
                || GetNumDeadAllies()   == _allies.Count;
        }

        public BattleResult GetBattleResult() => _battleResult;

        // ── Private helpers ───────────────────────────────────────────────

        private void RecordRoster()
        {
            _record.Clear();
            foreach (Character e in _enemies)
                _record.Add(new CharacterRecord
                    { faction = e.Faction, type = e.Type, dir = e.Dir,
                      w = e.GridW, h = e.GridH });
            foreach (Character a in _allies)
                _record.Add(new CharacterRecord
                    { faction = a.Faction, type = a.Type, dir = a.Dir,
                      w = a.GridW, h = a.GridH });
            foreach (Character c in _civilians)
                _record.Add(new CharacterRecord
                    { faction = c.Faction, type = c.Type, dir = c.Dir,
                      w = c.GridW, h = c.GridH });
        }

        /// <summary>
        /// Determine the battle outcome.
        /// Mirrors the eBattleResult logic implied by GameLevel::SetBattleResult.
        /// </summary>
        private void EvaluateBattleResult()
        {
            int deadEnemies   = GetNumDeadEnemies();
            int deadAllies    = GetNumDeadAllies();
            int deadCivilians = GetNumDeadCivilians();

            bool allEnemiesDead = deadEnemies   == _enemies.Count;
            bool anyAllyDead    = deadAllies    > 0;
            bool anyCivilDead   = deadCivilians > 0;
            bool allAlliesDead  = deadAllies    == _allies.Count && _allies.Count > 0;

            if (allAlliesDead && !allEnemiesDead)
                _battleResult = BattleResult.Lose;
            else if (allEnemiesDead && anyAllyDead && anyCivilDead)
                _battleResult = BattleResult.BothDeadWin;
            else if (allEnemiesDead && anyAllyDead)
                _battleResult = BattleResult.AllyDeadWin;
            else if (allEnemiesDead && anyCivilDead)
                _battleResult = BattleResult.CivilDeadWin;
            else if (allEnemiesDead)
                _battleResult = BattleResult.PerfectWin;
            else
                _battleResult = BattleResult.AllyDeadLose;

            bool isVictory = _battleResult == BattleResult.PerfectWin
                          || _battleResult == BattleResult.CivilDeadWin
                          || _battleResult == BattleResult.AllyDeadWin
                          || _battleResult == BattleResult.BothDeadWin;

            EventBus.Publish(new BattleFinishedEvent
            {
                IsVictory  = isVictory,
                ResultCode = (int)_battleResult,
            });
        }

        private void DestroyAllCharacters()
        {
            foreach (Character e in _enemies)   if (e) Destroy(e.gameObject);
            foreach (Character a in _allies)    if (a) Destroy(a.gameObject);
            foreach (Character c in _civilians) if (c) Destroy(c.gameObject);
            _enemies.Clear();
            _allies.Clear();
            _civilians.Clear();
        }

        private Character SpawnAllyPrefab(CharacterType type)
        {
            return type switch
            {
                CharacterType.Brawler => allyBrawlerPrefab ? Instantiate(allyBrawlerPrefab) : null,
                CharacterType.Slasher => allySlasherPrefab ? Instantiate(allySlasherPrefab) : null,
                CharacterType.Gunman  => allyGunmanPrefab  ? Instantiate(allyGunmanPrefab)  : null,
                _                     => null,
            };
        }

        private Character SpawnEnemyPrefab(CharacterType type)
        {
            return type switch
            {
                CharacterType.Brawler => enemyBrawlerPrefab ? Instantiate(enemyBrawlerPrefab) : null,
                CharacterType.Gunman  => enemyGunmanPrefab  ? Instantiate(enemyGunmanPrefab)  : null,
                _                     => null,
            };
        }

        private static int CountDead(List<Character> characters)
        {
            int count = 0;
            foreach (Character c in characters)
                if (c != null && c.IsDead) count++;
            return count;
        }
    }
}
