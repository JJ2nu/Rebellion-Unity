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

    private bool isCellHovered;
    private float gridPlaneY;
    private Camera mainCamera;

    public bool IsPlacing => pendingSlot != null;

    private void OnDestroy()
    {
        PieceBase.AllyRightClicked -= HandleAllyPieceRightClick;
        PieceBase.AllyLeftClicked  -= HandleAllyPieceLeftClick;

        if (rotateAction?.action != null)
        {
            rotateAction.action.performed -= OnRotatePerformed;
            rotateAction.action.Disable();
        }
    }

    
private void Awake()
    {
        ResolveDependencies();
        mainCamera = Camera.main;

        PieceBase.AllyRightClicked += HandleAllyPieceRightClick;
        PieceBase.AllyLeftClicked  += HandleAllyPieceLeftClick;

        if (rotateAction?.action != null)
        {
            rotateAction.action.Enable();
            rotateAction.action.performed += OnRotatePerformed;
        }
        else
        {
            Debug.LogWarning("PlacementController: rotateAction is not assigned or does not reference a valid InputAction.", this);
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
            return;
        }

        if (!isCellHovered)
        {
            MovePreviewToMouseOnGridPlane();
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
        slot.TryConsumeOne();
        CreatePreview(slot.UnitType);
    }

    public void CancelPlacement()
    {
        if (pendingSlot != null)
        {
            pendingSlot.TryRestoreOne();
        }

        pendingSlot = null;
        isCellHovered = false;
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

        isCellHovered = true;
        gridPlaneY = cell.transform.position.y;

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

        isCellHovered = false;
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
        if (piece == null) return;

        ResolveDependencies();
        if (stageManager == null) return;

        if (IsPlacing) CancelPlacement();

        PieceType pieceType = piece.PieceType;
        if (!stageManager.TryRemoveAllyPiece(piece)) return;

        if (slotMap.TryGetValue(pieceType, out InGameUnitStorageSlotUI slot))
            slot.TryRestoreOne();
    }

    private void HandleAllyPieceLeftClick(PieceBase piece)
    {
        if (piece == null) return;

        ResolveDependencies();
        if (stageManager == null) return;

        if (IsPlacing) CancelPlacement();

        PieceType pieceType = piece.PieceType;
        if (!stageManager.TryRemoveAllyPiece(piece)) return;

        // 슬롯 복원 후 바로 배치 시작 (BeginPlacement가 다시 소모)
        if (slotMap.TryGetValue(pieceType, out InGameUnitStorageSlotUI slot))
        {
            slot.TryRestoreOne();
            BeginPlacement(slot);
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



    private void MovePreviewToMouseOnGridPlane()
    {
        if (previewObject == null)
        {
            return;
        }

        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector2 mouseScreen = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        Ray ray = cam.ScreenPointToRay(mouseScreen);
        Plane gridPlane = new Plane(Vector3.up, new Vector3(0f, gridPlaneY, 0f));

        if (gridPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPos = ray.GetPoint(distance);
            previewObject.transform.SetPositionAndRotation(
                worldPos + Vector3.up * previewHeight,
                Quaternion.Euler(0f, (int)currentFacingDirection * 90f, 0f));
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
