using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Bindings")]
    // 그리드 셀을 생성할 때 사용할 원본 프리팹
    [SerializeField] private GameObject gridCellPrefab;

    // 생성된 셀들을 정리해서 담아둘 부모 루트
    private Transform gridCellRoot;

    [Header("Stage")]
    // 자동 시작 시 처음 불러올 스테이지 경로
    [SerializeField] private string initialStagePath = "Stages/stage_001.json";

    // 게임 시작과 동시에 첫 스테이지를 열지 여부
    [SerializeField] private bool autoStartFirstStage = true;

    // Stage Scene 단독 실행 시 선택한 Stage부터 Dialogue와 다음 Stage 흐름까지 이어서 확인할지 여부
    [SerializeField] private bool autoStartCampaignFlow = true;

    [Header("Grid")]
    // 한 변에 배치할 셀 개수
    [SerializeField] private int gridSize = 6;

    // 셀을 생성할 높이값
    [SerializeField] private float gridYPosition = 0.1f;

    // 어디서든 접근할 수 있도록 유지하는 싱글톤 인스턴스
    public static GameManager Instance { get; private set; }

    // 자동 Stage 로드가 끝난 뒤 Binder가 실제 Stage ID로 캠페인 상태를 준비할 수 있게 알린다.
    public event Action<string> InitialStageCampaignStartRequested;

    // 실제로 생성해서 재사용 중인 셀 인스턴스 목록
    private GameObject[] loadedGridCells = Array.Empty<GameObject>();
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
        EnsureGridCellsLoaded();
        SetAllCellsActive(false);
    }

    // 옵션에 따라 첫 스테이지를 자동으로 시작
    private void Start()
    {
        if (!autoStartFirstStage || string.IsNullOrWhiteSpace(initialStagePath))
        {
            return;
        }

        if (GameFlowManager.HasActiveCampaign)
        {
            // Title Campaign은 GameFlowManager가 인트로 오디오를 보류한 단일 LoadStage 경로를 사용한다.
            return;
        }

        LoadStage(initialStagePath);

        if (autoStartCampaignFlow)
        {
            RequestInitialStageCampaignStart();
        }
    }

    private void RequestInitialStageCampaignStart()
    {
        if (stageManager == null || stageManager.CurrentStageData == null)
        {
            Debug.LogWarning("[GameManager] Cannot start campaign flow because the initial Stage did not load.", this);
            return;
        }

        string stageId = stageManager.CurrentStageId;
        if (string.IsNullOrWhiteSpace(stageId))
        {
            Debug.LogWarning("[GameManager] Cannot start campaign flow because the initial Stage ID is empty.", this);
            return;
        }

        InitialStageCampaignStartRequested?.Invoke(stageId);
    }

    public void LoadStage(string stagePath, bool playMapAudioImmediately = true)
    {
        EnsureStageGridReady();

        if (stageManager == null)
        {
            stageManager = StageManager.Instance;
        }

        if (stageManager == null)
        {
            Debug.LogWarning("GameManager has no StageManager assigned.", this);
            return;
        }

        stageManager.LoadStage(stagePath, playMapAudioImmediately);
    }

    public void PlayCurrentMapAudio()
    {
        if (stageManager == null)
        {
            stageManager = StageManager.Instance;
        }

        stageManager?.PlayCurrentMapAudio();
    }

    public void EnsureStageGridReady()
    {
        EnsureRoots();
        EnsureGridCellsLoaded();
        SetAllCellsActive(true);
        ResetAllTile();
    }

    // 스테이지 종료 시 셀과 맵을 전부 비활성화
    public void EndStage()
    {
        SetAllCellsActive(false);

        if (stageManager != null)
        {
            stageManager.EndStage();
        }
    }

    // 셀 루트가 없으면 런타임에 자동 생성
    private void EnsureRoots()
    {
        if (gridCellRoot == null)
        {
            GameObject rootObject = new GameObject("GridCellRoot");
            rootObject.transform.SetParent(transform, false);
            gridCellRoot = rootObject.transform;
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
                GridCell gridCell = cell.GetComponent<GridCell>();
                if (gridCell != null)
                {
                    gridCell.Initialize(index, gridSize);
                }
                loadedGridCells[index] = cell;
            }
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

    public void ShowCellRangeHighlight(int[] cellIndices)
    {
        if (loadedGridCells == null) return;

        foreach (int idx in cellIndices)
        {
            if (idx >= 0 && idx < loadedGridCells.Length && loadedGridCells[idx] != null)
                loadedGridCells[idx].GetComponent<GridCell>()?.ShowRangeHighlight(true);
        }
    }
    public void ShowMoveRangeHighlight(int[] cellIndices, Quaternion pieceRotation)
    {
        if (loadedGridCells == null) return;

        foreach (int idx in cellIndices)
        {
            if (idx >= 0 && idx < loadedGridCells.Length && loadedGridCells[idx] != null)
                loadedGridCells[idx].GetComponent<GridCell>()?.ShowMoveHighlight(true, pieceRotation);
        }
    }

    public void ClearAllRangeHighlights()
    {
        if (loadedGridCells == null) return;

        foreach (var cell in loadedGridCells)
            if (cell != null)
                cell.GetComponent<GridCell>()?.ShowRangeHighlight(false);
    }
    public void ClearAllTile()
    {
        if(loadedGridCells == null) return;
        foreach (var cell in loadedGridCells)
            if (cell != null)
                cell.GetComponent<GridCell>()?.ClearTile();
    }
    public void ResetAllTile()
    {
        if (loadedGridCells == null) return;
        foreach (var cell in loadedGridCells)
            if (cell != null)
                cell.GetComponent<GridCell>()?.ResetVisual();
    }
}

