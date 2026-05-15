using UnityEngine;
using UnityEngine.InputSystem;

// 마우스 위치에서 Raycast 실행

public class WorldInputRaycaster : MonoBehaviour
{
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private LayerMask raycastLayers = ~0;
    [SerializeField] private float maxDistance = 500f;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference pointerPositionAction;
    [SerializeField] private InputActionReference leftClickAction;
    [SerializeField] private InputActionReference rightClickAction;

    private IWorldInputTarget currentTarget;
    private WorldInputEventData currentEventData;

    private void Awake()
    {
        if (raycastCamera == null)
        {
            raycastCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        EnableAction(pointerPositionAction);
        EnableAction(leftClickAction);
        EnableAction(rightClickAction);

        if (leftClickAction != null) leftClickAction.action.performed += OnLeftClick;
        if (rightClickAction != null) rightClickAction.action.performed += OnRightClick;

    }

    private void Update()
    {
        UpdateHoverTarget();
    }

    private void UpdateHoverTarget()
    {
        if (raycastCamera == null || pointerPositionAction == null)
        {
            ClearHover();
            return;
        }

        Vector2 pointerPosition = pointerPositionAction.action.ReadValue<Vector2>();
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

        WorldInputEventData eventData = new WorldInputEventData( raycastCamera,
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

    private void OnLeftClick(InputAction.CallbackContext context)
    {
        if (currentTarget != null)
        {
            currentTarget.OnWorldLeftClick(currentEventData);
        }
    }

    private void OnRightClick(InputAction.CallbackContext context)
    {
        if (currentTarget != null)
        {
            currentTarget.OnWorldRightClick(currentEventData);
        }
    }

    private void ClearHover()
    {
        if (currentTarget == null)
        {
            return;
        }

        currentTarget.OnWorldUnHover(currentEventData);
        currentTarget = null;
    }
}
