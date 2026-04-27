namespace Rebellion
{
    public enum Faction
    {
        Ally = 0,
        Enemy = 1,
        Neutral = 2,
    }

    public enum PieceType
    {
        Brawler = 0,
        Slasher = 1,
        Gunman = 2,
        Civilian = 3,
        Boss = 4,
    }

    public enum Direction
    {
        North = 0, // +z
        East = 1,  // +x
        South = 2, // -z
        West = 3,  // -x
    }

    public enum SimulationPhase
    {
        Setup,
        PreSimulation,
        Simulating,
        Finished,
    }

    public enum SkillTiming
    {
        PreSimulation,
        DuringSimulation,
        PostSimulation,
    }
}
