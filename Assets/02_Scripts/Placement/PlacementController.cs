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
        StageInputModalGate.BlockedStateChanged -= HandleModalBlockedStateChanged;
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

        // 모달 잠금이 시작되는 순간 진행 중인 배치를 취소해, 미리보기가 모달 뒤에 남거나 슬롯 소모가 유지되지 않게 한다.
        StageInputModalGate.BlockedStateChanged += HandleModalBlockedStateChanged;
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

        if (StageInputModalGate.IsBlocked || !IsPlacing)
        {
            return;
        }

        if (Mouse.current != null &&
            FixedAspectRatioController.ContainsScreenPoint(Mouse.current.position.ReadValue()) &&
            Mouse.current.rightButton.wasPressedThisFrame)
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

    private void HandleModalBlockedStateChanged(bool blocked)
    {
        // 잠금 해제 시에는 아무것도 하지 않는다. 취소된 배치는 플레이어가 슬롯 클릭으로 다시 시작한다.
        if (blocked && IsPlacing)
        {
            CancelPlacement();
        }
    }

    private void OnRotatePerformed(InputAction.CallbackContext context)
    {
        if (StageInputModalGate.IsBlocked || !IsPlacing)
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

        if (!FixedAspectRatioController.ContainsScreenPoint(Mouse.current.position.ReadValue()))
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

        if (stageManager.IsCellOccupied(cell.CellIndex))
        {
            return false;
        }

        // 튜토리얼 가이드 셀에는 가이드가 요구하는 종류만 배치할 수 있다.
        // 다른 종류로 hover하면 기존 배치 불가 셀과 같은 시각 피드백이 표시되고, 좌클릭해도 배치되지 않는다.
        if (pendingSlot != null &&
            stageManager.TryGetTutorialGhostRequiredType(cell.CellIndex, out PieceType requiredType) &&
            pendingSlot.UnitType != requiredType)
        {
            return false;
        }

        return true;
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

        ShowPreviewRotateIcon();
    }

    /// <summary>
    /// 미리보기 기물 머리 위에 회전 안내 아이콘(UI_Rotate_03)을 켠다.
    /// 미리보기는 PieceBase를 꺼 두므로 PieceBase.Start()의 HUD 활성화와
    /// Update()의 상태 갱신이 일어나지 않아, 여기서 HUD를 직접 켜고 상태를 고정한다.
    /// </summary>
    private void ShowPreviewRotateIcon()
    {
        if (previewObject == null)
        {
            return;
        }

        // 기물 Prefab의 HUD 자식은 기본 비활성이고 PieceBase.Start()가 켜는 구조라 직접 켠다.
        Transform hudTransform = previewObject.transform.Find("HUD");
        if (hudTransform == null)
        {
            Debug.LogWarning("PlacementController: preview piece has no HUD child for the rotate icon.", this);
            return;
        }

        hudTransform.gameObject.SetActive(true);

        InGameHUDUI hudUi = hudTransform.GetComponent<InGameHUDUI>();
        if (hudUi == null)
        {
            Debug.LogWarning("PlacementController: preview HUD has no InGameHUDUI component.", this);
            return;
        }

        hudUi.InitializeRotate();
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

        if (!FixedAspectRatioController.ContainsScreenPoint(mouseScreen))
        {
            return;
        }

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
