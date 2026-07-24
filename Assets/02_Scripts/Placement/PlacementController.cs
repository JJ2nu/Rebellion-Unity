using Rebellion;
using System;
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
    [SerializeField] private float mouseWheelRotateThreshold = 0.01f;

    private InGameUnitStorageSlotUI pendingSlot;
    private GameObject previewObject;
    private Direction currentFacingDirection;
    private readonly Dictionary<PieceType, InGameUnitStorageSlotUI> slotMap = new();

    private bool isCellHovered;
    private float gridPlaneY;
    private Camera mainCamera;

    /// <summary>
    /// 배치 시작 또는 종료로 IsPlacing 값이 실제로 바뀐 직후 새 상태를 전달한다.
    /// </summary>
    public event Action<bool> PlacementStateChanged;

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
        EnsureMainCamera();

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
        EnsureMainCamera();

        if (!IsPlacing)
        {
            return;
        }

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelPlacement();
            return;
        }

        HandleMouseWheelRotation();

        if (!isCellHovered)
        {
            MovePreviewToMouseOnGridPlane();
        }
    }

public void BeginPlacement(InGameUnitStorageSlotUI slot)
    {
        if (slot == null)
        {
            return;
        }

        if (pendingSlot == slot)
        {
            return;
        }

        if (pendingSlot != null)
        {
            CancelPlacement();
        }

        if (slot.RemainingDeployableCount <= 0)
        {
            return;
        }

        ResolveDependencies();

        bool wasPlacing = IsPlacing;
        ClearPreview();
        currentFacingDirection = defaultFacingDirection;
        pendingSlot = slot;
        slot.TryConsumeOne();
        CreatePreview(slot.UnitType);
        NotifyPlacementStateChanged(wasPlacing);
    }

    public void CancelPlacement()
    {
        bool wasPlacing = IsPlacing;

        if (pendingSlot != null)
        {
            pendingSlot.TryRestoreOne();
        }

        pendingSlot = null;
        isCellHovered = false;
        ClearPreview();
        NotifyPlacementStateChanged(wasPlacing);
    }

    private void OnRotatePerformed(InputAction.CallbackContext context)
    {
        if (!IsPlacing)
        {
            return;
        }

        RotatePreview(1);
    }

    private void HandleMouseWheelRotation()
    {
        if (!IsPlacing || Mouse.current == null)
        {
            return;
        }

        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) <= mouseWheelRotateThreshold)
        {
            return;
        }

        RotatePreview(scrollY > 0f ? 1 : -1);
    }

    private void RotatePreview(int directionDelta)
    {
        if (!IsPlacing || directionDelta == 0)
        {
            return;
        }

        int directionCount = Enum.GetValues(typeof(Direction)).Length;
        int nextDirection = ((int)currentFacingDirection + directionDelta) % directionCount;
        if (nextDirection < 0)
        {
            nextDirection += directionCount;
        }

        currentFacingDirection = (Direction)nextDirection;
        ApplyPreviewRotation();
    }

    private void ApplyPreviewRotation()
    {
        if (previewObject != null)
        {
            previewObject.transform.rotation = Quaternion.Euler(0f, (int)currentFacingDirection * 90f, 0f);
        }
    }


public void HandleCellHover(GridCell cell)
    {
        if (!IsPlacing || cell == null || SimulationController.Instance?._isRunning == true)
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
        if (cell == null || SimulationController.Instance?._isRunning == true)
        {
            return;
        }

        isCellHovered = false;
        cell.ResetVisual();
    }

public void HandleCellLeftClick(GridCell cell)
    {
        if (!IsPlacing || cell == null || SimulationController.Instance?._isRunning == true)
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

        bool wasPlacing = IsPlacing;
        pendingSlot = null;
        cell.ResetVisual();
        ClearPreview();
        NotifyPlacementStateChanged(wasPlacing);
    }

    public void HandleCellRightClick(GridCell cell)
    {
        if (!IsPlacing || cell == null || SimulationController.Instance?._isRunning == true)
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
        if (piece == null || SimulationController.Instance?._isRunning == true) return;

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
        if (piece == null || SimulationController.Instance?._isRunning == true) return;

        var prevDirection = piece.FacingDirection;

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
            currentFacingDirection = prevDirection; // 이전 방향 유지

        }
    }

    public void RegisterSlot(InGameUnitStorageSlotUI slot)
    {
        if (slot != null)
        {
            slotMap[slot.UnitType] = slot;
        }
    }

    /// <summary>
    /// Storage UI 재생성 전에 제거될 슬롯 인스턴스를 타입별 등록 맵에서 비운다.
    /// </summary>
    public void ClearRegisteredSlots()
    {
        slotMap.Clear();
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

    private void NotifyPlacementStateChanged(bool previousState)
    {
        bool currentState = IsPlacing;
        if (previousState == currentState)
        {
            return;
        }

        // 상태 변경을 모두 마친 뒤 알리므로 구독자는 같은 프레임에 완성된 배치 상태를 읽을 수 있다.
        PlacementStateChanged?.Invoke(currentState);
    }

    private void ResolveDependencies()
    {
        if (stageManager == null)
        {
            stageManager = StageManager.Instance;
        }
    }

    private void EnsureMainCamera()
    {
        if (mainCamera == null || !mainCamera.isActiveAndEnabled)
        {
            mainCamera = Camera.main;
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
