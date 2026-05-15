using UnityEngine;

public class GridCell : MonoBehaviour, IWorldInputTarget
{
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
        End
    }

    [SerializeField]    GameObject defaultTile;
    [SerializeField]    GameObject activeTile;
    [SerializeField]    GameObject occupiedTile;
    [SerializeField]    GameObject directionTile;
    [SerializeField]    GameObject emptyTile;
    [SerializeField]    private float offset = 1.3f;

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
    }
    public float cellSize
    {
        get
        {
            return offset;
        }
    }

    #region 3D Input Events
    public void OnWorldHover(WorldInputEventData eventData)
    {
        Debug.Log($"[WorldInput] Hover GridCell: {name}", this);
    }

    public void OnWorldUnHover(WorldInputEventData eventData)
    {
        Debug.Log($"[WorldInput] UnHover GridCell: {name}", this);
    }

    public void OnWorldLeftClick(WorldInputEventData eventData)
    {
        Debug.Log($"[WorldInput] LeftClick GridCell: {name}", this);
    }

    public void OnWorldRightClick(WorldInputEventData eventData)
    {
        Debug.Log($"[WorldInput] RightClick GridCell: {name}", this);
    }
    #endregion
}
