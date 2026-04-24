// Adapted from Client/Contents/GameObjects/Map/Types.h,
// Client/Contents/GameObjects/Map/Characters/Character.h,
// Client/Contents/GameObjects/Map/Grid/CellObject.h,
// and Client/Contents/Levels/GameLevel.h of 4Q-Rebellion (C++).
//
// Direction, Faction, CharacterType, CellType and BattleResult are
// directly mirrored from the original enum definitions and are the
// primary contract between GridMap, Character, and BattleManager.

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Cardinal directions used for character facing and target search.
    /// Matches 4Q-Rebellion's Direction enum (kNorth/kEast/kSouth/kWest).
    /// Grid convention: North = +Z, East = +X, South = -Z, West = -X.
    /// </summary>
    public enum Direction
    {
        North = 0,  // +Z
        East  = 1,  // +X
        South = 2,  // -Z
        West  = 3,  // -X
    }

    /// <summary>
    /// Which side a character belongs to.
    /// Matches 4Q-Rebellion's Faction enum (kAlly/kEnemy/kNeutral).
    /// </summary>
    public enum Faction
    {
        Ally    = 0,
        Enemy   = 1,
        Neutral = 2,
    }

    /// <summary>
    /// Character archetype, determining range, weapon, and animation.
    /// Matches 4Q-Rebellion's CharacterType enum.
    /// </summary>
    public enum CharacterType
    {
        Brawler  = 0,
        Slasher  = 1,
        Gunman   = 2,
        Civilian = 3,
        Eliza    = 4,
        Boss     = 5,
    }

    /// <summary>
    /// Visual state of a grid cell.
    /// Matches 4Q-Rebellion's CellType enum (CellType_Default …).
    /// </summary>
    public enum CellType
    {
        Default,
        Placement,
        RangeZone,
        DashZone,
        DamageZone,
    }

    /// <summary>
    /// Possible outcomes of a battle phase.
    /// Matches 4Q-Rebellion's eBattleResult enum in GameLevel.h.
    /// </summary>
    public enum BattleResult
    {
        PerfectWin,
        CivilDeadWin,
        AllyDeadWin,
        BothDeadWin,
        AllyDeadLose,
        Lose,
    }
}
