using System;
using UnityEngine;

[Serializable]
public class AllySlotData
{
    // PieceType enum 값 (0=Brawler, 1=Slasher, 2=Gunman)
    public int pieceType;
    public int count;
}

[Serializable]
public enum MissionType
{
    EliminateAllEnemies = 0,
    PreserveAllies = 1,
    PreserveCivilians = 2,
    PreserveEliza = 3,
    UseOpeningShot = 4,
}

[Serializable]
public class StageMissionData
{
    // 화면 문구와 판정 종류를 분리해 문구 수정이나 현지화가 미션 규칙을 바꾸지 않게 한다.
    public MissionType type = MissionType.PreserveAllies;
    public string text = "";
}

public enum CivilianType
{
    Civilian = 0,
    Eliza = 1,
}

public readonly struct SimulationMissionFacts
{
    public SimulationMissionFacts(
        int totalEnemyCount,
        int deadEnemyCount,
        int deadAllyCount,
        int deadCivilianCount,
        int deadElizaCount,
        bool openingShotExecuted)
    {
        TotalEnemyCount = totalEnemyCount;
        DeadEnemyCount = deadEnemyCount;
        DeadAllyCount = deadAllyCount;
        DeadCivilianCount = deadCivilianCount;
        DeadElizaCount = deadElizaCount;
        OpeningShotExecuted = openingShotExecuted;
    }

    public int TotalEnemyCount { get; }
    public int DeadEnemyCount { get; }
    public int DeadAllyCount { get; }
    public int DeadCivilianCount { get; }
    public int DeadElizaCount { get; }
    public bool OpeningShotExecuted { get; }
}

[Serializable]
public class StageData
{
    // 포맷 버전. 나중에 구조가 바뀌면 업그레이드 분기 기준으로 사용
    public int version = 3;

    // 현재는 6x6 고정이지만 이후 확장을 위해 포함
    public int boardSize = 6;

    // 어떤 맵 프리팹을 활성화할지 결정하는 인덱스
    public int mapIndex;

    // version 2 이하 파일을 읽기 위한 기존 필드다. 새 파일은 stageTitle과 명시적인 미션 데이터를 사용한다.
    public string mainMission = "";
    public string subMission1 = "";
    public string subMission2 = "";

    // 메인 슬롯 상단에는 스테이지 제목을, 결과 줄에는 주 미션을 표시한다.
    public string stageTitle = "";
    public StageMissionData primaryMission = new()
    {
        type = MissionType.EliminateAllEnemies,
        text = "모든 적 처치",
    };
    public StageMissionData[] subMissions = Array.Empty<StageMissionData>();

    // 오더 스킬 사용 가능 여부
    public bool hasOrder = false;

    // 스테이지에서 사용 가능한 아군 기물 종류별 슬롯 수
    public AllySlotData[] allySlots = Array.Empty<AllySlotData>();

    // 적/시민/오브젝트를 한 배열로 저장
    public StageEntityData[] entities = Array.Empty<StageEntityData>();

    public string GetStageTitle()
    {
        // 아직 이관되지 않은 Challenge 데이터도 기존 제목으로 정상 표시한다.
        return string.IsNullOrWhiteSpace(stageTitle) ? mainMission : stageTitle;
    }

    public StageMissionData GetPrimaryMission()
    {
        primaryMission ??= new StageMissionData();
        primaryMission.type = MissionType.EliminateAllEnemies;
        if (string.IsNullOrWhiteSpace(primaryMission.text))
        {
            primaryMission.text = "모든 적 처치";
        }

        return primaryMission;
    }

    public StageMissionData[] GetSubMissions()
    {
        subMissions ??= Array.Empty<StageMissionData>();
        return subMissions;
    }

    public int GetAllyCount(PieceType type)
    {
        if (allySlots == null)
        {
            return 0;
        }

        int typeInt = (int)type;
        for (int i = 0; i < allySlots.Length; i++)
        {
            if (allySlots[i] != null && allySlots[i].pieceType == typeInt)
            {
                return allySlots[i].count;
            }
        }

        return 0;
    }
}

[Serializable]
public class StageEntityData
{
    // 0 = Enemy, 1 = Civilian, 2 = Object
    public int entityKind;

    // Enemy일 때 공격 방식 ID, Object일 때 오브젝트 종류 ID
    // Enemy 기준: 0 = Brawler, 1 = Reserved, 2 = Gunman, 3 = Boss
    public int detailType;

    // 0 = North, 1 = East, 2 = South, 3 = West
    public int facing;

    // 배열 저장 규칙 기준 셀 인덱스
    // index 0 = x 최대, z 최소
    // x 감소 우선으로 증가, 한 줄이 끝나면 z 증가
    public int cellIndex;
}

public static class StageGridIndexUtility
{
    // index 0 = x 최대, z 최소 규칙으로 좌표를 셀 인덱스로 변환
    public static int ToCellIndex(int boardSize, int x, int z)
    {
        return z * boardSize + (boardSize - 1 - x);
    }

    // 셀 인덱스를 논리 그리드 좌표로 변환
    public static Vector2Int ToGridCoord(int boardSize, int cellIndex)
    {
        int z = cellIndex / boardSize;
        int xOrder = cellIndex % boardSize;
        int x = boardSize - 1 - xOrder;
        return new Vector2Int(x, z);
    }
}
