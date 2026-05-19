using Rebellion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlacementController : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private StageManager stageManager;

    [Header("Preview")]
    [SerializeField] private float previewHeight = 0.2f;
    [SerializeField] private Direction defaultFacingDirection = Direction.North;

    [Header("Input")]
    [SerializeField] private InputActionReference rotateAction;

    private InGameUnitStorageSlotUI pendingSlot;
    private GameObject previewObject;
    private Direction currentFacingDirection;
    private readonly Dictionary<PieceType, InGameUnitStorageSlotUI> slotMap = new();

    public bool IsPlacing => pendingSlot != null;

    private void OnDestroy()
    {
        PieceBase.AllyRightClicked -= HandleAllyPieceRightClick;

        if (rotateAction?.action != null)
        {
            rotateAction.action.performed -= OnRotatePerformed;
            rotateAction.action.Disable();
        }
    }

    
private void Awake()
    {
        ResolveDependencies();

        PieceBase.AllyRightClicked += HandleAllyPieceRightClick;

        if (rotateAction != null)
        {
            rotateAction.action.Enable();
            rotateAction.action.performed += OnRotatePerformed;
        }
    }

private void Update()
    {
        if (!IsPlacing)
        {
            return;
        }

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelPlacement();
        }
    }

public void BeginPlacement(InGameUnitStorageSlotUI slot)
    {
        if (slot == null || slot.RemainingDeployableCount <= 0)
        {
            return;
        }

        ResolveDependencies();

        ClearPreview();

        currentFacingDirection = defaultFacingDirection;
        pendingSlot = slot;
        CreatePreview(slot.UnitType);
    }

    public void CancelPlacement()
    {
        pendingSlot = null;
        ClearPreview();
    }

private void OnRotatePerformed(InputAction.CallbackContext context)
    {
        if (!IsPlacing)
        {
            return;
        }

        currentFacingDirection = (Direction)(((int)currentFacingDirection + 1) % 4);

        if (previewObject != null)
        {
            previewObject.transform.rotation = Quaternion.Euler(0f, (int)currentFacingDirection * 90f, 0f);
        }
    }


public void HandleCellHover(GridCell cell)
    {
        if (!IsPlacing || cell == null)
        {
            return;
        }

        cell.ShowPlacementAvailability(CanPlaceOn(cell));

        if (previewObject != null)
        {
            previewObject.transform.SetPositionAndRotation(
                cell.transform.position + Vector3.up * previewHeight,
                Quaternion.Euler(0f, (int)currentFacingDirection * 90f, 0f));
        }
    }

    public void HandleCellUnhover(GridCell cell)
    {
        if (cell == null)
        {
            return;
        }

        cell.ResetVisual();
    }

public void HandleCellLeftClick(GridCell cell)
    {
        if (!IsPlacing || cell == null)
        {
            return;
        }

        if (!CanPlaceOn(cell))
        {
            return;
        }

        bool isSpawned = stageManager != null &&
                         stageManager.TrySpawnAllyPiece(
                             pendingSlot.UnitType,
                             cell.CellIndex,
                             currentFacingDirection);

        if (!isSpawned)
        {
            return;
        }

        pendingSlot.TryConsumeOne();
        pendingSlot = null;
        cell.ResetVisual();
        ClearPreview();
    }

    public void HandleCellRightClick(GridCell cell)
    {
        if (!IsPlacing)
        {
            return;
        }

        if (cell != null)
        {
            cell.ResetVisual();
        }

        CancelPlacement();
    }

public void HandleAllyPieceRightClick(PieceBase piece)
    {
        if (piece == null)
        {
            return;
        }

        ResolveDependencies();

        if (stageManager == null)
        {
            return;
        }

        if (IsPlacing)
        {
            CancelPlacement();
        }

        PieceType pieceType = piece.PieceType;

        if (!stageManager.TryRemoveAllyPiece(piece))
        {
            return;
        }

        if (slotMap.TryGetValue(pieceType, out InGameUnitStorageSlotUI slot))
        {
            slot.TryRestoreOne();
        }
    }

    public void RegisterSlot(InGameUnitStorageSlotUI slot)
    {
        if (slot != null)
        {
            slotMap[slot.UnitType] = slot;
        }
    }


    private bool CanPlaceOn(GridCell cell)
    {
        ResolveDependencies();

        if (cell == null || stageManager == null)
        {
            return false;
        }

        return !stageManager.IsCellOccupied(cell.CellIndex);
    }

    private void CreatePreview(PieceType pieceType)
    {
        ResolveDependencies();

        if (stageManager == null)
        {
            return;
        }

        GameObject piecePrefab = stageManager.GetAllyPiecePrefab(pieceType);
        if (piecePrefab == null)
        {
            return;
        }

        previewObject = Instantiate(piecePrefab);
        previewObject.name = $"{piecePrefab.name}_PlacementPreview";
        SetLayerRecursively(previewObject, LayerMask.NameToLayer("Ignore Raycast"));

        foreach (Collider collider in previewObject.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        PieceBase piece = previewObject.GetComponent<PieceBase>();
        if (piece != null)
        {
            piece.enabled = false;
        }
    }



    private void ClearPreview()
    {
        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
        }
    }

private void ResolveDependencies()
    {
        if (stageManager == null)
        {
            stageManager = StageManager.Instance;
        }
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null || layer < 0)
        {
            return;
        }

        target.layer = layer;
        Transform targetTransform = target.transform;
        for (int index = 0; index < targetTransform.childCount; index++)
        {
            SetLayerRecursively(targetTransform.GetChild(index).gameObject, layer);
        }
    }
}
