using UnityEngine;

public class GridCell : MonoBehaviour, IWorldInputTarget
{
    private PlacementController placementController;

    enum TileDirection
    {
        Up,
        Down,
        Left,
        Right,
        End
    }
    enum TileState
    {
        Default,
        Active,
        Occupied,
        Direction,
        Empty,
        Clear,
        End
    }

    [SerializeField]    GameObject defaultTile;
    [SerializeField]    GameObject activeTile;
    [SerializeField]    GameObject occupiedTile;
    [SerializeField]    GameObject directionTile;
    [SerializeField]    GameObject emptyTile;
    [SerializeField]    private float offset = 1.3f;

    public int CellIndex { get; private set; } = -1;
    public int BoardSize { get; private set; }

    private bool isDirty = false;
    private TileState _tileState;
    private TileState tileState
    {
        get
        {
            return _tileState;
        }
        set
        {
            isDirty = true;
            _tileState = value;
        }
    }
    private TileDirection tileDirection { get; set; }

    void Start()
    {
        tileState = TileState.Default;
        tileDirection = TileDirection.Up;
        ApplyVisualState();
    }
    public float cellSize
    {
        get
        {
            return offset;
        }
    }

    public void Initialize(int cellIndex, int boardSize)
    {
        CellIndex = cellIndex;
        BoardSize = boardSize;
    }

    public void ShowPlacementAvailability(bool canPlace)
    {
        tileState = canPlace ? TileState.Active : TileState.Occupied;
        ApplyVisualState();
    }

    public void ShowRangeHighlight(bool show)
    {
        tileState = show ? TileState.Active : TileState.Default;
        ApplyVisualState();
    }
    public void ShowMoveHighlight(bool show, Quaternion pieceRotation)
    {
        tileState = show ? TileState.Direction : TileState.Default;
        transform.rotation = pieceRotation;
        ApplyVisualState();
    }
    public void ClearTile()
    {
        tileState = TileState.Clear;
        ApplyVisualState();
    }

    public void ResetVisual()
    {
        tileState = TileState.Default;
        ApplyVisualState();
    }

    #region 3D Input Events
public void OnWorldHover(WorldInputEventData eventData)
    {
        TryGetPlacementController()?.HandleCellHover(this);
    }

    public void OnWorldUnHover(WorldInputEventData eventData)
    {
        TryGetPlacementController()?.HandleCellUnhover(this);
    }

    public void OnWorldLeftClick(WorldInputEventData eventData)
    {
        TryGetPlacementController()?.HandleCellLeftClick(this);
    }

    public void OnWorldRightClick(WorldInputEventData eventData)
    {
        TryGetPlacementController()?.HandleCellRightClick(this);
    }
    #endregion

    private void ApplyVisualState()
    {
        SetTileActive(defaultTile, tileState == TileState.Default);
        SetTileActive(activeTile, tileState == TileState.Active);
        SetTileActive(occupiedTile, tileState == TileState.Occupied);
        SetTileActive(directionTile, tileState == TileState.Direction);
        SetTileActive(emptyTile, tileState == TileState.Empty);
    }

    private PlacementController TryGetPlacementController()
    {
        if (placementController == null)
        {
        placementController = FindObjectOfType<PlacementController>();
        }

        return placementController;
    }

    private static void SetTileActive(GameObject tile, bool isActive)
    {
        if (tile != null)
        {
            tile.SetActive(isActive);
        }
    }
}
