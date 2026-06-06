using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SimulationController : MonoBehaviour
{
    public static SimulationController Instance { get; private set; }

    [SerializeField] private float stepDuration = 0.5f;
    // ... (rest of the code)

    [Header("Debug (Read-only)")]
    public bool _isRunning;
    [SerializeField] private int _currentPhase;
    [SerializeField] private int _currentStep;
    [SerializeField] private string _lastResult = "-";


    [SerializeField] InputActionReference _leftClickAction;
    [SerializeField] InputActionReference _rightClickAction;

    private PieceBase _currentClickedPiece = null;
    [SerializeField] private List<SkillBase> _skills;
    public IReadOnlyList<SkillBase> GetStageSkills() => _skills.AsReadOnly();

    public enum SimulationResult
    {
        PerfectWin,
        AllyDeadWin,
        CivilianDeadWin,
        BothDeadWin,
        Lose,
    }

    [SerializeField] public SimulationResult LastSimulationResult = SimulationResult.Lose; 

    public enum Skills
    {
        OpeningShot,
        RegressorWatch,
        End
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {

            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject);
    }
    public void StartSimulation()
    {
        StartCoroutine(RunSimulation());
    }

    public void ResetSimulation()
    {
        StopAllCoroutines();
        foreach (var piece in StageManager.Instance.GetAllPieces())
        {
            if (piece != null)
            {
                piece.ResetState();
                piece.IsDead = false;
            }
        }
        GameManager.Instance.ResetAllTile();
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

        GameManager.Instance.ClearAllTile();
        var allPieces = StageManager.Instance.GetAllActivePieces();
        foreach (var skill in _skills)
        {
            if (skill.CanExecute(allPieces))
            {
                Debug.Log($"[Simulation] Executing Skill: {skill.SkillName}");
                skill.Execute(this, allPieces);
            }
        }
        allPieces = StageManager.Instance.GetAllActivePieces();

        foreach (var piece in allPieces)
            piece.OnSimulationStart();

        var totalPieces = StageManager.Instance.GetAllPieces();
        foreach(var piece in totalPieces)
        {
            if (piece != null)
            {
                piece._HUD.SetActive(false);
            }
        }

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
    public void SetTargetForPreSimulation(int skillIndex)
    {
        var pieces = StageManager.Instance.GetAllActivePieces();
        StartCoroutine(_skills[skillIndex].TargetMode(this, pieces));
    }
    public void OnClickPiece(PieceBase piece)
    {
       PreviousClickAction();
        _currentClickedPiece = piece;
        ClickAction();
       
    }
    void PreviousClickAction()
    {
         var previousClickedPiece = _currentClickedPiece;
        if (previousClickedPiece != null)
        {
            if (((OpeningShotSkill)_skills[0]).isTargetingMode)
            {

                if (previousClickedPiece._isTargeted)
                {
                    previousClickedPiece._isTargeted = false;
                }
            }
        }
    }
    void ClickAction()
    {
         if (_currentClickedPiece != null)
        {
            if (((OpeningShotSkill)_skills[0]).isTargetingMode)
            {
                if (_currentClickedPiece.Faction == Faction.Enemy && !_currentClickedPiece.IsDead)
                {
                    ((OpeningShotSkill)_skills[0]).Target = _currentClickedPiece;
                    ((OpeningShotSkill)_skills[0]).Target._isTargeted = true;
                    ((OpeningShotSkill)_skills[0]).isTargetingMode = false;
                }
            }
        }
    }

}
 
