using UnityEngine;
using UnityEngine.InputSystem;

// 마우스 위치에서 Raycast 실행

public class WorldInputRaycaster : MonoBehaviour
{
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

        if (leftClickAction != null) leftClickAction.action.performed += OnLeftClick;
        if (rightClickAction != null) rightClickAction.action.performed += OnRightClick;

    }

    private void OnDisable()
    {
        StageInputModalGate.BlockedStateChanged -= HandleModalBlockedStateChanged;

        if (leftClickAction != null) leftClickAction.action.performed -= OnLeftClick;
        if (rightClickAction != null) rightClickAction.action.performed -= OnRightClick;
        ClearHover();
    }

    private void Update()
    {
        EnsureRaycastCamera();
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

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, raycastLayers))
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

    private void OnLeftClick(InputAction.CallbackContext context)
    {
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
