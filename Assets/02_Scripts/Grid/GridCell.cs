using UnityEngine;

public class GridCell : MonoBehaviour
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

    [SerializeField]
    GameObject defaultTile;
    [SerializeField]
    GameObject activeTile;
    [SerializeField]
    GameObject occupiedTile;
    [SerializeField]
    GameObject directionTile;
    [SerializeField]
    GameObject emptyTile;
    [SerializeField]
    private float offset = 1.3f;

    private bool isDirty = false;
    private TileState tileState
    {
        get
        {
            return tileState;
        }
        set
        {
            isDirty = true;
            tileState = value;
        }
    }
    private TileDirection tileDirection { get; set; }

    void Start()
    {
        tileState = TileState.Default;
        tileDirection = TileDirection.Up;
    }

}
