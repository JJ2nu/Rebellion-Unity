using System;
using UnityEngine;
using Rebellion;

public class SimulationController : MonoBehaviour
{
    [SerializeField] private GameObject stageLoader;

    public enum SimulationResult
    {
        PerfectWin,
        AllyDeadWin,
        CivilianDeadWin,
        BothDeadWin,
        Lose,
    }

    private int currentPhaseIndex = 0;
    private SimulationResult simulationResult;

    //public void Register

    public void StartSimulation()
    {
        
    }
    public void ResetSimulation()
    {
    }
    
}
