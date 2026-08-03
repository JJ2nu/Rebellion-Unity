using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    private const string ObjectGroupTag = "ObjectGroup";
    private const float RetryRewindDuration = 0.45f;

    public static StageManager Instance { get; private set; }
    public static event System.Action<StageData> StageLoaded;
    public event Action<bool> RetryResetStateChanged;

    public StageData CurrentStageData => currentStageData;
    public string CurrentStageId => string.IsNullOrWhiteSpace(currentStagePath)
        ? string.Empty
        : Path.GetFileNameWithoutExtension(currentStagePath);
    public bool IsRetryResetting { get; private set; }

    [SerializeField] private GameObject[] allyPiecePrefabs;
    [Header("Tutorial")]
    [SerializeField] private GameObject[] tutorialGhostPrefabs = Array.Empty<GameObject>();
    [SerializeField] private GameObject[] enemyPiecePrefabs;
    [SerializeField] private GameObject[] civilianPiecePrefabs;
    [SerializeField] private GameObject[] mapPrefabs;
    [SerializeField] private GameObject hitImpactRedPrefab;
    [SerializeField] private GameObject hitImpactWhitePrefab;
    [SerializeField] private HitImpactColorMode hitImpactColorMode = HitImpactColorMode.Red;

    [Header("Map Audio")]
    [SerializeField] private MapAudioController mapAudioController;

    [Header("Combat SFX")]
    [SerializeField] private AudioSource combatSfxAudioSource;
    [SerializeField] private AudioClip[] punchAttackSfx = Array.Empty<AudioClip>();
    [SerializeField] private AudioClip[] rushAttackSfx = Array.Empty<AudioClip>();
    [SerializeField] private AudioClip[] gunReadySfx = Array.Empty<AudioClip>();
    [SerializeField] private AudioClip[] gunFireSfx = Array.Empty<AudioClip>();
    [SerializeField] private AudioClip[] hitSfx = Array.Empty<AudioClip>();
    [SerializeField] private AudioClip[] civilianHitSfx = Array.Empty<AudioClip>();

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
    private readonly List<(int detailType, PieceBase piece)> spawnedCivilianPieces = new();
    private readonly List<(int detailType, GameObject obj)> spawnedObjects = new();
    private readonly List<(int cellIndex, GameObject obj)> tutorialGhostPieces = new();
    private bool tutorialGhostPiecesVisible = true;
    [SerializeField] private int currentStageEnemyCount;
    [SerializeField] private List<int> currentStageEnemyTypeCounts = new();
    public int CurrentStageEnemyCount => currentStageEnemyCount;
    public IReadOnlyList<int> CurrentStageEnemyTypeCounts => currentStageEnemyTypeCounts;

    /// <summary>
    /// 스킬로 인해 타겟을 선택해야 하는 모드인지 여부를 나타낸다.
    /// </summary>
    public bool IsSelectionMode { get; set; }
    public int CurrentSpawnedEnemyPieceCount { get; private set; }
    public int CurrentSpawnedPieceCount { get; private set; }

    // 풀: 스테이지 클리어 전까지 보관하고 재배치


    private readonly List<Queue<PieceBase>> allyPool = new();
    private readonly List<Queue<PieceBase>> enemyPool = new();
    private readonly List<Queue<PieceBase>> civilianPool = new();
    private readonly Dictionary<int, Queue<GameObject>> objectPool = new();
    private HitImpactVfxPool hitImpactVfxPool;
    private GroundBloodDecalPool groundBloodDecalPool;

    private PieceBase[][] allyPieces;
    private PieceBase[][] enemyPieces;
    private PieceBase[][] civilianPieces;
    private Coroutine retryResetCoroutine;

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
        PrewarmAllyPool();
        EnsureHitImpactVfxPool();
        EnsureCombatSfxAudioSource();
    }

    public void LoadStage(string stagePath, bool playMapAudioImmediately = true)
    {
        currentStagePath = stagePath;
        SetCurrentStageEnemyPieceCounts(null);

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

        ClearSpawnedAllyPieces();
        ClearTutorialGhostPieces();
        currentStageData = parsedData;
        CacheStageEntities(parsedData);

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        MapRootCheck();
        MapCacheCheck();

        if (loadedMaps.Length == 0)
        {
            currentMapIndex = -1;
            mapAudioController?.Stop();
            return;
        }

        currentMapIndex = Mathf.Clamp(parsedData.mapIndex, 0, loadedMaps.Length - 1);
        if (currentMapIndex != parsedData.mapIndex)
        {
            Debug.LogWarning(
                $"Stage map index {parsedData.mapIndex} is out of range. Using {currentMapIndex} instead.",
                this);
        }

        Debug.Log($"[StageManager] Activating map index: {currentMapIndex}", this);
        UpdateActiveMap();
        if (playMapAudioImmediately)
        {
            mapAudioController?.PlayForMap(currentMapIndex);
        }
        else
        {
            mapAudioController?.PrepareForMap(currentMapIndex);
        }

        gameManager?.EnsureStageGridReady();

        SpawnEnemies(parsedData);
        SpawnCivilians(parsedData);
        SpawnObjects(parsedData);
        SpawnTutorialGhostPieces(parsedData);
        RefreshSpawnedPieceCounts();
        EnsureHitImpactPoolSize();

        //Debug.Log($"Stage parsed: mapIndex={parsedData.mapIndex}, entityCount={parsedData.entities.Length}", this);
        StageLoaded?.Invoke(currentStageData);

        SimulationController.Instance?.ResetSimulation();
    }

    /// <summary>
    /// 인트로 오디오드라마가 끝난 뒤 현재 맵에 준비된 BGM과 앰비언트를 시작한다.
    /// </summary>
    public void PlayCurrentMapAudio()
    {
        mapAudioController?.PlayPrepared();
    }

    public void EndStage()
    {
        mapAudioController?.Stop();
        SetMapsActive(false);
        SetCurrentStageEnemyPieceCounts(null);
        groundBloodDecalPool?.Clear();
        ClearTutorialGhostPieces();
        ClearPools();
        CurrentSpawnedEnemyPieceCount = 0;
        CurrentSpawnedPieceCount = 0;
    }

    public void PlayHitImpact(Vector3 position, Vector3 direction, HitImpactAttackType attackType)
    {
        EnsureHitImpactVfxPool();
        hitImpactVfxPool?.Play(position, direction, hitImpactColorMode, attackType);

        if (hitImpactColorMode == HitImpactColorMode.Red)
        {
            PlayGroundBloodDecal(position);
        }
    }

    /// <summary>
    /// 피격 VFX를 거치지 않는 처치도 일반 공격과 같은 바닥 혈흔을 남길 수 있게 한다.
    /// </summary>
    public void PlayGroundBloodDecal(Vector3 position)
    {
        EnsureGroundBloodDecalPool();
        groundBloodDecalPool?.Play(position);
    }

    public void PlayBrawlerAttackSfx()
    {
        PlayRandomCombatSfx(punchAttackSfx);
    }

    public void PlaySlasherAttackSfx()
    {
        PlayRandomCombatSfx(rushAttackSfx);
    }

    public void PlayGunReadySfx()
    {
        PlayRandomCombatSfx(gunReadySfx);
    }

    public void PlayGunFireSfx()
    {
        PlayRandomCombatSfx(gunFireSfx);
    }

    public void PlayPieceHitSfx(PieceBase hitPiece)
    {
        PlayRandomCombatSfx(hitPiece is CivilianPiece ? civilianHitSfx : hitSfx);
    }

    private void PlayRandomCombatSfx(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            return;
        }

        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        if (clip == null)
        {
            return;
        }

        EnsureCombatSfxAudioSource();
        combatSfxAudioSource?.PlayOneShot(clip);
    }

    private void EnsureCombatSfxAudioSource()
    {
        if (combatSfxAudioSource != null)
        {
            return;
        }

        combatSfxAudioSource = GetComponent<AudioSource>();
        if (combatSfxAudioSource == null)
        {
            combatSfxAudioSource = gameObject.AddComponent<AudioSource>();
        }

        combatSfxAudioSource.playOnAwake = false;
        combatSfxAudioSource.loop = false;
        combatSfxAudioSource.spatialBlend = 0f;
    }

    /// <summary>
    /// 시뮬레이션 직전 상태로 되돌린다.
    /// 아군 기물은 위치/방향을 유지한 채 상태만 초기화하고,
    /// 적/민간인은 원래 위치에 다시 스폰한다.
    /// </summary>
    public void ResetForRetry()
    {
        // 재시작 입력 즉시 이전 시뮬레이션의 바닥 핏자국을 제거한다.
        groundBloodDecalPool?.Clear();

        if (currentStageData == null) return;

        if (retryResetCoroutine != null)
        {
            StopCoroutine(retryResetCoroutine);
            retryResetCoroutine = null;
        }

        foreach (var ally in spawnedAllyPieces)
        {
            ally?._HUD?.SetActive(true);
        }
        foreach (var enemy in spawnedEnemyPieces)
        {
            enemy.piece?._HUD?.SetActive(true);
        }
        foreach (var civilian in spawnedCivilianPieces)
        {
            civilian.piece?._HUD?.SetActive(true);
        }

        SetRetryResetting(true);
        retryResetCoroutine = StartCoroutine(ResetForRetryRoutine());
    }

    public void CompleteRetryResetImmediately()
    {
        if (!IsRetryResetting && retryResetCoroutine == null)
        {
            return;
        }

        if (retryResetCoroutine != null)
        {
            StopCoroutine(retryResetCoroutine);
            retryResetCoroutine = null;
        }

        foreach (var ally in spawnedAllyPieces)
        {
            ally?.CompleteRetryRewindImmediately();
        }

        foreach (var civilian in spawnedCivilianPieces)
        {
            civilian.piece?.CompleteRetryRewindImmediately();
        }

        foreach (var enemy in spawnedEnemyPieces)
        {
            enemy.piece?.CompleteRetryRewindImmediately();
        }

        SetRetryResetting(false);
    }

    private IEnumerator ResetForRetryRoutine()
    {
        foreach (var ally in spawnedAllyPieces)
        {
            ResetPieceForRetry(ally, RetryRewindDuration);
        }

        foreach (var civilian in spawnedCivilianPieces)
        {
            ResetPieceForRetry(civilian.piece, RetryRewindDuration);
        }
        foreach (var enemy in spawnedEnemyPieces)
        {
            ResetPieceForRetry(enemy.piece, RetryRewindDuration);
        }

        yield return new WaitForSeconds(RetryRewindDuration);
        retryResetCoroutine = null;
        SetRetryResetting(false);
    }

    private void SetRetryResetting(bool isResetting)
    {
        if (IsRetryResetting == isResetting)
        {
            return;
        }

        IsRetryResetting = isResetting;
        RetryResetStateChanged?.Invoke(IsRetryResetting);
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

    public void SetTutorialGhostPiecesActive(bool isActive)
    {
        tutorialGhostPiecesVisible = isActive;
        RefreshTutorialGhostPieceVisibility();
    }

    private void RefreshTutorialGhostPieceVisibility()
    {
        for (int index = 0; index < tutorialGhostPieces.Count; index++)
        {
            (int cellIndex, GameObject ghost) = tutorialGhostPieces[index];
            if (ghost != null)
            {
                ghost.SetActive(tutorialGhostPiecesVisible && !IsCellOccupiedBySpawnedAlly(cellIndex));
            }
        }
    }

    private bool IsCellOccupiedBySpawnedAlly(int cellIndex)
    {
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
            ReturnAllyToPool(piece);
            RefreshSpawnedPieceCounts();
            EnsureHitImpactPoolSize();
            RefreshTutorialGhostPieceVisibility();
        }

        return removed;
    }
    public bool TrySpawnAllyPiece(PieceType pieceType, int cellIndex, Direction facingDirection)
    {
        if (IsCellOccupied(cellIndex))
        {
            return false;
        }

        int prefabIndex = (int)pieceType;
        if (allyPiecePrefabs == null || prefabIndex < 0 || prefabIndex >= allyPiecePrefabs.Length)
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

        PieceBase allyPiece;
        while (allyPool.Count <= prefabIndex)
            allyPool.Add(new Queue<PieceBase>());

        Queue<PieceBase> pool = allyPool[prefabIndex];
        if (pool.Count > 0)
        {
            allyPiece = pool.Dequeue();
            allyPiece.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            allyPiece.gameObject.SetActive(true);
        }
        else
        {
            GameObject allyPrefab = allyPiecePrefabs[prefabIndex];
            if (allyPrefab == null)
            {
                Debug.LogWarning($"Ally prefab is missing for PieceType: {pieceType}", this);
                return false;
            }

            GameObject allyObject = Instantiate(allyPrefab, spawnPosition, spawnRotation, transform);
            allyPiece = allyObject.GetComponent<PieceBase>();
            if (allyPiece == null)
            {
                Debug.LogWarning($"Ally prefab has no PieceBase component: {allyPrefab.name}", this);
                Destroy(allyObject);
                return false;
            }
        }

        Vector2Int gridCoord = StageGridIndexUtility.ToGridCoord(GetBoardSize(), cellIndex);
        allyPiece.GridX = gridCoord.x;
        allyPiece.GridY = gridCoord.y;
        allyPiece.FacingDirection = facingDirection;
        allyPiece.CaptureSpawnState();
        spawnedAllyPieces.Add(allyPiece);
        RefreshSpawnedPieceCounts();
        EnsureHitImpactPoolSize();
        RefreshTutorialGhostPieceVisibility();
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

    private void EnsureHitImpactVfxPool()
    {
        if (hitImpactVfxPool != null)
        {
            return;
        }

        GameObject poolObject = new GameObject("HitImpactVfxPool");
        poolObject.transform.SetParent(transform, false);
        hitImpactVfxPool = poolObject.AddComponent<HitImpactVfxPool>();
        hitImpactVfxPool.Configure(hitImpactRedPrefab, hitImpactWhitePrefab, CurrentSpawnedPieceCount, hitImpactColorMode);
    }

    private void EnsureGroundBloodDecalPool()
    {
        if (groundBloodDecalPool != null)
        {
            return;
        }

        GameObject poolObject = new GameObject("GroundBloodDecalPool");
        poolObject.transform.SetParent(transform, false);
        groundBloodDecalPool = poolObject.AddComponent<GroundBloodDecalPool>();
    }

    private void EnsureHitImpactPoolSize()
    {
        EnsureHitImpactVfxPool();
        hitImpactVfxPool?.Prewarm(CurrentSpawnedPieceCount, hitImpactColorMode);
    }

    private void RefreshSpawnedPieceCounts()
    {
        CurrentSpawnedEnemyPieceCount = 0;
        foreach ((_, PieceBase enemy) in spawnedEnemyPieces)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                CurrentSpawnedEnemyPieceCount++;
            }
        }

        CurrentSpawnedPieceCount = 0;
        foreach (PieceBase ally in spawnedAllyPieces)
        {
            if (ally != null && ally.gameObject.activeInHierarchy)
            {
                CurrentSpawnedPieceCount++;
            }
        }

        foreach ((_, PieceBase enemy) in spawnedEnemyPieces)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                CurrentSpawnedPieceCount++;
            }
        }

        foreach ((_, PieceBase civilian) in spawnedCivilianPieces)
        {
            if (civilian != null && civilian.gameObject.activeInHierarchy)
            {
                CurrentSpawnedPieceCount++;
            }
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
            //Debug.Log($"[StageManager] Map '{map.name}' active={index == currentMapIndex}", this);
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
        int[] spawnedEnemyTypeCounts = new int[Enum.GetValues(typeof(PieceType)).Length];

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
            enemyPiece.CaptureSpawnState();

            int enemyTypeIndex = enemyTypeIndices[enemyEntity.detailType]++;
            enemyPieces[enemyEntity.detailType][enemyTypeIndex] = enemyPiece;
            spawnedEnemyPieces.Add((enemyEntity.detailType, enemyPiece));

            int pieceTypeIndex = (int)enemyPiece.PieceType;
            if (pieceTypeIndex >= 0 && pieceTypeIndex < spawnedEnemyTypeCounts.Length)
            {
                spawnedEnemyTypeCounts[pieceTypeIndex]++;
            }
        }

        SetCurrentStageEnemyPieceCounts(spawnedEnemyTypeCounts);
    }

    private void SetCurrentStageEnemyPieceCounts(IReadOnlyList<int> enemyTypeCounts)
    {
        currentStageEnemyTypeCounts.Clear();
        currentStageEnemyCount = 0;

        if (enemyTypeCounts != null)
        {
            for (int index = 0; index < enemyTypeCounts.Count; index++)
            {
                int count = Mathf.Max(0, enemyTypeCounts[index]);
                currentStageEnemyTypeCounts.Add(count);
                currentStageEnemyCount += count;
            }
        }

        SimulationController.Instance?.SetStageEnemyPieceCounts(currentStageEnemyTypeCounts);
    }

    public int GetCurrentStageEnemyCount(PieceType pieceType)
    {
        int index = (int)pieceType;
        if (index < 0 || index >= currentStageEnemyTypeCounts.Count)
        {
            return 0;
        }

        return currentStageEnemyTypeCounts[index];
    }

    private void SpawnCivilians(StageData stageData)
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        PoolCivilians();

        while (civilianPool.Count < civilianPiecePrefabs.Length)
            civilianPool.Add(new Queue<PieceBase>());

        int[] civilianTypeCounts = new int[civilianPiecePrefabs.Length];
        foreach (StageEntityData civilianEntity in civilianEntities)
        {
            if (civilianEntity.detailType >= 0 && civilianEntity.detailType < civilianTypeCounts.Length)
            {
                civilianTypeCounts[civilianEntity.detailType]++;
            }
        }

        civilianPieces = new PieceBase[civilianPiecePrefabs.Length][];
        for (int index = 0; index < civilianPiecePrefabs.Length; index++)
        {
            civilianPieces[index] = new PieceBase[civilianTypeCounts[index]];
        }

        int[] civilianTypeIndices = new int[civilianPiecePrefabs.Length];

        foreach (StageEntityData civilianEntity in civilianEntities)
        {
            if (civilianEntity.detailType < 0 || civilianEntity.detailType >= civilianPiecePrefabs.Length)
            {
                Debug.LogWarning($"Civilian detailType is out of range: {civilianEntity.detailType}", this);
                continue;
            }

            GameObject civilianPrefab = civilianPiecePrefabs[civilianEntity.detailType];
            if (civilianPrefab == null)
            {
                Debug.LogWarning($"Civilian prefab is missing for detailType: {civilianEntity.detailType}", this);
                continue;
            }

            Vector3 spawnPosition = gameManager.GetCellPosition(civilianEntity.cellIndex);
            Quaternion spawnRotation = Quaternion.Euler(0f, civilianEntity.facing * 90f, 0f);

            PieceBase civilianPiece;
            Queue<PieceBase> pool = civilianPool[civilianEntity.detailType];
            if (pool.Count > 0)
            {
                civilianPiece = pool.Dequeue();
                civilianPiece.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                civilianPiece.gameObject.SetActive(true);
            }
            else
            {
                GameObject civilianObject = Instantiate(civilianPrefab, spawnPosition, spawnRotation, transform);
                civilianPiece = civilianObject.GetComponent<PieceBase>();
                if (civilianPiece == null)
                {
                    Debug.LogWarning($"Civilian prefab has no PieceBase component: {civilianPrefab.name}", this);
                    Destroy(civilianObject);
                    continue;
                }
            }

            Vector2Int gridCoord = StageGridIndexUtility.ToGridCoord(stageData.boardSize, civilianEntity.cellIndex);
            civilianPiece.GridX = gridCoord.x;
            civilianPiece.GridY = gridCoord.y;
            civilianPiece.FacingDirection = (Direction)civilianEntity.facing;
            civilianPiece.CaptureSpawnState();

            int civilianTypeIndex = civilianTypeIndices[civilianEntity.detailType]++;
            civilianPieces[civilianEntity.detailType][civilianTypeIndex] = civilianPiece;
            spawnedCivilianPieces.Add((civilianEntity.detailType, civilianPiece));
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

                if (spawnedObject == null || !string.Equals(spawnedObject.name, objectPrefab.name, StringComparison.Ordinal))
                {
                    if (spawnedObject != null)
                    {
                        Debug.LogWarning($"Discarding pooled object for detailType {objectEntity.detailType} because it does not match prefab {objectPrefab.name} on map {currentMapIndex}.", this);
                        Destroy(spawnedObject);
                    }

                    spawnedObject = Instantiate(objectPrefab, spawnPosition, spawnRotation, objectParent);
                    spawnedObject.name = objectPrefab.name;
                }
                else
                {
                    spawnedObject.transform.SetParent(objectParent);
                    spawnedObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                    spawnedObject.SetActive(true);
                }
            }
            else
            {
                spawnedObject = Instantiate(objectPrefab, spawnPosition, spawnRotation, objectParent);
                spawnedObject.name = objectPrefab.name;
            }

            spawnedObjects.Add((objectEntity.detailType, spawnedObject));
        }
    }

    private void SpawnTutorialGhostPieces(StageData stageData)
    {
        ClearTutorialGhostPieces();

        if (stageData?.tutorialGhostPieces == null || stageData.tutorialGhostPieces.Length == 0)
        {
            return;
        }

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (gameManager == null)
        {
            Debug.LogWarning("StageManager could not find GameManager for tutorial ghost pieces.", this);
            return;
        }

        for (int index = 0; index < stageData.tutorialGhostPieces.Length; index++)
        {
            TutorialGhostPieceData ghostData = stageData.tutorialGhostPieces[index];
            if (ghostData == null)
            {
                continue;
            }

            int prefabIndex = ghostData.ghostType;
            if (tutorialGhostPrefabs == null || prefabIndex < 0 || prefabIndex >= tutorialGhostPrefabs.Length)
            {
                Debug.LogWarning($"Tutorial ghost type is out of range: {ghostData.ghostType}", this);
                continue;
            }

            GameObject prefab = tutorialGhostPrefabs[prefabIndex];
            if (prefab == null)
            {
                Debug.LogWarning($"Tutorial ghost prefab is missing for ghostType: {ghostData.ghostType}", this);
                continue;
            }

            Vector3 spawnPosition = gameManager.GetCellPosition(ghostData.cellIndex);
            Quaternion spawnRotation = Quaternion.Euler(0f, ghostData.facing * 90f, 0f);
            GameObject ghost = Instantiate(prefab, spawnPosition, spawnRotation, transform);
            ghost.name = $"{prefab.name}_TutorialGhost";
            ConfigureTutorialGhostPiece(ghost);
            tutorialGhostPieces.Add((ghostData.cellIndex, ghost));
        }

        tutorialGhostPiecesVisible = true;
        RefreshTutorialGhostPieceVisibility();
    }

    private void ConfigureTutorialGhostPiece(GameObject ghost)
    {
        if (ghost == null)
        {
            return;
        }

        foreach (Collider collider in ghost.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (Rigidbody rigidbody in ghost.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
        }

        PieceBase piece = ghost.GetComponent<PieceBase>();
        if (piece != null)
        {
            piece.enabled = false;
        }

        ConfigureTutorialGhostHud(ghost);
        ConfigureTutorialGhostAnimator(ghost);
    }

    private static void ConfigureTutorialGhostHud(GameObject ghost)
    {
        Transform hudTransform = ghost.transform.Find("HUD");
        if (hudTransform == null)
        {
            return;
        }

        GameObject hud = hudTransform.gameObject;
        hud.SetActive(true);

        InGameHUDUI hudUi = hud.GetComponent<InGameHUDUI>();
        if (hudUi != null)
        {
            hudUi.InitializeGuide();
        }
    }

    private static void ConfigureTutorialGhostAnimator(GameObject ghost)
    {
        foreach (Animator animator in ghost.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                continue;
            }

            animator.enabled = true;
            animator.speed = 1f;
            animator.Rebind();
            ResetTriggerIfExists(animator, "Attack");
            ResetTriggerIfExists(animator, "Hit");
            ResetTriggerIfExists(animator, "Reset");
            ResetTriggerIfExists(animator, "Shoot1");
            ResetTriggerIfExists(animator, "Shoot2");

            int idleHash = Animator.StringToHash("Base Layer.idle");
            if (animator.HasState(0, idleHash))
            {
                animator.Play(idleHash, 0, 0f);
            }
            else
            {
                animator.Play("idle", 0, 0f);
            }

            animator.Update(0f);
        }
    }

    private static void ResetTriggerIfExists(Animator animator, string triggerName)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
            {
                animator.ResetTrigger(triggerName);
                return;
            }
        }
    }

    private void ClearTutorialGhostPieces()
    {
        for (int index = 0; index < tutorialGhostPieces.Count; index++)
        {
            GameObject ghost = tutorialGhostPieces[index].obj;
            if (ghost != null)
            {
                Destroy(ghost);
            }
        }

        tutorialGhostPieces.Clear();
        tutorialGhostPiecesVisible = true;
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
    private void PoolCivilians()
    {
        foreach ((int detailType, PieceBase civilian) in spawnedCivilianPieces)
        {
            if (civilian == null) continue;
            civilian.gameObject.SetActive(false);
            while (civilianPool.Count <= detailType)
                civilianPool.Add(new Queue<PieceBase>());
            civilianPool[detailType].Enqueue(civilian);
        }
        spawnedCivilianPieces.Clear();
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
        hitImpactVfxPool?.Clear();

        ClearSpawnedAllyPieces();

        foreach (Queue<PieceBase> pool in allyPool)
        {
            while (pool.Count > 0)
            {
                PieceBase pooled = pool.Dequeue();
                if (pooled != null) Destroy(pooled.gameObject);
            }
        }
        allyPool.Clear();

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
        enemyPool.Clear();

        foreach ((int _, PieceBase civilian) in spawnedCivilianPieces)
        {
            if (civilian != null) Destroy(civilian.gameObject);
        }
        spawnedCivilianPieces.Clear();

        foreach (Queue<PieceBase> pool in civilianPool)
        {
            while (pool.Count > 0)
            {
                PieceBase pooled = pool.Dequeue();
                if (pooled != null) Destroy(pooled.gameObject);
            }
        }
        civilianPool.Clear();

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

    private void ClearSpawnedAllyPieces()
    {
        for (int index = 0; index < spawnedAllyPieces.Count; index++)
        {
            PieceBase allyPiece = spawnedAllyPieces[index];
            if (allyPiece != null)
            {
                ReturnAllyToPool(allyPiece);
            }
        }
        spawnedAllyPieces.Clear();
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

    public IReadOnlyList<PieceBase> GetAllActivePieces()
    {
        var result = new List<PieceBase>();

        foreach (var piece in spawnedAllyPieces)
            if (piece != null && piece.gameObject.activeInHierarchy && !piece.IsDead)
                result.Add(piece);

        foreach ((_, var piece) in spawnedEnemyPieces)
            if (piece != null && piece.gameObject.activeInHierarchy && !piece.IsDead)
                result.Add(piece);

        foreach ((_, var piece) in spawnedCivilianPieces)
            if (piece != null && piece.gameObject.activeInHierarchy && !piece.IsDead)
                result.Add(piece);

        return result.AsReadOnly();
    }
    public IReadOnlyList<PieceBase> GetAllPieces()
    {
        var result = new List<PieceBase>();

        foreach (var piece in spawnedAllyPieces)
            if (piece != null && piece.gameObject.activeInHierarchy)
                result.Add(piece);

        foreach ((_, var piece) in spawnedEnemyPieces)
            if (piece != null && piece.gameObject.activeInHierarchy)
                result.Add(piece);

        foreach ((_, var piece) in spawnedCivilianPieces)
            if (piece != null && piece.gameObject.activeInHierarchy)
                result.Add(piece);

        return result.AsReadOnly();
    }

    public int GetDeadCivilianCount(CivilianType civilianType)
    {
        int detailType = (int)civilianType;
        int deadCount = 0;

        // Neutral 진영 하나로 합쳐진 일반 민간인과 엘리자를 Stage 데이터의 타입으로 구분한다.
        foreach ((int spawnedDetailType, PieceBase piece) in spawnedCivilianPieces)
        {
            if (spawnedDetailType == detailType && piece != null && piece.IsDead)
            {
                deadCount++;
            }
        }

        return deadCount;
    }

    private void PrewarmAllyPool()
    {
        if (allyPiecePrefabs == null)
        {
            return;
        }

        while (allyPool.Count < allyPiecePrefabs.Length)
            allyPool.Add(new Queue<PieceBase>());

        for (int index = 0; index < allyPiecePrefabs.Length; index++)
        {
            GameObject prefab = allyPiecePrefabs[index];
            if (prefab == null)
            {
                continue;
            }

            GameObject instance = Instantiate(prefab, transform);
            PieceBase piece = instance.GetComponent<PieceBase>();
            if (piece == null)
            {
                Debug.LogWarning($"Ally prefab has no PieceBase component: {prefab.name}", this);
                Destroy(instance);
                continue;
            }

            instance.SetActive(false);
            allyPool[index].Enqueue(piece);
        }
    }

    private void ReturnAllyToPool(PieceBase piece)
    {
        if (piece == null)
        {
            return;
        }

        piece.gameObject.SetActive(false);

        int prefabIndex = (int)piece.PieceType;
        while (allyPool.Count <= prefabIndex)
            allyPool.Add(new Queue<PieceBase>());

        allyPool[prefabIndex].Enqueue(piece);
    }

    private static void ResetPieceForRetry(PieceBase piece, float duration)
    {
        if (piece == null)
        {
            return;
        }

        piece.gameObject.SetActive(true);
        piece.StartRetryRewind(duration);
    }

    public void SetAttackRange(int[] cellIndices)
    {
        foreach (int idx in cellIndices)
        {
            var coord = StageGridIndexUtility.ToGridCoord(GetBoardSize(), idx);
            foreach (var piece in GetAllActivePieces())
            {
                if (piece.GridX == coord.x && piece.GridY == coord.y)
                {
                    piece._isInRange = true;
                }
            }
        }
    }
    public void ClearAttackRange()
    {
        foreach (var piece in GetAllActivePieces())
        {
            piece._isInRange = false;
        }
    }
}
