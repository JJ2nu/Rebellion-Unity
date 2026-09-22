using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Play 요청 순간의 배치로 계산한 전투 기록과 최종 결과다.
/// 실제 전투는 같은 Phase/Hit 기록만 피해로 승인해 사전 판정과 실행 결과를 일치시킨다.
/// </summary>
public sealed class CombatPredictionResult
{
    private readonly IReadOnlyList<CombatPredictionPhase> phases;
    private readonly IReadOnlyDictionary<PieceBase, CombatPredictionPieceState> finalStates;

    internal CombatPredictionResult(
        SimulationController.SimulationResult predictedResult,
        IReadOnlyList<CombatPredictionPhase> phases,
        IReadOnlyDictionary<PieceBase, CombatPredictionPieceState> finalStates,
        int pieceCount,
        bool openingShotExecuted)
    {
        PredictedResult = predictedResult;
        this.phases = phases ?? Array.Empty<CombatPredictionPhase>();
        this.finalStates = finalStates ??
            new Dictionary<PieceBase, CombatPredictionPieceState>();
        PieceCount = pieceCount;
        OpeningShotExecuted = openingShotExecuted;
    }

    public SimulationController.SimulationResult PredictedResult { get; }
    public bool IsPerfectWin =>
        PredictedResult == SimulationController.SimulationResult.PerfectWin;
    public int PieceCount { get; }
    public bool OpeningShotExecuted { get; }

    internal CombatPredictionPhase GetPhase(int phaseIndex)
    {
        for (int index = 0; index < phases.Count; index++)
        {
            if (phases[index].PhaseIndex == phaseIndex)
            {
                return phases[index];
            }
        }

        return null;
    }

    internal bool TryGetFinalState(
        PieceBase piece,
        out CombatPredictionPieceState state)
    {
        state = default;
        return piece != null && finalStates.TryGetValue(piece, out state);
    }
}

/// <summary>
/// 한 Phase에 동시에 성립한 피해 기록이다.
/// Source/Target 참조는 Play 요청 시점의 실제 기물을 가리킨다.
/// </summary>
internal sealed class CombatPredictionPhase
{
    public CombatPredictionPhase(int phaseIndex, IReadOnlyList<CombatPredictionHit> hits)
    {
        PhaseIndex = phaseIndex;
        Hits = hits ?? Array.Empty<CombatPredictionHit>();
    }

    public int PhaseIndex { get; }
    public IReadOnlyList<CombatPredictionHit> Hits { get; }
}

internal sealed class CombatPredictionHit
{
    public CombatPredictionHit(PieceBase source, PieceBase target, int damage)
    {
        Source = source;
        Target = target;
        Damage = damage;
    }

    public PieceBase Source { get; }
    public PieceBase Target { get; }
    public int Damage { get; }
}

internal readonly struct CombatPredictionPieceState
{
    public CombatPredictionPieceState(bool isDead, int health, int gridX, int gridY)
    {
        IsDead = isDead;
        Health = health;
        GridX = gridX;
        GridY = gridY;
    }

    public bool IsDead { get; }
    public int Health { get; }
    public int GridX { get; }
    public int GridY { get; }
}

/// <summary>
/// Unity 프레임·Animator·Physics 상태를 변경하지 않고 현재 전투 규칙을 계산한다.
/// 모든 입력은 Play 요청 순간에 복사하며 같은 입력은 같은 전투 기록을 만든다.
/// </summary>
public static class CombatPredictionResolver
{
    private const int AttackDamage = 1;

    public static CombatPredictionResult Resolve(
        IReadOnlyList<PieceBase> pieces,
        int boardSize,
        PieceBase openingShotTarget)
    {
        var states = new List<MutablePieceState>();
        if (pieces != null)
        {
            for (int index = 0; index < pieces.Count; index++)
            {
                PieceBase piece = pieces[index];
                if (piece != null)
                {
                    states.Add(new MutablePieceState(piece, index));
                }
            }
        }

        int safeBoardSize = Math.Max(1, boardSize);
        bool openingShotExecuted = ApplyOpeningShot(states, openingShotTarget);

        // 실제 SimulationController도 선처리 스킬 이후 한 번만 CanAct를 고정한다.
        foreach (MutablePieceState state in states)
        {
            state.CanAct = !state.IsDead && FindTarget(state, states) != null;
        }

        List<int> phaseIndices = states
            .Where(state => !state.IsDead && state.PhaseIndex > 0)
            .Select(state => state.PhaseIndex)
            .Distinct()
            .OrderBy(phaseIndex => phaseIndex)
            .ToList();
        var phases = new List<CombatPredictionPhase>(phaseIndices.Count);

        foreach (int phaseIndex in phaseIndices)
        {
            ResolvePhase(states, safeBoardSize, phaseIndex, phases);
        }

        SimulationController.SimulationResult result = DetermineResult(states);
        var finalStates = new Dictionary<PieceBase, CombatPredictionPieceState>(states.Count);
        foreach (MutablePieceState state in states)
        {
            finalStates[state.Source] = new CombatPredictionPieceState(
                state.IsDead,
                Math.Max(0, state.Health),
                state.GridX,
                state.GridY);
        }

        return new CombatPredictionResult(
            result,
            phases,
            finalStates,
            states.Count,
            openingShotExecuted);
    }

    private static bool ApplyOpeningShot(
        IReadOnlyList<MutablePieceState> states,
        PieceBase target)
    {
        if (target == null)
        {
            return false;
        }

        MutablePieceState targetState = states.FirstOrDefault(state =>
            state.Source == target &&
            state.Faction == Faction.Enemy &&
            !state.IsDead);
        if (targetState == null)
        {
            return false;
        }

        targetState.Health = 0;
        targetState.IsDead = true;
        return true;
    }

    private static void ResolvePhase(
        IReadOnlyList<MutablePieceState> states,
        int boardSize,
        int phaseIndex,
        ICollection<CombatPredictionPhase> phases)
    {
        List<MutablePieceState> actors = states
            .Where(state =>
                !state.IsDead &&
                state.CanAct &&
                state.PhaseIndex == phaseIndex)
            .OrderBy(state => state.SourceOrder)
            .ToList();
        var intents = new List<AttackIntent>(actors.Count);
        var gunmen = new List<MutablePieceState>();

        // 같은 Phase의 공격 대상은 피해가 발생하기 전 상태에서 모두 확정한다.
        foreach (MutablePieceState actor in actors)
        {
            // 실제 GunmanPiece.Fire는 Phase 0의 CanAct만 보고 재탐색 없이 발사한다.
            // 앞선 Phase에서 표적이 죽거나 이동해 정면 최근접이 같은 진영(적끼리 등)이어도
            // 총알은 그대로 날아가 직선 위 모든 기물을 맞히므로 여기서 다시 걸러내지 않는다.
            if (actor.PieceType == PieceType.Gunman)
            {
                gunmen.Add(actor);
                continue;
            }

            MutablePieceState target = FindTarget(actor, states);
            if (target != null)
            {
                intents.Add(new AttackIntent(actor, target));
            }
        }

        var hits = new List<CombatPredictionHit>();
        var pendingDamage = new Dictionary<MutablePieceState, int>();
        foreach (AttackIntent intent in intents)
        {
            AddHit(intent.Source, intent.Target, hits, pendingDamage);
        }

        foreach (MutablePieceState gunman in gunmen)
        {
            AddGunmanHits(gunman, states, boardSize, hits, pendingDamage);
        }

        foreach (KeyValuePair<MutablePieceState, int> damageEntry in pendingDamage)
        {
            MutablePieceState target = damageEntry.Key;
            target.Health -= damageEntry.Value;
            if (target.Health <= 0)
            {
                target.Health = 0;
                target.IsDead = true;
            }
        }

        // Slasher는 공격 중 살아남았을 때만 처음 확정한 대상 셀을 점유한다.
        foreach (AttackIntent intent in intents)
        {
            if (intent.Source.PieceType != PieceType.Slasher || intent.Source.IsDead)
            {
                continue;
            }

            intent.Source.GridX = intent.TargetGridX;
            intent.Source.GridY = intent.TargetGridY;
        }

        phases.Add(new CombatPredictionPhase(phaseIndex, hits));
    }

    private static void AddGunmanHits(
        MutablePieceState source,
        IReadOnlyList<MutablePieceState> states,
        int boardSize,
        ICollection<CombatPredictionHit> hits,
        IDictionary<MutablePieceState, int> pendingDamage)
    {
        (int deltaX, int deltaY) = GetFacingDelta(source.FacingDirection);
        int x = source.GridX + deltaX;
        int y = source.GridY + deltaY;

        // 실제 총알은 첫 대상 뒤에도 진행하므로 보드 끝까지 같은 직선의 모든 기물을 맞힌다.
        while (x >= 0 && y >= 0 && x < boardSize && y < boardSize)
        {
            foreach (MutablePieceState target in states.OrderBy(state => state.SourceOrder))
            {
                if (target == source || target.IsDead || target.GridX != x || target.GridY != y)
                {
                    continue;
                }

                AddHit(source, target, hits, pendingDamage);
            }

            x += deltaX;
            y += deltaY;
        }
    }

    private static void AddHit(
        MutablePieceState source,
        MutablePieceState target,
        ICollection<CombatPredictionHit> hits,
        IDictionary<MutablePieceState, int> pendingDamage)
    {
        hits.Add(new CombatPredictionHit(source.Source, target.Source, AttackDamage));
        pendingDamage.TryGetValue(target, out int damage);
        pendingDamage[target] = damage + AttackDamage;
    }

    private static MutablePieceState FindTarget(
        MutablePieceState source,
        IReadOnlyList<MutablePieceState> states)
    {
        (int deltaX, int deltaY) = GetFacingDelta(source.FacingDirection);
        for (int distance = 1; distance <= source.AttackRange; distance++)
        {
            int targetX = source.GridX + deltaX * distance;
            int targetY = source.GridY + deltaY * distance;
            MutablePieceState closest = states
                .Where(state =>
                    state != source &&
                    !state.IsDead &&
                    state.GridX == targetX &&
                    state.GridY == targetY)
                .OrderBy(state => state.SourceOrder)
                .FirstOrDefault();

            if (closest != null)
            {
                return IsEnemy(source.Faction, closest.Faction) ? closest : null;
            }
        }

        return null;
    }

    private static bool IsEnemy(Faction source, Faction target)
    {
        if (source == Faction.Ally)
        {
            return target == Faction.Enemy;
        }

        if (source == Faction.Enemy)
        {
            return target == Faction.Ally || target == Faction.Neutral;
        }

        return false;
    }

    private static (int deltaX, int deltaY) GetFacingDelta(Direction direction)
    {
        return direction switch
        {
            Direction.North => (0, 1),
            Direction.East => (1, 0),
            Direction.South => (0, -1),
            Direction.West => (-1, 0),
            _ => (0, 0),
        };
    }

    private static SimulationController.SimulationResult DetermineResult(
        IReadOnlyList<MutablePieceState> states)
    {
        if (states.Any(state => state.Faction == Faction.Enemy && !state.IsDead))
        {
            return SimulationController.SimulationResult.Lose;
        }

        int deadAllyCount = states.Count(state =>
            state.Faction == Faction.Ally && state.IsDead);
        if (deadAllyCount >= 3)
        {
            return SimulationController.SimulationResult.AllyDeadLose;
        }

        bool anyAllyDead = deadAllyCount > 0;
        bool anyNeutralDead = states.Any(state =>
            state.Faction == Faction.Neutral && state.IsDead);
        if (anyAllyDead && anyNeutralDead)
        {
            return SimulationController.SimulationResult.BothDeadWin;
        }

        if (anyAllyDead)
        {
            return SimulationController.SimulationResult.AllyDeadWin;
        }

        if (anyNeutralDead)
        {
            return SimulationController.SimulationResult.CivilianDeadWin;
        }

        return SimulationController.SimulationResult.PerfectWin;
    }

    private sealed class MutablePieceState
    {
        public MutablePieceState(PieceBase source, int sourceOrder)
        {
            Source = source;
            SourceOrder = sourceOrder;
            Faction = source.Faction;
            PieceType = source.PieceType;
            FacingDirection = source.FacingDirection;
            GridX = source.GridX;
            GridY = source.GridY;
            Health = Math.Max(1, source.MaxHealth);
            AttackRange = Math.Max(0, source.AttackRange);
            PhaseIndex = source.EffectivePhaseIndex;
        }

        public PieceBase Source { get; }
        public int SourceOrder { get; }
        public Faction Faction { get; }
        public PieceType PieceType { get; }
        public Direction FacingDirection { get; }
        public int AttackRange { get; }
        public int PhaseIndex { get; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public int Health { get; set; }
        public bool IsDead { get; set; }
        public bool CanAct { get; set; }
    }

    private sealed class AttackIntent
    {
        public AttackIntent(MutablePieceState source, MutablePieceState target)
        {
            Source = source;
            Target = target;
            TargetGridX = target.GridX;
            TargetGridY = target.GridY;
        }

        public MutablePieceState Source { get; }
        public MutablePieceState Target { get; }
        public int TargetGridX { get; }
        public int TargetGridY { get; }
    }
}
