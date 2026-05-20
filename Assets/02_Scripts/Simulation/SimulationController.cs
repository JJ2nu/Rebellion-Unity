using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SimulationController : MonoBehaviour
{
    [SerializeField] private float stepDuration = 0.5f;

    [Header("Debug (Read-only)")]
    [SerializeField] private bool _isRunning;
    [SerializeField] private int _currentPhase;
    [SerializeField] private int _currentStep;
    [SerializeField] private string _lastResult = "-";

    public enum SimulationResult
    {
        PerfectWin,
        AllyDeadWin,
        CivilianDeadWin,
        BothDeadWin,
        Lose,
    }

    public void StartSimulation()
    {
        StartCoroutine(RunSimulation());
    }

    public void ResetSimulation()
    {
        StopAllCoroutines();
        _isRunning = false;
        _currentPhase = 0;
        _currentStep = 0;
        _lastResult = "-";
    }

    /// <summary>
    /// 배치 완료 직후 상태로 되돌린다. 버튼에 바인딩해서 사용.
    /// </summary>
    public void RetrySimulation()
    {
        ResetSimulation();
        StageManager.Instance?.ResetForRetry();
    }

    private IEnumerator RunSimulation()
    {
        _isRunning = true;
        _currentPhase = 0;
        _currentStep = 0;
        _lastResult = "-";

        var allPieces = StageManager.Instance.GetAllActivePieces();

        foreach (var piece in allPieces)
            piece.OnSimulationStart();

        // Phase 0: 공격 가능 여부 체크
        foreach (var piece in allPieces)
            piece.CheckCanAct(allPieces);

        // 모든 기물의 EffectivePhaseIndex를 수집해 오름차순으로 실행
        var phases = allPieces
            .Where(p => p.EffectivePhaseIndex > 0)
            .Select(p => p.EffectivePhaseIndex)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        foreach (var phase in phases)
            yield return RunPhase(phase, allPieces);

        var result = DetermineResult(allPieces);
        _lastResult = result.ToString();
        _isRunning = false;
        Debug.Log($"[Simulation] Result: {result}");
    }

    private IEnumerator RunPhase(int phaseIndex, IReadOnlyList<PieceBase> allPieces)
    {
        var active = allPieces
            .Where(p => !p.IsDead && p.EffectivePhaseIndex == phaseIndex && p.CanAct)
            .ToList();

        if (active.Count == 0) yield break;

        _currentPhase = phaseIndex;

        foreach (var piece in active)
            StartCoroutine(piece.ExecuteAction(allPieces, stepDuration));

        // 스탭 카운터: stepDuration마다 _currentStep 증가
        StartCoroutine(TickSteps());

        yield return new WaitUntil(() => active.All(p => p.IsActionFinished || p.IsDead));
    }

    private IEnumerator TickSteps()
    {
        _currentStep = 0;
        while (_isRunning)
        {
            yield return new WaitForSeconds(stepDuration);
            _currentStep++;
        }
    }

    private SimulationResult DetermineResult(IReadOnlyList<PieceBase> allPieces)
    {
        bool anyEnemyAlive = allPieces.Any(p => p.Faction == Faction.Enemy && !p.IsDead);
        if (anyEnemyAlive) return SimulationResult.Lose;

        bool anyAllyDead = allPieces.Any(p => p.Faction == Faction.Ally && p.IsDead);
        bool anyCivilianDead = allPieces.Any(p => p.Faction == Faction.Neutral && p.IsDead);

        if (anyAllyDead && anyCivilianDead) return SimulationResult.BothDeadWin;
        if (anyAllyDead) return SimulationResult.AllyDeadWin;
        if (anyCivilianDead) return SimulationResult.CivilianDeadWin;
        return SimulationResult.PerfectWin;
    }
}

