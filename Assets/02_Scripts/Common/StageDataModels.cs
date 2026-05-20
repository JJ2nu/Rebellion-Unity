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
public class StageData
{
    // 포맷 버전. 나중에 구조가 바뀌면 업그레이드 분기 기준으로 사용
    public int version = 2;

    // 현재는 6x6 고정이지만 이후 확장을 위해 포함
    public int boardSize = 6;

    // 어떤 맵 프리팹을 활성화할지 결정하는 인덱스
    public int mapIndex;

    // 미션 텍스트
    public string mainMission = "";
    public string subMission1 = "";
    public string subMission2 = "";

    // 오더 스킬 사용 가능 여부
    public bool hasOrder = false;

    // 스테이지에서 사용 가능한 아군 기물 종류별 슬롯 수
    public AllySlotData[] allySlots = Array.Empty<AllySlotData>();

    // 적/시민/오브젝트를 한 배열로 저장
    public StageEntityData[] entities = Array.Empty<StageEntityData>();

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
    // Enemy 기준: 0 = Brawler, 1 = Reserved, 2 = Gunman
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
