using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rebellion
{
    /// <summary>
    /// 시뮬레이션 전체 흐름을 관리하는 컨트롤러.
    /// Play 버튼 → PreSimulation(스킬 발동) → Simulating(기물 동시 행동) → Finished(결과 판정)
    /// </summary>
    public class SimulationController : MonoBehaviour
    {
        // ─── Inspector ──────────────────────────────────────────────────
        [Header("Pieces")]
        [SerializeField] private List<PieceBase> _pieces = new();

        // ─── State ──────────────────────────────────────────────────────
        private SimulationPhase _phase = SimulationPhase.Setup;
        private readonly List<SkillBase> _skills = new();

        public SimulationPhase Phase => _phase;
        public IReadOnlyList<PieceBase> AllPieces => _pieces;

        // ─── Events ─────────────────────────────────────────────────────
        public event Action<SimulationPhase> OnPhaseChanged;
        public event Action<SimulationResult> OnSimulationFinished;

        // ─── Public API ─────────────────────────────────────────────────

        public void RegisterSkill(SkillBase skill) => _skills.Add(skill);
        public void UnregisterSkill(SkillBase skill) => _skills.Remove(skill);

        public void RegisterPiece(PieceBase piece)
        {
            if (!_pieces.Contains(piece))
                _pieces.Add(piece);
        }

        public void UnregisterPiece(PieceBase piece) => _pieces.Remove(piece);

        /// <summary>
        /// Play 버튼에서 호출. Setup 단계일 때만 시뮬레이션을 시작한다.
        /// </summary>
        public void StartSimulation()
        {
            if (_phase != SimulationPhase.Setup)
            {
                Debug.LogWarning("[SimulationController] Already running or finished.");
                return;
            }
            StartCoroutine(RunSimulation());
        }

        /// <summary>
        /// 시뮬레이션을 초기 Setup 상태로 되돌린다.
        /// </summary>
        public void Reset()
        {
            StopAllCoroutines();
            foreach (var piece in _pieces)
                piece.OnSimulationStart();

            ChangePhase(SimulationPhase.Setup);
        }

        // ─── Simulation Flow ─────────────────────────────────────────────

        private IEnumerator RunSimulation()
        {
            // 모든 기물 초기화
            foreach (var piece in _pieces)
                piece.OnSimulationStart();

            // ── Phase 1: PreSimulation ─────────────────────────────────
            ChangePhase(SimulationPhase.PreSimulation);

            var preSkills = _skills.Where(s =>
                s.Timing == SkillTiming.PreSimulation &&
                s.CanExecute(_pieces)).ToList();

            foreach (var skill in preSkills)
            {
                Debug.Log($"[Skill] {skill.SkillName} 발동");
                yield return skill.Execute(this, _pieces);
            }

            // ── Phase 2: Simulating ────────────────────────────────────
            ChangePhase(SimulationPhase.Simulating);

            var activeActions = _pieces
                .Where(p => !p.IsDead)
                .Select(p => p.ExecuteAction(_pieces))
                .ToList();

            yield return RunParallel(activeActions);

            // ── Phase 3: DuringSimulation skills ──────────────────────
            var duringSkills = _skills.Where(s =>
                s.Timing == SkillTiming.DuringSimulation &&
                s.CanExecute(_pieces)).ToList();

            foreach (var skill in duringSkills)
                yield return skill.Execute(this, _pieces);

            // ── Phase 4: Finished ──────────────────────────────────────
            ChangePhase(SimulationPhase.Finished);

            var result = EvaluateResult();
            Debug.Log($"[Simulation] 종료 - 결과: {result}");
            OnSimulationFinished?.Invoke(result);
        }

        // ─── Player Selection ────────────────────────────────────────────

        private Action<PieceBase> _selectionCallback;

        /// <summary>
        /// 플레이어가 후보 목록에서 기물을 선택할 때까지 대기한다.
        /// UI 레이어에서 NotifyPlayerSelection()을 호출해 결과를 전달한다.
        /// </summary>
        public IEnumerator WaitForPlayerSelection(IList<PieceBase> candidates, Action<PieceBase> onSelected)
        {
            _selectionCallback = onSelected;

            // 선택 UI 활성화 요청
            OnSelectionRequested?.Invoke(candidates);

            // 선택이 들어올 때까지 대기
            yield return new WaitUntil(() => _selectionCallback == null);
        }

        /// <summary>
        /// 플레이어가 기물을 선택했을 때 UI 레이어에서 호출한다.
        /// </summary>
        public void NotifyPlayerSelection(PieceBase selected)
        {
            if (_selectionCallback == null) return;

            var cb = _selectionCallback;
            _selectionCallback = null;

            OnSelectionCompleted?.Invoke();
            cb?.Invoke(selected);
        }

        // 외부(UI)에서 구독: 선택 대기 시작 시 호출됨
        public event Action<IList<PieceBase>> OnSelectionRequested;
        // 외부(UI)에서 구독: 선택 완료 시 호출됨
        public event Action OnSelectionCompleted;

        // ─── Parallel Coroutine Runner ────────────────────────────────────

        /// <summary>
        /// 여러 코루틴을 동시에 실행하고 모두 끝날 때까지 대기한다.
        /// OpeningShotSkill 등 외부 스킬에서도 사용한다.
        /// </summary>
        public IEnumerator RunParallel(IList<IEnumerator> coroutines)
        {
            int remaining = coroutines.Count;
            if (remaining == 0) yield break;

            foreach (var co in coroutines)
                StartCoroutine(WrapCoroutine(co, () => remaining--));

            yield return new WaitUntil(() => remaining <= 0);
        }

        private IEnumerator WrapCoroutine(IEnumerator co, Action onDone)
        {
            yield return StartCoroutine(co);
            onDone();
        }

        // ─── Result Evaluation ────────────────────────────────────────────

        private SimulationResult EvaluateResult()
        {
            bool allEnemiesDead = _pieces
                .Where(p => p.Faction == Faction.Enemy)
                .All(p => p.IsDead);

            bool anyCivilianDead = _pieces
                .Where(p => p.PieceType == PieceType.Civilian)
                .Any(p => p.IsDead);

            bool anyAllyDead = _pieces
                .Where(p => p.Faction == Faction.Ally)
                .Any(p => p.IsDead);

            if (!allEnemiesDead)
                return SimulationResult.Lose;

            if (anyCivilianDead && anyAllyDead)
                return SimulationResult.BothDeadWin;
            if (anyCivilianDead)
                return SimulationResult.CivilianDeadWin;
            if (anyAllyDead)
                return SimulationResult.AllyDeadWin;

            return SimulationResult.PerfectWin;
        }

        private void ChangePhase(SimulationPhase phase)
        {
            _phase = phase;
            OnPhaseChanged?.Invoke(phase);
        }
    }

    public enum SimulationResult
    {
        PerfectWin,
        AllyDeadWin,
        CivilianDeadWin,
        BothDeadWin,
        Lose,
    }
}
