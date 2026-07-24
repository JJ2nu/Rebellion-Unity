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
    // Stage JSON은 재사용 가능한 미션 정의의 안정적인 ID만 저장한다.
    // 화면 문구와 판정 규칙은 MissionDefinitionRegistry가 제공한다.
    public string missionId = MissionIds.PreserveAllies;
}

public static class MissionIds
{
    public const string EliminateAllEnemies = "eliminate_all_enemies";
    public const string PreserveAllies = "preserve_allies";
    public const string PreserveCivilians = "preserve_civilians";
    public const string PreserveEliza = "preserve_eliza";
    public const string UseOpeningShot = "use_opening_shot";
}

public enum CivilianType
{
    Civilian = 0,
    Eliza = 1,
}

[Serializable]
public class StageData
{
    // 포맷 버전. 나중에 구조가 바뀌면 업그레이드 분기 기준으로 사용
    public int version = 4;

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
        missionId = MissionIds.EliminateAllEnemies,
    };
    public StageMissionData[] subMissions = Array.Empty<StageMissionData>();

    // 오더 스킬 사용 가능 여부
    public bool hasOrder = false;

    // 스테이지에서 사용 가능한 아군 기물 종류별 슬롯 수
    public AllySlotData[] allySlots = Array.Empty<AllySlotData>();

    // 적/시민/오브젝트를 한 배열로 저장
    public StageEntityData[] entities = Array.Empty<StageEntityData>();

    // 튜토리얼에서 권장 배치를 보여주는 비상호작용 고스트 기물
    public TutorialGhostPieceData[] tutorialGhostPieces = Array.Empty<TutorialGhostPieceData>();

    public string GetStageTitle()
    {
        // 아직 이관되지 않은 Challenge 데이터도 기존 제목으로 정상 표시한다.
        return string.IsNullOrWhiteSpace(stageTitle) ? mainMission : stageTitle;
    }

    public StageMissionData GetPrimaryMission()
    {
        primaryMission ??= new StageMissionData();
        // 현재 MainMissionSlot의 진행도는 적 처치 수 전용이므로 주 미션 정책을 그대로 고정한다.
        primaryMission.missionId = MissionIds.EliminateAllEnemies;

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
public class TutorialGhostPieceData
{
    // StageManager.tutorialGhostPrefabs 배열 인덱스
    public int ghostType;

    // 0 = North, 1 = East, 2 = South, 3 = West
    public int facing;

    // 배열 저장 규칙 기준 셀 인덱스
    public int cellIndex;
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
