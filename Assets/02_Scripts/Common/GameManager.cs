using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Bindings")]
    // 그리드 셀을 생성할 때 사용할 원본 프리팹
    [SerializeField] private GameObject gridCellPrefab;

    // 맵을 최초 1회 생성할 때 사용할 원본 프리팹 목록
    [SerializeField] private GameObject[] mapPrefabs = Array.Empty<GameObject>();

    // 생성된 셀들을 정리해서 담아둘 부모 루트
    private Transform gridCellRoot;

    // 생성된 맵들을 정리해서 담아둘 부모 루트
    private Transform mapRoot;

    [Header("Stage")]
    // 자동 시작 시 처음 활성화할 맵 인덱스
    [SerializeField] private int initialMapIndex;

    // 게임 시작과 동시에 첫 스테이지를 열지 여부
    [SerializeField] private bool autoStartFirstStage = true;

    [Header("Grid")]
    // 한 변에 배치할 셀 개수
    [SerializeField] private int gridSize = 6;

    // 셀을 생성할 높이값
    [SerializeField] private float gridYPosition = 0.1f;

    // 어디서든 접근할 수 있도록 유지하는 싱글톤 인스턴스
    public static GameManager Instance { get; private set; }

    // 실제로 생성해서 재사용 중인 셀 인스턴스 목록
    private GameObject[] loadedGridCells = Array.Empty<GameObject>();

    // 실제로 생성해서 재사용 중인 맵 인스턴스 목록
    private GameObject[] loadedMaps = Array.Empty<GameObject>();

    // 현재 활성화해야 하는 맵 인덱스
    private int currentMapIndex;

    //현재 스테이지 정보
    private string currentStagePath;

    [SerializeField] private SimulationController simulationController;
    [SerializeField] private StageManager stageManager;


    // 싱글톤 보장, DontDestroy 설정, 초기 캐시 준비를 담당
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureRoots();
        EnsureMapInstancesLoaded();
        EnsureGridCellsLoaded();
        SetAllCellsActive(false);
        SetAllMapsActive(false);
    }

    // 옵션에 따라 첫 스테이지를 자동으로 시작
    private void Start()
    {
        if (autoStartFirstStage)
        {
            StartStage(initialMapIndex);
        }
    }

    // 스테이지 시작 시 필요한 셀/맵을 준비하고 현재 맵만 활성화
    public void StartStage(int mapIndex)
    {
        EnsureRoots();
        EnsureMapInstancesLoaded();
        EnsureGridCellsLoaded();

        if (loadedMaps.Length == 0)
        {
            currentMapIndex = -1;
            SetAllCellsActive(true);
            return;
        }

        currentMapIndex = Mathf.Clamp(mapIndex, 0, loadedMaps.Length - 1);

        SetAllCellsActive(true);
        ActivateCurrentMap();
    }

    // 스테이지 종료 시 셀과 맵을 전부 비활성화
    public void EndStage()
    {
        SetAllCellsActive(false);
        SetAllMapsActive(false);
    }

    // 셀 루트와 맵 루트가 없으면 런타임에 자동 생성
    private void EnsureRoots()
    {
        if (gridCellRoot == null)
        {
            GameObject rootObject = new GameObject("GridCellRoot");
            rootObject.transform.SetParent(transform, false);
            gridCellRoot = rootObject.transform;
        }

        if (mapRoot == null)
        {
            GameObject rootObject = new GameObject("MapRoot");
            rootObject.transform.SetParent(transform, false);
            mapRoot = rootObject.transform;
        }
    }

    // 셀 인스턴스가 비어 있거나 일부 없으면 생성해서 채움
    private void EnsureGridCellsLoaded()
    {
        if (gridCellPrefab == null)
        {
            Debug.LogWarning("GameManager has no grid cell prefab assigned.", this);
            loadedGridCells = Array.Empty<GameObject>();
            return;
        }

        int targetCellCount = Mathf.Max(0, gridSize * gridSize);
        if (loadedGridCells == null || loadedGridCells.Length != targetCellCount)
        {
            ClearLoadedObjects(loadedGridCells);
            loadedGridCells = new GameObject[targetCellCount];
        }

        if (targetCellCount == 0)
        {
            return;
        }

        if (!Array.Exists(loadedGridCells, cell => cell == null))
        {
            return;
        }

        float halfCellSize = gridCellPrefab.GetComponent<GridCell>().cellSize / 2f;
        float originOffset = gridSize - 1;

        // 인덱스 0은 x 최대, z 최소부터 시작하고 x 감소를 우선으로 저장
        for (int z = 0; z < gridSize; z++)
        {
            for (int xOrder = 0; xOrder < gridSize; xOrder++)
            {
                int x = gridSize - 1 - xOrder;
                int index = z * gridSize + xOrder;

                if (loadedGridCells[index] != null)
                {
                    continue;
                }

                float xPosition = (x * 2f - originOffset) * halfCellSize;
                float zPosition = (z * 2f - originOffset) * halfCellSize;
                GameObject cell = Instantiate(gridCellPrefab, new Vector3(xPosition, gridYPosition, zPosition), Quaternion.identity, gridCellRoot);
                cell.name = $"GridCell_{index}";
                DontDestroyOnLoad(cell);
                loadedGridCells[index] = cell;
            }
        }
    }

    // 맵 인스턴스가 비어 있거나 일부 없으면 생성해서 채움
    private void EnsureMapInstancesLoaded()
    {
        if (mapPrefabs == null || mapPrefabs.Length == 0)
        {
            Debug.LogWarning("GameManager has no map prefabs assigned.", this);
            loadedMaps = Array.Empty<GameObject>();
            return;
        }

        if (loadedMaps == null || loadedMaps.Length != mapPrefabs.Length)
        {
            ClearLoadedObjects(loadedMaps);
            loadedMaps = new GameObject[mapPrefabs.Length];
        }

        bool hasMissingMap = Array.Exists(loadedMaps, map => map == null);
        if (!hasMissingMap)
        {
            return;
        }

        for (int index = 0; index < mapPrefabs.Length; index++)
        {
            if (loadedMaps[index] != null)
            {
                continue;
            }

            GameObject mapPrefab = mapPrefabs[index];
            if (mapPrefab == null)
            {
                continue;
            }

            GameObject mapInstance = Instantiate(mapPrefab, mapRoot);
            mapInstance.name = mapPrefab.name;
            DontDestroyOnLoad(mapInstance);
            loadedMaps[index] = mapInstance;
        }
    }

    // 현재 맵 인덱스에 해당하는 맵만 켜고 나머지는 끔
    private void ActivateCurrentMap()
    {
        if (loadedMaps == null)
        {
            return;
        }

        for (int index = 0; index < loadedMaps.Length; index++)
        {
            GameObject map = loadedMaps[index];
            if (map == null)
            {
                continue;
            }

            map.SetActive(index == currentMapIndex);
        }
    }

    // 로드된 모든 셀 인스턴스의 활성 상태를 한 번에 변경
    private void SetAllCellsActive(bool isActive)
    {
        if (loadedGridCells == null)
        {
            return;
        }

        foreach (GameObject cell in loadedGridCells)
        {
            if (cell == null)
            {
                continue;
            }

            cell.SetActive(isActive);
        }
    }

    // 로드된 모든 맵 인스턴스의 활성 상태를 한 번에 변경
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

    // 다시 생성이 필요할 때 기존 인스턴스를 정리
    private void ClearLoadedObjects(GameObject[] loadedObjects)
    {
        if (loadedObjects == null)
        {
            return;
        }

        foreach (GameObject loadedObject in loadedObjects)
        {
            if (loadedObject != null)
            {
                Destroy(loadedObject);
            }
        }
    }

    public Vector3 GetCellPosition(int index)
    {
        EnsureGridCellsLoaded();

        if (loadedGridCells == null || index < 0 || index >= loadedGridCells.Length || loadedGridCells[index] == null)
        {
            Debug.LogWarning($"Invalid cell index: {index}", this);
            return Vector3.zero;
        }

        return loadedGridCells[index].transform.position;
    }
}
