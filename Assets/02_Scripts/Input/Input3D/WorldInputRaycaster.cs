using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 마우스 위치에서 Raycast 실행

public class WorldInputRaycaster : MonoBehaviour
{
    private const float DefaultPointerDragThreshold = 10f;
    private const int PlacementRaycastHitCapacity = 64;

    public static WorldInputRaycaster Instance { get; private set; }

    [SerializeField] private Camera raycastCamera;
    [SerializeField] private LayerMask raycastLayers = ~0;
    [SerializeField] private float maxDistance = 500f;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference pointerPositionAction;
    [SerializeField] private InputActionReference leftClickAction;
    [SerializeField] private InputActionReference rightClickAction;

    private IWorldInputTarget currentTarget;
    private WorldInputEventData currentEventData;
    private bool isInputBlocked;
    private Vector2 leftPressPosition;
    private bool isTrackingLeftPress;
    private bool hasExceededLeftDragThreshold;
    private readonly RaycastHit[] placementRaycastHits = new RaycastHit[PlacementRaycastHitCapacity];
    private bool isPlacementGridPriorityActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureRaycastCamera();
    }

    private void OnEnable()
    {
        StageInputModalGate.BlockedStateChanged += HandleModalBlockedStateChanged;
        EnableAction(pointerPositionAction);
        EnableAction(leftClickAction);
        EnableAction(rightClickAction);

        if (leftClickAction != null)
        {
            leftClickAction.action.started += OnLeftClickStarted;
            leftClickAction.action.performed += OnLeftClick;
        }
        if (rightClickAction != null) rightClickAction.action.performed += OnRightClick;

    }

    private void OnDisable()
    {
        StageInputModalGate.BlockedStateChanged -= HandleModalBlockedStateChanged;

        if (leftClickAction != null)
        {
            leftClickAction.action.started -= OnLeftClickStarted;
            leftClickAction.action.performed -= OnLeftClick;
        }
        if (rightClickAction != null) rightClickAction.action.performed -= OnRightClick;
        ResetLeftPressTracking();
        ClearHover();
    }

    private void Update()
    {
        EnsureRaycastCamera();
        UpdateLeftDragState();
        UpdateHoverTarget();
    }

    private void UpdateHoverTarget()
    {
        if (isInputBlocked || StageInputModalGate.IsBlocked || raycastCamera == null || pointerPositionAction == null)
        {
            ClearHover();
            return;
        }

        Vector2 pointerPosition = pointerPositionAction.action.ReadValue<Vector2>();
        if (!FixedAspectRatioController.ContainsScreenPoint(pointerPosition))
        {
            ClearHover();
            return;
        }

        Ray ray = raycastCamera.ScreenPointToRay(pointerPosition);

        Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.red);

        if (!TryGetInputHit(ray, out RaycastHit hit))
        {
            ClearHover();
            return;
        }

        IWorldInputTarget nextTarget = hit.collider.GetComponentInParent<IWorldInputTarget>();
        if (nextTarget == null)
        {
            ClearHover();
            return;
        }

        WorldInputEventData eventData = new WorldInputEventData(raycastCamera,
                                                                 ray,
                                                                 hit,
                                                                 hit.collider.gameObject);

        if (!ReferenceEquals(currentTarget, nextTarget))
        {
            ClearHover();

            currentTarget = nextTarget;
            currentEventData = eventData;
            currentTarget.OnWorldHover(currentEventData);
            return;
        }

        currentEventData = eventData;
    }

    /// <summary>
    /// 배치 중에는 피스나 맵 오브젝트가 화면상 셀을 가려도 광선 아래 GridCell을 입력 대상으로 선택한다.
    /// 배치 외 상태는 기존 첫 충돌 우선순위를 그대로 사용해 피스 hover와 타겟팅 감각을 보존한다.
    /// </summary>
    private bool TryGetInputHit(Ray ray, out RaycastHit selectedHit)
    {
        if (!isPlacementGridPriorityActive)
        {
            return Physics.Raycast(ray, out selectedHit, maxDistance, raycastLayers);
        }

        int hitCount = Physics.RaycastNonAlloc(ray, placementRaycastHits, maxDistance, raycastLayers);
        float nearestGridCellDistance = float.PositiveInfinity;
        selectedHit = default;

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit candidateHit = placementRaycastHits[index];
            if (candidateHit.distance >= nearestGridCellDistance ||
                candidateHit.collider.GetComponentInParent<GridCell>() == null)
            {
                continue;
            }

            selectedHit = candidateHit;
            nearestGridCellDistance = candidateHit.distance;
        }

        return nearestGridCellDistance < float.PositiveInfinity;
    }


    private static void EnableAction(InputActionReference actionReference)
    {
        if (actionReference != null && actionReference.action != null)
        {
            actionReference.action.Enable();
        }
    }

    private void EnsureRaycastCamera()
    {
        if (raycastCamera == null || !raycastCamera.isActiveAndEnabled)
        {
            raycastCamera = Camera.main;
        }
    }

    private void OnLeftClickStarted(InputAction.CallbackContext context)
    {
        if (pointerPositionAction?.action == null)
        {
            ResetLeftPressTracking();
            return;
        }

        leftPressPosition = pointerPositionAction.action.ReadValue<Vector2>();
        isTrackingLeftPress = true;
        hasExceededLeftDragThreshold = false;
    }

    private void OnLeftClick(InputAction.CallbackContext context)
    {
        // 마지막 이동과 릴리스가 같은 InputSystem 갱신에 들어와도 현재 위치로 Drag 여부를 한 번 더 판정한다.
        UpdateLeftDragState();
        bool wasPointerDrag = hasExceededLeftDragThreshold;
        ResetLeftPressTracking();

        if (wasPointerDrag)
        {
            // 카메라 Drag를 끝낸 릴리스는 현재 hover 대원의 선택/배치 흐름으로 보내지 않는다.
            return;
        }

        if (!CanDispatchPointerClick())
        {
            ClearHover();
            return;
        }

        if (currentTarget != null)
        {
            currentTarget.OnWorldLeftClick(currentEventData);
        }
    }

    private void UpdateLeftDragState()
    {
        if (!isTrackingLeftPress || hasExceededLeftDragThreshold || pointerPositionAction?.action == null)
        {
            return;
        }

        float dragThreshold = EventSystem.current != null
            ? EventSystem.current.pixelDragThreshold
            : DefaultPointerDragThreshold;
        Vector2 pointerPosition = pointerPositionAction.action.ReadValue<Vector2>();

        // 카메라 회전이 사용하는 수평 이동만 판정해 세로 방향의 작은 마우스 흔들림은 일반 클릭으로 유지한다.
        hasExceededLeftDragThreshold =
            Mathf.Abs(pointerPosition.x - leftPressPosition.x) >= Mathf.Max(1f, dragThreshold);
    }

    private void ResetLeftPressTracking()
    {
        isTrackingLeftPress = false;
        hasExceededLeftDragThreshold = false;
    }

    private void OnRightClick(InputAction.CallbackContext context)
    {
        if (!CanDispatchPointerClick())
        {
            ClearHover();
            return;
        }

        if (currentTarget != null)
        {
            currentTarget.OnWorldRightClick(currentEventData);
        }
    }

    private bool CanDispatchPointerClick()
    {
        if (isInputBlocked || StageInputModalGate.IsBlocked || raycastCamera == null || pointerPositionAction?.action == null)
        {
            return false;
        }

        Vector2 pointerPosition = pointerPositionAction.action.ReadValue<Vector2>();
        return FixedAspectRatioController.ContainsScreenPoint(pointerPosition);
    }

    public void SetInputBlocked(bool blocked)
    {
        isInputBlocked = blocked;

        if (blocked)
        {
            // 모달 UI가 열릴 때 기존 3D 호버 표시도 같은 프레임에 정리한다.
            ClearHover();
        }
    }

    /// <summary>
    /// 배치 상태가 바뀌는 즉시 기존 피스 hover를 정리하고 다음 Raycast부터 셀 우선순위를 적용한다.
    /// </summary>
    public void SetPlacementGridPriorityActive(bool active)
    {
        if (isPlacementGridPriorityActive == active)
        {
            return;
        }

        isPlacementGridPriorityActive = active;
        ClearHover();
    }

    private void HandleModalBlockedStateChanged(bool blocked)
    {
        if (blocked)
        {
            // 모달 Lease를 얻는 즉시 기존 hover를 지워 뒤쪽 월드 강조가 남지 않게 한다.
            ClearHover();
        }
    }

    private void ClearHover()
    {
        if (currentTarget == null)
        {
            return;
        }

        // 인터페이스 참조는 Unity의 파괴 감지(== null 오버로드)를 우회하므로,
        // Stage 정리로 파괴된 대상은 UnHover 콜백 없이 참조만 비워 MissingReferenceException 반복을 막는다.
        if (currentTarget is MonoBehaviour targetBehaviour && targetBehaviour == null)
        {
            currentTarget = null;
            currentEventData = default;
            return;
        }

        currentTarget.OnWorldUnHover(currentEventData);
        currentTarget = null;
        currentEventData = default;
    }
    public GameObject GetCurrentHoveredObject()
    {

        return currentTarget != null ? currentEventData.TargetObject : null;
    }
}
