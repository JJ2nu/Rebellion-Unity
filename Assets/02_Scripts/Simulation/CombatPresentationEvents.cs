using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// 전투 판정 결과를 변경하지 않고 Presentation 계층이 관찰할 수 있는 기물 상태 스냅샷이다.
/// </summary>
public readonly struct CombatPieceSnapshot
{
    public PieceBase Piece { get; }
    public PieceType PieceType { get; }
    public Faction Faction { get; }
    public int GridX { get; }
    public int GridY { get; }
    public Direction FacingDirection { get; }
    public int CurrentHealth { get; }
    public bool IsDead { get; }
    public Vector3 Position { get; }

    public CombatPieceSnapshot(PieceBase piece)
    {
        Piece = piece;
        PieceType = piece != null ? piece.PieceType : default;
        Faction = piece != null ? piece.Faction : default;
        GridX = piece != null ? piece.GridX : 0;
        GridY = piece != null ? piece.GridY : 0;
        FacingDirection = piece != null ? piece.FacingDirection : default;
        CurrentHealth = piece != null ? piece.CurrentHealth : 0;
        IsDead = piece != null && piece.IsDead;
        Position = piece != null ? piece.transform.position : Vector3.zero;
    }
}

/// <summary>
/// 시뮬레이션 시작 시점의 읽기 전용 Presentation 문맥이다.
/// </summary>
public readonly struct CombatSimulationContext
{
    public int RunId { get; }
    public IReadOnlyList<CombatPieceSnapshot> Pieces { get; }

    public CombatSimulationContext(int runId, IReadOnlyList<PieceBase> pieces)
    {
        RunId = runId;
        Pieces = CombatPresentationSnapshotUtility.CreatePieceSnapshots(pieces);
    }
}

/// <summary>
/// 한 기물 종류 페이즈의 시작 또는 종료 문맥이다.
/// </summary>
public readonly struct CombatPhaseContext
{
    public int RunId { get; }
    public int PhaseIndex { get; }
    public IReadOnlyList<CombatPieceSnapshot> ActivePieces { get; }

    public CombatPhaseContext(int runId, int phaseIndex, IReadOnlyList<PieceBase> activePieces)
    {
        RunId = runId;
        PhaseIndex = phaseIndex;
        ActivePieces = CombatPresentationSnapshotUtility.CreatePieceSnapshots(activePieces);
    }
}

/// <summary>
/// 한 기물이 행동을 시작하기 직전의 문맥이다.
/// </summary>
public readonly struct CombatAttackContext
{
    public int RunId { get; }
    public int PhaseIndex { get; }
    public CombatPieceSnapshot Attacker { get; }
    public CombatPieceSnapshot Target { get; }
    public bool HasTarget { get; }

    public CombatAttackContext(int runId, int phaseIndex, PieceBase attacker, PieceBase target)
    {
        RunId = runId;
        PhaseIndex = phaseIndex;
        Attacker = new CombatPieceSnapshot(attacker);
        Target = new CombatPieceSnapshot(target);
        HasTarget = target != null;
    }
}

/// <summary>
/// 실제 피격 판정 직전의 정확한 충돌 데이터다. 이 이벤트는 게임 상태를 수정하지 않는다.
/// </summary>
public readonly struct CombatHitContext
{
    public int RunId { get; }
    public int PhaseIndex { get; }
    public CombatPieceSnapshot Attacker { get; }
    public CombatPieceSnapshot Victim { get; }
    public Vector3 HitPoint { get; }
    public Vector3 ImpactDirection { get; }
    public HitImpactAttackType AttackType { get; }
    public int Damage { get; }
    public bool IsLethal { get; }

    public CombatHitContext(
        int runId,
        int phaseIndex,
        PieceBase attacker,
        PieceBase victim,
        Vector3 hitPoint,
        Vector3 impactDirection,
        HitImpactAttackType attackType,
        int damage,
        bool isLethal)
    {
        RunId = runId;
        PhaseIndex = phaseIndex;
        Attacker = new CombatPieceSnapshot(attacker);
        Victim = new CombatPieceSnapshot(victim);
        HitPoint = hitPoint;
        ImpactDirection = impactDirection;
        AttackType = attackType;
        Damage = damage;
        IsLethal = isLethal;
    }
}

/// <summary>
/// GunmanPiece가 총알을 초기화하고 실제 비행을 시작한 뒤 전달하는 Presentation 전용 문맥이다.
/// </summary>
public readonly struct CombatProjectileSpawnedContext
{
    public int RunId { get; }
    public int PhaseIndex { get; }
    public CombatPieceSnapshot Shooter { get; }
    public BulletController Projectile { get; }
    public Transform ProjectileTransform { get; }

    public CombatProjectileSpawnedContext(
        int runId,
        int phaseIndex,
        PieceBase shooter,
        BulletController projectile)
    {
        RunId = runId;
        PhaseIndex = phaseIndex;
        Shooter = new CombatPieceSnapshot(shooter);
        Projectile = projectile;
        ProjectileTransform = projectile != null ? projectile.transform : null;
    }
}

/// <summary>
/// PieceBase.OnDied가 실제 사망을 확정한 뒤 전달하는 Presentation 문맥이다.
/// </summary>
public readonly struct CombatPieceDiedContext
{
    public int RunId { get; }
    public int PhaseIndex { get; }
    public CombatPieceSnapshot Piece { get; }

    public CombatPieceDiedContext(int runId, int phaseIndex, PieceBase piece)
    {
        RunId = runId;
        PhaseIndex = phaseIndex;
        Piece = new CombatPieceSnapshot(piece);
    }
}

/// <summary>
/// 모든 판정과 총알 정리가 끝난 뒤의 최종 Presentation 문맥이다.
/// </summary>
public readonly struct CombatSimulationFinishedContext
{
    public int RunId { get; }
    public SimulationController.SimulationResult Result { get; }
    public IReadOnlyList<CombatPieceSnapshot> Pieces { get; }

    public CombatSimulationFinishedContext(
        int runId,
        SimulationController.SimulationResult result,
        IReadOnlyList<PieceBase> pieces)
    {
        RunId = runId;
        Result = result;
        Pieces = CombatPresentationSnapshotUtility.CreatePieceSnapshots(pieces);
    }
}

internal static class CombatPresentationSnapshotUtility
{
    public static IReadOnlyList<CombatPieceSnapshot> CreatePieceSnapshots(IReadOnlyList<PieceBase> pieces)
    {
        if (pieces == null || pieces.Count == 0)
        {
            return Array.Empty<CombatPieceSnapshot>();
        }

        var snapshots = new CombatPieceSnapshot[pieces.Count];
        for (int index = 0; index < pieces.Count; index++)
        {
            snapshots[index] = new CombatPieceSnapshot(pieces[index]);
        }

        return new ReadOnlyCollection<CombatPieceSnapshot>(snapshots);
    }
}
