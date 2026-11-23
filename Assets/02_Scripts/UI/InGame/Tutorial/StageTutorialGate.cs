using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대사형 튜토리얼이 배치·시작 입력을 단계별로 제한할 때 쓰는 정적 게이트다.
/// 튜토리얼이 없는 스테이지에서는 IsActive가 false라 모든 검사가 기존 동작을 그대로 통과시킨다.
/// </summary>
public static class StageTutorialGate
{
    private static readonly HashSet<int> lockedCells = new();

    public static bool IsActive { get; private set; }

    // null이면 모든 슬롯을 허용한다.
    public static PieceType? AllowedSlotType { get; private set; }

    // 튜토리얼 대사가 진행되는 동안 모든 슬롯 선택을 막는다.
    public static bool AreAllSlotsLocked { get; private set; }

    public static bool IsSimulationStartLocked { get; private set; }

    // 강제 배치 단계 동안 이미 놓인 기물을 회수·재배치하지 못하게 한다.
    public static bool IsPieceRemovalLocked { get; private set; }

    public static bool HasRequiredPlacement { get; private set; }
    public static PieceType RequiredPieceType { get; private set; }
    public static int RequiredCellIndex { get; private set; } = -1;
    // null이면 방향은 자유다.
    public static Direction? RequiredFacing { get; private set; }

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        // Domain Reload를 끈 Editor 실행에서도 이전 Play Mode의 잠금과 구독자를 넘기지 않는다.
        ClearState();
        Changed = null;
    }

    public static void Begin()
    {
        ClearState();
        IsActive = true;
        IsSimulationStartLocked = true;
        NotifyChanged();
    }

    public static void End()
    {
        if (!IsActive)
        {
            return;
        }

        ClearState();
        NotifyChanged();
    }

    public static void SetAllowedSlotType(PieceType? pieceType)
    {
        AllowedSlotType = pieceType;
        NotifyChanged();
    }

    public static void SetAllSlotsLocked(bool isLocked)
    {
        if (AreAllSlotsLocked == isLocked)
        {
            return;
        }

        AreAllSlotsLocked = isLocked;
        NotifyChanged();
    }

    public static void SetSimulationStartLocked(bool isLocked)
    {
        IsSimulationStartLocked = isLocked;
        NotifyChanged();
    }

    public static void SetPieceRemovalLocked(bool isLocked)
    {
        IsPieceRemovalLocked = isLocked;
        NotifyChanged();
    }

    public static void SetRequiredPlacement(PieceType pieceType, int cellIndex, Direction? facing)
    {
        HasRequiredPlacement = true;
        RequiredPieceType = pieceType;
        RequiredCellIndex = cellIndex;
        RequiredFacing = facing;
        NotifyChanged();
    }

    public static void ClearRequiredPlacement()
    {
        HasRequiredPlacement = false;
        RequiredCellIndex = -1;
        RequiredFacing = null;
        NotifyChanged();
    }

    public static void LockCell(int cellIndex)
    {
        if (lockedCells.Add(cellIndex))
        {
            NotifyChanged();
        }
    }

    public static bool IsSlotAllowed(PieceType pieceType)
    {
        if (!IsActive)
        {
            return true;
        }

        return !AreAllSlotsLocked && (AllowedSlotType == null || AllowedSlotType.Value == pieceType);
    }

    /// <summary>현재 단계 규칙상 이 칸·방향으로 해당 기물을 놓을 수 있는지 돌려준다.</summary>
    public static bool CanPlace(PieceType pieceType, int cellIndex, Direction facing)
    {
        if (!IsActive || !HasRequiredPlacement || pieceType != RequiredPieceType)
        {
            return true;
        }

        return cellIndex == RequiredCellIndex &&
               (RequiredFacing == null || RequiredFacing.Value == facing);
    }

    /// <summary>강제 배치 칸에 놓인 기물이나 강제 단계 중의 기물은 회수할 수 없다.</summary>
    public static bool CanRemovePieceAt(int cellIndex)
    {
        return !IsActive || (!IsPieceRemovalLocked && !lockedCells.Contains(cellIndex));
    }

    private static void ClearState()
    {
        IsActive = false;
        AllowedSlotType = null;
        AreAllSlotsLocked = false;
        IsSimulationStartLocked = false;
        IsPieceRemovalLocked = false;
        HasRequiredPlacement = false;
        RequiredCellIndex = -1;
        RequiredFacing = null;
        lockedCells.Clear();
    }

    private static void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
