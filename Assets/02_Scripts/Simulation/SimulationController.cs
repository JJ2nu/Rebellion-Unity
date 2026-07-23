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

    // SimulationFinished는 결과 UI에 값을 전달하고, RunningStateChanged는 배치 UI 잠금 상태를 동기화한다.
    public event Action<SimulationResult> SimulationFinished;
    public event Action<bool> RunningStateChanged;
    public event Action SimulationReset;
    public event Action<SimulationMissionFacts> MissionFactsChanged;
    public event Action<SimulationMissionFacts> MissionStartFactsFinalized;

    public List<int> _currentDeadCount;
    [SerializeField] private int _currentStageEnemyCount;
    [SerializeField] private List<int> _currentStageEnemyTypeCounts = new();
    public int CurrentStageEnemyCount => _currentStageEnemyCount;
    public IReadOnlyList<int> CurrentStageEnemyTypeCounts => _currentStageEnemyTypeCounts;
    public int CurrentDeadEnemyCount => GetDeadCount(Faction.Enemy);
    public SimulationMissionFacts CurrentMissionFacts { get; private set; }
    public enum SimulationResult
    {
        PerfectWin,
        AllyDeadWin,
        CivilianDeadWin,
        BothDeadWin,
        AllyDeadLose,
        Lose,
    }

    [SerializeField] public SimulationResult LastSimulationResult = SimulationResult.Lose; 
    private bool isExecutingSimulation;
    private readonly HashSet<Skills> executedSkills = new();
    private readonly List<PieceBase> missionFactTrackedPieces = new();

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
        _currentDeadCount = new List<int>();
        foreach (Faction f in Enum.GetValues(typeof(Faction)))
        {
            _currentDeadCount.Add(0);
        }
        // DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        StopMissionFactTracking();
    }

    public void SetStageEnemyPieceCounts(IReadOnlyList<int> enemyTypeCounts)
    {
        _currentStageEnemyTypeCounts.Clear();
        _currentStageEnemyCount = 0;

        if (enemyTypeCounts == null)
        {
            ResetMissionFacts();
            return;
        }

        for (int index = 0; index < enemyTypeCounts.Count; index++)
        {
            int count = Mathf.Max(0, enemyTypeCounts[index]);
            _currentStageEnemyTypeCounts.Add(count);
            _currentStageEnemyCount += count;
        }

        ResetMissionFacts();
    }

    public int GetCurrentStageEnemyCount(PieceType pieceType)
    {
        int index = (int)pieceType;
        if (index < 0 || index >= _currentStageEnemyTypeCounts.Count)
        {
            return 0;
        }

        return _currentStageEnemyTypeCounts[index];
    }

    public void StartSimulation()
    {
        if (_isRunning || isExecutingSimulation)
        {
            return;
        }

        StageManager.Instance?.CompleteRetryResetImmediately();
        StartCoroutine(RunSimulation());
    }

    public void ResetSimulation()
    {
        StopAllCoroutines();
        isExecutingSimulation = false;
        StopMissionFactTracking();

        foreach (var bullet in GameObject.FindGameObjectsWithTag("Bullet"))
        {
            Destroy(bullet);
        }

        foreach (var piece in StageManager.Instance.GetAllPieces())
        {
            if (piece != null)
            {
                piece.GetComponent<OutlineEffect>()?.ClearPersistent();
                piece.ResetState();
                piece.IsDead = false;
            }
        }

        GameManager.Instance.ResetAllTile();
        SetRunning(false);
        _currentPhase = 0;
        _currentStep = 0;
        _lastResult = "-";
        for (int i = 0; i < _currentDeadCount.Count; i++)
        {
            _currentDeadCount[i] = 0;
        }

        executedSkills.Clear();
        ResetMissionFacts();

        foreach (var skill in _skills)
        {
            skill?.ResetTarget();
        }

        // Retry와 스테이지 재로드에서 결과 UI가 같은 초기화 시점을 사용하도록 알린다.
        SimulationReset?.Invoke();
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
        SetRunning(true);
        isExecutingSimulation = true;
        _currentPhase = 0;
        _currentStep = 0;
        _lastResult = "-";
        executedSkills.Clear();
        ResetMissionFacts();

        GameManager.Instance.ClearAllTile();
        var allPieces = StageManager.Instance.GetAllActivePieces();
        StartMissionFactTracking(allPieces);
        foreach (PieceBase piece in allPieces)
        {
            piece?.GetComponent<OutlineEffect>()?.ClearPersistent();
        }

        foreach (var skill in _skills)
        {
            if (skill.CanExecute(allPieces))
            {
                Debug.Log($"[Simulation] Executing Skill: {skill.SkillName}");
                skill.Execute(this, allPieces);
            }
        }

        // 선처리 스킬이 모두 실행된 뒤 시작 판정 미션이 사용할 사실을 한 번 확정한다.
        CurrentMissionFacts = BuildMissionFacts(StageManager.Instance.GetAllPieces());
        MissionStartFactsFinalized?.Invoke(CurrentMissionFacts);
        allPieces = StageManager.Instance.GetAllActivePieces();

        foreach (var piece in allPieces)
        {
            piece.OnSimulationStart();
        }

        var totalPieces = StageManager.Instance.GetAllPieces();
        foreach(var piece in totalPieces)
        {
            if (piece != null && piece._HUD != null)
            {
                piece._HUD?.SetActive(false);
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
        ShowSurvivingEnemyOutlines(allPieces);
        CurrentMissionFacts = BuildMissionFacts(StageManager.Instance.GetAllPieces());
        StopMissionFactTracking();
        _lastResult = result.ToString();
        LastSimulationResult = result;
        isExecutingSimulation = false;
        Debug.Log($"[Simulation] Result: {result}");
        SimulationFinished?.Invoke(result);
    }

    private static void ShowSurvivingEnemyOutlines(IReadOnlyList<PieceBase> allPieces)
    {
        foreach (PieceBase piece in allPieces)
        {
            if (piece == null || piece.IsDead || piece.Faction != Faction.Enemy)
            {
                continue;
            }

            piece.GetComponent<OutlineEffect>()?.ShowPersistent(Color.red);
        }
    }

    public void RecordSkillExecution(Skills skill)
    {
        // 타겟 선택이 아니라 실제 효과 실행 시점만 기록해 미사용 미션을 정확히 판정한다.
        executedSkills.Add(skill);
        RefreshMissionFactsAndNotify();
    }

    private void StartMissionFactTracking(IReadOnlyList<PieceBase> pieces)
    {
        StopMissionFactTracking();
        if (pieces == null)
        {
            return;
        }

        // PieceBase가 실제 사망을 확정한 OnDied 시점을 사용해 보호 미션을 즉시 갱신한다.
        foreach (PieceBase piece in pieces)
        {
            if (piece == null)
            {
                continue;
            }

            piece.OnDied -= HandleTrackedPieceDied;
            piece.OnDied += HandleTrackedPieceDied;
            missionFactTrackedPieces.Add(piece);
        }
    }

    private void StopMissionFactTracking()
    {
        foreach (PieceBase piece in missionFactTrackedPieces)
        {
            if (piece != null)
            {
                piece.OnDied -= HandleTrackedPieceDied;
            }
        }

        missionFactTrackedPieces.Clear();
    }

    private void HandleTrackedPieceDied(PieceBase _)
    {
        RefreshMissionFactsAndNotify();
    }

    private void RefreshMissionFactsAndNotify()
    {
        if (!isExecutingSimulation || StageManager.Instance == null)
        {
            return;
        }

        CurrentMissionFacts = BuildMissionFacts(StageManager.Instance.GetAllPieces());
        MissionFactsChanged?.Invoke(CurrentMissionFacts);
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

        foreach (var piece in active)
        {
            piece.EndAttackHitboxes();
        }
    }

    private IEnumerator TickSteps()
    {
        _currentStep = 0;
        while (isExecutingSimulation)
        {
            yield return new WaitForSeconds(stepDuration);
            _currentStep++;
        }
    }

    public void MarkSimulationConfirmed()
    {
        // 실행 코루틴이 끝나도 결과 확정 전까지는 배치 모드를 잠근 상태로 유지한다.
        SetRunning(false);
    }

    private void SetRunning(bool isRunning)
    {
        if (_isRunning == isRunning)
        {
            return;
        }

        _isRunning = isRunning;
        RunningStateChanged?.Invoke(_isRunning);
    }

    private SimulationResult DetermineResult(IReadOnlyList<PieceBase> allPieces)
    {
        // 이벤트 누적 카운터 대신 최종 기물 상태를 사용해 중복 사망 이벤트나 Retry 타이밍의 영향을 피한다.
        // 적이 남은 실패를 먼저 판정한 뒤, 적 전멸 상태에서 아군/민간인 피해 정도를 세분화한다.
        bool anyEnemyAlive = allPieces.Any(p => p.Faction == Faction.Enemy && !p.IsDead);
        if (anyEnemyAlive) return SimulationResult.Lose;

        int allyDeadCount = allPieces.Count(p => p.Faction == Faction.Ally && p.IsDead);
        if (allyDeadCount >= 3) return SimulationResult.AllyDeadLose;

        bool anyAllyDead = allyDeadCount > 0;
        bool anyCivilianDead = allPieces.Any(p => p.Faction == Faction.Neutral && p.IsDead);

        if (anyAllyDead && anyCivilianDead) return SimulationResult.BothDeadWin;
        if (anyAllyDead) return SimulationResult.AllyDeadWin;
        if (anyCivilianDead) return SimulationResult.CivilianDeadWin;
        return SimulationResult.PerfectWin;
    }

    private int GetDeadCount(Faction faction)
    {
        int index = (int)faction;
        if (_currentDeadCount == null || index < 0 || index >= _currentDeadCount.Count)
        {
            return 0;
        }

        return Mathf.Max(0, _currentDeadCount[index]);
    }

    private SimulationMissionFacts BuildMissionFacts(IReadOnlyList<PieceBase> allPieces)
    {
        int deadEnemyCount = allPieces.Count(piece => piece.Faction == Faction.Enemy && piece.IsDead);
        int deadAllyCount = allPieces.Count(piece => piece.Faction == Faction.Ally && piece.IsDead);
        StageManager stageManager = StageManager.Instance;
        int deadCivilianCount = stageManager != null
            ? stageManager.GetDeadCivilianCount(CivilianType.Civilian)
            : 0;
        int deadElizaCount = stageManager != null
            ? stageManager.GetDeadCivilianCount(CivilianType.Eliza)
            : 0;

        return new SimulationMissionFacts(
            _currentStageEnemyCount,
            deadEnemyCount,
            deadAllyCount,
            deadCivilianCount,
            deadElizaCount,
            executedSkills.Contains(Skills.OpeningShot));
    }

    private void ResetMissionFacts()
    {
        CurrentMissionFacts = new SimulationMissionFacts(
            _currentStageEnemyCount,
            0,
            0,
            0,
            0,
            false);
    }

    public void SetTargetForPreSimulation(int skillIndex)
    {
        if (_skills == null || skillIndex < 0 || skillIndex >= _skills.Count)
        {
            return;
        }

        SetTargetForPreSimulation(_skills[skillIndex]);
    }

    public void SetTargetForPreSimulation(SkillBase skill)
    {
        // UI가 이미 찾은 스킬 인스턴스를 넘기면 리스트 순서에 의존하지 않고 타겟팅 모드를 시작할 수 있다.
        if (skill == null || StageManager.Instance == null)
        {
            return;
        }

        var pieces = StageManager.Instance.GetAllActivePieces();
        StartCoroutine(skill.TargetMode(this, pieces));
    }
    public void OnClickPiece(PieceBase piece)
    {
       PreviousClickAction();
        _currentClickedPiece = piece;
        ClickAction();
       
    }

    public void OnRightClickPiece(PieceBase piece)
    {
        // 확정된 OpeningShot 타겟을 다시 우클릭하면 선택을 해제해 버튼/스코프 상태를 초기화한다.
        if (piece == null || _skills == null || _skills.Count == 0)
        {
            return;
        }

        OpeningShotSkill openingShotSkill = GetOpeningShotSkill();
        if (openingShotSkill == null || !openingShotSkill.HasConfirmedTarget)
        {
            return;
        }

        if (openingShotSkill.Target == piece)
        {
            openingShotSkill.ResetTarget();
        }
    }

    void PreviousClickAction()
    {
        var previousClickedPiece = _currentClickedPiece;
        OpeningShotSkill openingShotSkill = GetOpeningShotSkill();
        if (previousClickedPiece != null && openingShotSkill != null)
        {
            // 타겟팅 중 다른 피스를 누를 때 이전 후보 표시가 남지 않게 정리한다.
            if (openingShotSkill.isTargetingMode)
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
            // OpeningShot 타겟팅 중에는 월드 클릭을 일반 선택이 아니라 선처치 대상 확정으로 해석한다.
            OpeningShotSkill openingShotSkill = GetOpeningShotSkill();
            if (openingShotSkill != null && openingShotSkill.isTargetingMode)
            {
                if (_currentClickedPiece.Faction == Faction.Enemy && !_currentClickedPiece.IsDead)
                {
                    openingShotSkill.ConfirmTarget(_currentClickedPiece);
                }
            }
        }
    }

    private OpeningShotSkill GetOpeningShotSkill()
    {
        // 스킬 리스트 순서가 바뀌어도 OpeningShot만 정확히 찾아 타겟팅 흐름에 사용한다.
        return _skills != null ? _skills.OfType<OpeningShotSkill>().FirstOrDefault() : null;
    }

}
 
