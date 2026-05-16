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
    private readonly List<PieceBase> spawnedEnemyPieces = new();
    private readonly List<GameObject> spawnedObjects = new();

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
        EnsureMapRoot();
        EnsureMapInstancesLoaded();
        SetAllMapsActive(false);
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

        EnsureMapRoot();
        EnsureMapInstancesLoaded();

        if (loadedMaps.Length == 0)
        {
            currentMapIndex = -1;
            return;
        }

        currentMapIndex = Mathf.Clamp(parsedData.mapIndex, 0, loadedMaps.Length - 1);
    Debug.Log($"[StageManager] Activating map index: {currentMapIndex}", this);
        ActivateCurrentMap();

        SpawnEnemies(parsedData);
        SpawnObjects(parsedData);

        Debug.Log($"Stage parsed: mapIndex={parsedData.mapIndex}, entityCount={parsedData.entities.Length}", this);
    }

    public void EndStage()
    {
        SetAllMapsActive(false);
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
    private void EnsureMapRoot()
    {
        if (mapRoot == null)
        {
            GameObject rootObject = new GameObject("MapRoot");
            rootObject.transform.SetParent(transform, false);
            mapRoot = rootObject.transform;
        }
    }
    private void EnsureMapInstancesLoaded()
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
            ClearLoadedMaps();
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
    private void ActivateCurrentMap()
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
    private void SetAllMapsActive(bool isActive)
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

        ClearSpawnedObjects();

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
            GameObject spawnedObject = Instantiate(objectPrefab, spawnPosition, spawnRotation, objectParent);
            spawnedObject.name = objectPrefab.name;
            spawnedObjects.Add(spawnedObject);
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
    private void ClearSpawnedObjects()
    {
        foreach (GameObject spawnedObject in spawnedObjects)
        {
            if (spawnedObject != null)
            {
                Destroy(spawnedObject);
            }
        }

        spawnedObjects.Clear();
    }
    private void ClearLoadedMaps()
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
}
