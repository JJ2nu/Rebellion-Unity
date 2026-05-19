using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Rebellion;

public class StageManager : MonoBehaviour
{
    private const string ObjectGroupTag = "ObjectGroup";

    public static StageManager Instance { get; private set; }

    [SerializeField] private GameObject[] allyPiecePrefabs;
    [SerializeField] private GameObject[] enemyPiecePrefabs;
    [SerializeField] private GameObject[] mapPrefabs;

    [SerializeField] private GameObject[][] objectPrefabs;

    private GameManager gameManager;
    private string currentStagePath;
    private Transform mapRoot;
    private GameObject[] loadedMaps = Array.Empty<GameObject>();
    private int currentMapIndex = -1;

    // 마지막으로 읽어온 원본 스테이지 데이터
    private StageData currentStageData;

    // 파싱 결과를 종류별로 분류해 둔 엔트리 목록
    private readonly List<StageEntityData> enemyEntities = new();
    private readonly List<StageEntityData> civilianEntities = new();
    private readonly List<StageEntityData> objectEntities = new();
    private readonly List<PieceBase> spawnedAllyPieces = new();
    private readonly List<(int detailType, PieceBase piece)> spawnedEnemyPieces = new();
    private readonly List<(int detailType, GameObject obj)> spawnedObjects = new();

    // 풀: 스테이지 클리어 전까지 보관하고 재배치
    private readonly List<Queue<PieceBase>> enemyPool = new();
    private readonly Dictionary<int, Queue<GameObject>> objectPool = new();

    private PieceBase[][] allyPieces;
    private PieceBase[][] enemyPieces;
    private PieceBase[] civilianPieces;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        gameManager = GameManager.Instance;
        MapRootCheck();
        MapCacheCheck();
        SetMapsActive(false);
    }

    public void LoadStage(string stagePath)
    {
        currentStagePath = stagePath;

        Debug.Log($"[StageManager] LoadStage called: {currentStagePath}", this);

        if (string.IsNullOrWhiteSpace(currentStagePath))
        {
            Debug.LogWarning("StageManager has no stage path assigned.", this);
            return;
        }

        string fullPath = ResolveStagePath(currentStagePath);
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
        CacheStageEntities(parsedData);

        MapRootCheck();
        MapCacheCheck();

        if (loadedMaps.Length == 0)
        {
            currentMapIndex = -1;
            return;
        }

        currentMapIndex = Mathf.Clamp(parsedData.mapIndex, 0, loadedMaps.Length - 1);
        Debug.Log($"[StageManager] Activating map index: {currentMapIndex}", this);
        UpdateActiveMap();

        SpawnEnemies(parsedData);
        SpawnObjects(parsedData);

        Debug.Log($"Stage parsed: mapIndex={parsedData.mapIndex}, entityCount={parsedData.entities.Length}", this);
    }

    public void EndStage()
    {
        SetMapsActive(false);
        ClearPools();
    }

    public GameObject GetAllyPiecePrefab(PieceType pieceType)
    {
        int prefabIndex = (int)pieceType;
        if (allyPiecePrefabs == null || prefabIndex < 0 || prefabIndex >= allyPiecePrefabs.Length)
        {
            Debug.LogWarning($"Ally prefab is out of range. PieceType: {pieceType}", this);
            return null;
        }

        return allyPiecePrefabs[prefabIndex];
    }

    public bool IsCellOccupied(int cellIndex)
    {
        if (cellIndex < 0)
        {
            return true;
        }

        for (int index = 0; index < spawnedAllyPieces.Count; index++)
        {
            PieceBase allyPiece = spawnedAllyPieces[index];
            if (allyPiece == null || !allyPiece.gameObject.activeInHierarchy)
            {
                continue;
            }

            int allyCellIndex = StageGridIndexUtility.ToCellIndex(GetBoardSize(), allyPiece.GridX, allyPiece.GridY);
            if (allyCellIndex == cellIndex)
            {
                return true;
            }
        }

        if (currentStageData?.entities == null)
        {
            return false;
        }

        for (int index = 0; index < currentStageData.entities.Length; index++)
        {
            StageEntityData entity = currentStageData.entities[index];
            if (entity != null && entity.cellIndex == cellIndex)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryRemoveAllyPiece(PieceBase piece)
    {
        if (piece == null)
        {
            return false;
        }

        bool removed = spawnedAllyPieces.Remove(piece);
        if (removed)
        {
            Destroy(piece.gameObject);
        }

        return removed;
    }

    
public bool TrySpawnAllyPiece(PieceType pieceType, int cellIndex, Direction facingDirection)
    {
        if (IsCellOccupied(cellIndex))
        {
            return false;
        }

        GameObject allyPrefab = GetAllyPiecePrefab(pieceType);
        if (allyPrefab == null)
        {
            return false;
        }

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (gameManager == null)
        {
            Debug.LogWarning("StageManager could not find GameManager.", this);
            return false;
        }

        Vector3 spawnPosition = gameManager.GetCellPosition(cellIndex);
        Quaternion spawnRotation = Quaternion.Euler(0f, (int)facingDirection * 90f, 0f);
        GameObject allyObject = Instantiate(allyPrefab, spawnPosition, spawnRotation, transform);
        PieceBase allyPiece = allyObject.GetComponent<PieceBase>();
        if (allyPiece == null)
        {
            Debug.LogWarning($"Ally prefab has no PieceBase component: {allyPrefab.name}", this);
            Destroy(allyObject);
            return false;
        }

        Vector2Int gridCoord = StageGridIndexUtility.ToGridCoord(GetBoardSize(), cellIndex);
        allyPiece.GridX = gridCoord.x;
        allyPiece.GridY = gridCoord.y;
        allyPiece.FacingDirection = facingDirection;
        spawnedAllyPieces.Add(allyPiece);
        return true;
    }

    private string ResolveStagePath(string stagePath)
    {
        if (Path.IsPathRooted(stagePath))
        {
            return stagePath;
        }

        return Path.Combine(Application.streamingAssetsPath, stagePath);
    }

    private void CacheStageEntities(StageData stageData)
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
    private void MapRootCheck()
    {
        if (mapRoot == null)
        {
            GameObject rootObject = new GameObject("MapRoot");
            rootObject.transform.SetParent(transform, false);
            mapRoot = rootObject.transform;
        }
    }
    private void MapCacheCheck()
    {
        if (mapPrefabs == null || mapPrefabs.Length == 0)
        {
            Debug.LogWarning("StageManager has no map prefabs assigned.", this);
            loadedMaps = Array.Empty<GameObject>();
            objectPrefabs = Array.Empty<GameObject[]>();
            return;
        }

        if (loadedMaps == null || loadedMaps.Length != mapPrefabs.Length)
        {
            ClearMaps();
            loadedMaps = new GameObject[mapPrefabs.Length];
            objectPrefabs = new GameObject[mapPrefabs.Length][];
        }

        bool hasMissingMap = Array.Exists(loadedMaps, map => map == null);
        bool hasMissingObjectPrefabGroup = objectPrefabs == null || objectPrefabs.Length != mapPrefabs.Length || Array.Exists(objectPrefabs, prefabGroup => prefabGroup == null);
        if (!hasMissingMap && !hasMissingObjectPrefabGroup)
        {
            return;
        }

        for (int index = 0; index < mapPrefabs.Length; index++)
        {
            GameObject mapPrefab = mapPrefabs[index];
            if (mapPrefab == null)
            {
                objectPrefabs[index] = Array.Empty<GameObject>();
                continue;
            }

            if (loadedMaps[index] == null)
            {
                GameObject mapInstance = Instantiate(mapPrefab, mapRoot);

                mapInstance.name = mapPrefab.name;
                DontDestroyOnLoad(mapInstance);
                loadedMaps[index] = mapInstance;
            }

            if (objectPrefabs[index] == null)
            {
                Transform objectGroup = null;
                Queue<Transform> pendingTransforms = new Queue<Transform>();
                pendingTransforms.Enqueue(mapPrefab.transform);

                while (pendingTransforms.Count > 0)
                {
                    Transform currentTransform = pendingTransforms.Dequeue();
                    if (currentTransform == null)
                    {
                        continue;
                    }

                    if (currentTransform.CompareTag(ObjectGroupTag))
                    {
                        objectGroup = currentTransform;
                        break;
                    }

                    for (int childIndex = 0; childIndex < currentTransform.childCount; childIndex++)
                    {
                        pendingTransforms.Enqueue(currentTransform.GetChild(childIndex));
                    }
                }

                if (objectGroup == null)
                {
                    objectPrefabs[index] = Array.Empty<GameObject>();
                    Debug.LogWarning($"Map prefab has no ObjectGroup child: {mapPrefab.name}", this);
                    continue;
                }

                List<GameObject> collectedObjects = new List<GameObject>();
                for (int childIndex = 0; childIndex < objectGroup.childCount; childIndex++)
                {
                    Transform objectTransform = objectGroup.GetChild(childIndex);
                    if (objectTransform == null)
                    {
                        continue;
                    }

                    collectedObjects.Add(objectTransform.gameObject);
                }

                objectPrefabs[index] = collectedObjects.ToArray();
            }
        }
    }
    private void UpdateActiveMap()
    {
        for (int index = 0; index < loadedMaps.Length; index++)
        {
            GameObject map = loadedMaps[index];
            if (map == null)
            {
                continue;
            }

            map.SetActive(index == currentMapIndex);
            Debug.Log($"[StageManager] Map '{map.name}' active={index == currentMapIndex}", this);
        }
    }
    private void SetMapsActive(bool isActive)
    {
        if (loadedMaps == null)
        {
            return;
        }

        foreach (GameObject map in loadedMaps)
        {
            if (map == null)
            {
                continue;
            }

            map.SetActive(isActive);
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

        PoolEnemies();

        while (enemyPool.Count < enemyPiecePrefabs.Length)
            enemyPool.Add(new Queue<PieceBase>());

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

            PieceBase enemyPiece;
            Queue<PieceBase> pool = enemyPool[enemyEntity.detailType];
            if (pool.Count > 0)
            {
                enemyPiece = pool.Dequeue();
                enemyPiece.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                enemyPiece.gameObject.SetActive(true);
            }
            else
            {
                GameObject enemyObject = Instantiate(enemyPrefab, spawnPosition, spawnRotation, transform);
                enemyPiece = enemyObject.GetComponent<PieceBase>();
                if (enemyPiece == null)
                {
                    Debug.LogWarning($"Enemy prefab has no PieceBase component: {enemyPrefab.name}", this);
                    Destroy(enemyObject);
                    continue;
                }
            }

            Vector2Int gridCoord = StageGridIndexUtility.ToGridCoord(stageData.boardSize, enemyEntity.cellIndex);
            enemyPiece.GridX = gridCoord.x;
            enemyPiece.GridY = gridCoord.y;
            enemyPiece.FacingDirection = (Direction)enemyEntity.facing;

            int enemyTypeIndex = enemyTypeIndices[enemyEntity.detailType]++;
            enemyPieces[enemyEntity.detailType][enemyTypeIndex] = enemyPiece;
            spawnedEnemyPieces.Add((enemyEntity.detailType, enemyPiece));
        }
    }
private void SpawnObjects(StageData stageData)
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

        PoolObjects();

        if (currentMapIndex < 0 || objectPrefabs == null || currentMapIndex >= objectPrefabs.Length)
        {
            return;
        }

        GameObject[] currentMapObjectPrefabs = objectPrefabs[currentMapIndex];
        if (currentMapObjectPrefabs == null || currentMapObjectPrefabs.Length == 0)
        {
            return;
        }

        Transform objectParent = loadedMaps != null && currentMapIndex >= 0 && currentMapIndex < loadedMaps.Length && loadedMaps[currentMapIndex] != null
            ? loadedMaps[currentMapIndex].transform
            : transform;

        foreach (StageEntityData objectEntity in objectEntities)
        {
            if (objectEntity.detailType < 0 || objectEntity.detailType >= currentMapObjectPrefabs.Length)
            {
                Debug.LogWarning($"Object detailType is out of range for map {currentMapIndex}: {objectEntity.detailType}", this);
                continue;
            }

            GameObject objectPrefab = currentMapObjectPrefabs[objectEntity.detailType];
            if (objectPrefab == null)
            {
                Debug.LogWarning($"Object prefab is missing for detailType: {objectEntity.detailType}", this);
                continue;
            }

            Vector3 spawnPosition = gameManager.GetCellPosition(objectEntity.cellIndex);
            Quaternion spawnRotation = Quaternion.Euler(0f, objectEntity.facing * 90f, 0f);

            GameObject spawnedObject;
            if (objectPool.TryGetValue(objectEntity.detailType, out Queue<GameObject> pool) && pool.Count > 0)
            {
                spawnedObject = pool.Dequeue();
                spawnedObject.transform.SetParent(objectParent);
                spawnedObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                spawnedObject.SetActive(true);
            }
            else
            {
                spawnedObject = Instantiate(objectPrefab, spawnPosition, spawnRotation, objectParent);
                spawnedObject.name = objectPrefab.name;
            }

            spawnedObjects.Add((objectEntity.detailType, spawnedObject));
        }
    }
    private void PoolEnemies()
    {
        foreach ((int detailType, PieceBase enemy) in spawnedEnemyPieces)
        {
            if (enemy == null) continue;
            enemy.gameObject.SetActive(false);
            while (enemyPool.Count <= detailType)
                enemyPool.Add(new Queue<PieceBase>());
            enemyPool[detailType].Enqueue(enemy);
        }
        spawnedEnemyPieces.Clear();
    }

    private void PoolObjects()
    {
        foreach ((int detailType, GameObject obj) in spawnedObjects)
        {
            if (obj == null) continue;
            obj.SetActive(false);
            if (!objectPool.ContainsKey(detailType))
            {
                objectPool[detailType] = new Queue<GameObject>();
            }
            objectPool[detailType].Enqueue(obj);
        }
        spawnedObjects.Clear();
    }

    private void ClearPools()
    {
        for (int index = 0; index < spawnedAllyPieces.Count; index++)
        {
            PieceBase allyPiece = spawnedAllyPieces[index];
            if (allyPiece != null)
            {
                Destroy(allyPiece.gameObject);
            }
        }
        spawnedAllyPieces.Clear();

        foreach ((int _, PieceBase enemy) in spawnedEnemyPieces)
        {
            if (enemy != null) Destroy(enemy.gameObject);
        }
        spawnedEnemyPieces.Clear();

        foreach (Queue<PieceBase> pool in enemyPool)
        {
            while (pool.Count > 0)
            {
                PieceBase pooled = pool.Dequeue();
                if (pooled != null) Destroy(pooled.gameObject);
            }
        }

        foreach ((int _, GameObject obj) in spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedObjects.Clear();

        foreach (Queue<GameObject> pool in objectPool.Values)
        {
            while (pool.Count > 0)
            {
                GameObject pooled = pool.Dequeue();
                if (pooled != null) Destroy(pooled);
            }
        }
        objectPool.Clear();
    }
    private void ClearMaps()
    {
        if (loadedMaps == null)
        {
            return;
        }

        foreach (GameObject loadedMap in loadedMaps)
        {
            if (loadedMap != null)
            {
                Destroy(loadedMap);
            }
        }
    }

    private int GetBoardSize()
    {
        return currentStageData != null && currentStageData.boardSize > 0
            ? currentStageData.boardSize
            : 6;
    }
}
