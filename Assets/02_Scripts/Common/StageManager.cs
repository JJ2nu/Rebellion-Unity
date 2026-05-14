using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Rebellion;

public class StageManager : MonoBehaviour
{
    [SerializeField] private GameObject[] allyPiecePrefabs;
    [SerializeField] private GameObject[] enemyPiecePrefabs;

    private GameManager gameManager;
    private string currentStagePath;

    // 마지막으로 읽어온 원본 스테이지 데이터
    private StageData currentStageData;

    // 파싱 결과를 종류별로 분류해 둔 엔트리 목록
    private readonly List<StageEntityData> enemyEntities = new();
    private readonly List<StageEntityData> civilianEntities = new();
    private readonly List<StageEntityData> objectEntities = new();

    private readonly List<PieceBase> spawnedEnemyPieces = new();

    private PieceBase[][] allyPieces;
    private PieceBase[][] enemyPieces;
    private PieceBase[] civilianPieces;

    private void Awake()
    {
        gameManager = GameManager.Instance;
    }

    public void SetStagePath(string path)
    {
        currentStagePath = path;
    }

    public void ParseStageData()
    {
        if (string.IsNullOrWhiteSpace(currentStagePath))
        {
            Debug.LogWarning("StageManager has no stage path assigned.", this);
            return;
        }

        string fullPath = GetStageFilePath(currentStagePath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"Stage file not found: {fullPath}", this);
            return;
        }

        string json = File.ReadAllText(fullPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning($"Stage file is empty: {fullPath}", this);
            return;
        }

        StageData parsedData = JsonUtility.FromJson<StageData>(json);
        if (parsedData == null)
        {
            Debug.LogWarning($"Failed to parse stage file: {fullPath}", this);
            return;
        }

        currentStageData = parsedData;
        RebuildEntityBuckets(parsedData);
        SpawnEnemies(parsedData);

        Debug.Log($"Stage parsed: mapIndex={parsedData.mapIndex}, entityCount={parsedData.entities.Length}", this);
    }

    public IReadOnlyList<StageEntityData> GetEnemyEntities()
    {
        return enemyEntities;
    }

    public IReadOnlyList<StageEntityData> GetCivilianEntities()
    {
        return civilianEntities;
    }

    public IReadOnlyList<StageEntityData> GetObjectEntities()
    {
        return objectEntities;
    }

    public StageData GetCurrentStageData()
    {
        return currentStageData;
    }

    private string GetStageFilePath(string stagePath)
    {
        if (Path.IsPathRooted(stagePath))
        {
            return stagePath;
        }

        return Path.Combine(Application.streamingAssetsPath, stagePath);
    }

    private void RebuildEntityBuckets(StageData stageData)
    {
        enemyEntities.Clear();
        civilianEntities.Clear();
        objectEntities.Clear();

        if (stageData.entities == null)
        {
            stageData.entities = Array.Empty<StageEntityData>();
            return;
        }

        foreach (StageEntityData entity in stageData.entities)
        {
            if (entity == null)
            {
                continue;
            }

            switch (entity.entityKind)
            {
                case 0:
                    enemyEntities.Add(entity);
                    break;

                case 1:
                    civilianEntities.Add(entity);
                    break;

                case 2:
                    objectEntities.Add(entity);
                    break;

                default:
                    Debug.LogWarning($"Unknown entityKind: {entity.entityKind}", this);
                    break;
            }
        }
    }

    private void SpawnEnemies(StageData stageData)
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (gameManager == null)
        {
            Debug.LogWarning("StageManager could not find GameManager.", this);
            return;
        }

        ClearSpawnedEnemies();

        int[] enemyTypeCounts = new int[enemyPiecePrefabs.Length];
        foreach (StageEntityData enemyEntity in enemyEntities)
        {
            if (enemyEntity.detailType >= 0 && enemyEntity.detailType < enemyTypeCounts.Length)
            {
                enemyTypeCounts[enemyEntity.detailType]++;
            }
        }

        enemyPieces = new PieceBase[enemyPiecePrefabs.Length][];
        for (int index = 0; index < enemyPiecePrefabs.Length; index++)
        {
            enemyPieces[index] = new PieceBase[enemyTypeCounts[index]];
        }

        int[] enemyTypeIndices = new int[enemyPiecePrefabs.Length];

        foreach (StageEntityData enemyEntity in enemyEntities)
        {
            if (enemyEntity.detailType < 0 || enemyEntity.detailType >= enemyPiecePrefabs.Length)
            {
                Debug.LogWarning($"Enemy detailType is out of range: {enemyEntity.detailType}", this);
                continue;
            }

            GameObject enemyPrefab = enemyPiecePrefabs[enemyEntity.detailType];
            if (enemyPrefab == null)
            {
                Debug.LogWarning($"Enemy prefab is missing for detailType: {enemyEntity.detailType}", this);
                continue;
            }

            Vector3 spawnPosition = gameManager.GetCellPosition(enemyEntity.cellIndex);
            Quaternion spawnRotation = Quaternion.Euler(0f, enemyEntity.facing * 90f, 0f);
            GameObject enemyObject = Instantiate(enemyPrefab, spawnPosition, spawnRotation, transform);
            PieceBase enemyPiece = enemyObject.GetComponent<PieceBase>();
            if (enemyPiece == null)
            {
                Debug.LogWarning($"Enemy prefab has no PieceBase component: {enemyPrefab.name}", this);
                Destroy(enemyObject);
                continue;
            }

            Vector2Int gridCoord = StageGridIndexUtility.ToGridCoord(stageData.boardSize, enemyEntity.cellIndex);
            enemyPiece.GridX = gridCoord.x;
            enemyPiece.GridY = gridCoord.y;
            enemyPiece.FacingDirection = (Direction)enemyEntity.facing;

            int enemyTypeIndex = enemyTypeIndices[enemyEntity.detailType]++;
            enemyPieces[enemyEntity.detailType][enemyTypeIndex] = enemyPiece;
            spawnedEnemyPieces.Add(enemyPiece);
        }
    }

    private void ClearSpawnedEnemies()
    {
        foreach (PieceBase spawnedEnemyPiece in spawnedEnemyPieces)
        {
            if (spawnedEnemyPiece != null)
            {
                Destroy(spawnedEnemyPiece.gameObject);
            }
        }

        spawnedEnemyPieces.Clear();
    }
}
