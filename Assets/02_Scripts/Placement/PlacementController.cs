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
    // 미리보기 기물의 PieceBase 캐시. 컴포넌트는 비활성이지만 범위 계산 메서드 호출에 사용한다.
    private PieceBase previewPiece;
    private Direction currentFacingDirection;
    private readonly Dictionary<PieceType, InGameUnitStorageSlotUI> slotMap = new();

    private bool isCellHovered;
    // 현재 미리보기가 스냅된 셀. 회전 시 같은 셀 기준으로 범위 표시를 갱신하기 위해 저장한다.
    private GridCell hoveredCell;
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
        hoveredCell = null;
        // 취소 시점에 셀 위였다면 범위·해골 표시가 화면에 남지 않게 지운다.
        ClearPreviewAttackRange();
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

        // 셀 위에서 회전하면 새 방향 기준으로 공격 범위·해골 표시를 즉시 갱신한다.
        if (isCellHovered && hoveredCell != null)
        {
            UpdatePreviewAttackRange(hoveredCell);
        }
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
        hoveredCell = cell;
        gridPlaneY = cell.transform.position.y;

        if (previewObject != null)
        {
            previewObject.transform.SetPositionAndRotation(
                cell.transform.position + Vector3.up * previewHeight,
                Quaternion.Euler(0f, (int)currentFacingDirection * 90f, 0f));
        }

        // 배치 확정 전에도 이 셀에 놓았을 때의 공격 범위·해골 표시를 미리 보여준다.
        // 스냅 셀의 배치 가능/불가 표시(ShowPlacementAvailability)는 범위 정리 후 이 안에서 적용한다.
        UpdatePreviewAttackRange(cell);
    }

    public void HandleCellUnhover(GridCell cell)
    {
        if (cell == null || SimulationController.Instance?._isRunning == true)
        {
            return;
        }

        isCellHovered = false;
        hoveredCell = null;
        cell.ResetVisual();

        // 배치 중이 아닐 때는 기물 호버가 쓰는 범위 표시를 건드리지 않도록 배치 중에만 지운다.
        if (IsPlacing)
        {
            ClearPreviewAttackRange();
        }
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
        hoveredCell = null;
        cell.ResetVisual();
        // 확정 직후에는 미리보기용 범위 표시를 지운다. 이후 표시는 배치된 실제 기물 호버가 담당한다.
        ClearPreviewAttackRange();
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

        // 범위 계산에 재사용할 수 있게 비활성 상태 그대로 캐시한다.
        previewPiece = piece;

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

        previewPiece = null;
    }

    /// <summary>
    /// 미리보기 기물이 스냅된 셀 기준으로 공격 범위 셀 하이라이트와 범위 안 기물의 해골 HUD를 갱신한다.
    /// 배치된 기물 호버와 동일한 PieceBase.ShowAttackRangeCells 경로를 재사용해
    /// 종류별 범위 규칙(Brawler/Gunman 직선 사거리, Slasher 이동 경로)을 그대로 따른다.
    /// </summary>
    private void UpdatePreviewAttackRange(GridCell cell)
    {
        if (cell == null)
        {
            return;
        }

        ResolveDependencies();

        // 셀 이동·회전 시 이전 위치 기준 표시가 남지 않게 항상 먼저 지운다.
        ClearPreviewAttackRange();

        bool canPlace = CanPlaceOn(cell);

        // 배치 불가 셀(점유·튜토리얼 종류 불일치)에서는 범위를 표시하지 않는다 (Task 57 범위 밖).
        if (previewPiece != null && stageManager != null && canPlace)
        {
            // 미리보기는 PieceBase가 비활성이라 좌표·방향이 갱신되지 않으므로 스냅된 셀 기준으로 직접 설정한다.
            Vector2Int gridCoord = StageGridIndexUtility.ToGridCoord(cell.BoardSize, cell.CellIndex);
            previewPiece.GridX = gridCoord.x;
            previewPiece.GridY = gridCoord.y;
            previewPiece.FacingDirection = currentFacingDirection;

            // 미리보기는 StageManager 목록에 없으므로 배치된 기물들만 대상으로 공격 가능 여부를 판정한다.
            previewPiece.CheckCanAct(stageManager.GetAllActivePieces());
            previewPiece.ShowAttackRangeCells();
        }

        // ClearAllRangeHighlights가 모든 셀을 Default로 되돌리므로,
        // 스냅된 셀의 배치 가능/불가 표시는 범위 표시 뒤에 다시 적용해야 남는다.
        cell.ShowPlacementAvailability(canPlace);
    }

    /// <summary>
    /// 미리보기용 범위 표시(셀 하이라이트 + 범위 안 기물의 _isInRange 해골 HUD)를 해제한다.
    /// 배치된 기물 호버 해제(PieceBase.OnWorldUnHover)와 같은 정리 경로를 사용한다.
    /// </summary>
    private void ClearPreviewAttackRange()
    {
        StageManager.Instance?.ClearAttackRange();
        GameManager.Instance?.ClearAllRangeHighlights();
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
