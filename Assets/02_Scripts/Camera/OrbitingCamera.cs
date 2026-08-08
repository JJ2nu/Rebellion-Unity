using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class OrbitingCamera : MonoBehaviour
{
    [SerializeField] InputActionReference orbitAction;
    [SerializeField] private PlacementController placementController;
    [SerializeField] private float orbitSpeed = 90f;
    [SerializeField] private float dragOrbitSpeed = 0.18f;
    [SerializeField, Min(0.01f)] private float resetDuration = 0.35f;

    private Coroutine orbitCoroutine;
    private Coroutine resetCoroutine;
    private float currentOrbitAngle = 0f;
    [SerializeField] private float minOrbitAngle = -30f;
    [SerializeField] private float maxOrbitAngle = 30f;
    private bool isDraggingMap;

    public bool IsResettingToDefaultOrbit => isActiveAndEnabled && resetCoroutine != null;

    private void OnEnable()
    {
        if (orbitAction?.action == null)
        {
            return;
        }

        orbitAction.action.started  += OnOrbitStarted;
        orbitAction.action.canceled += OnOrbitStopped;
    }

    private void OnDisable()
    {
        isDraggingMap = false;
        StopReset();

        if (orbitAction?.action == null)
        {
            return;
        }

        orbitAction.action.started  -= OnOrbitStarted;
        orbitAction.action.canceled -= OnOrbitStopped;
    }

    private void Update()
    {
        if (StageInputModalGate.IsBlocked)
        {
            isDraggingMap = false;
            StopOrbit();
            return;
        }

        HandleMouseDragOrbit();
    }

    private void OnOrbitStarted(InputAction.CallbackContext ctx)
    {
        if (StageInputModalGate.IsBlocked)
        {
            return;
        }

        float orbitDirection = orbitAction.action.ReadValue<float>();
        StartOrbit(orbitDirection);

    } 
    private void OnOrbitStopped(InputAction.CallbackContext ctx)
    {
        StopOrbit();
    }

    private void StartOrbit(float direction)
    {
        StopReset();
        StopOrbit();
        orbitCoroutine = StartCoroutine(OrbitRoutine(direction));
    }

    private IEnumerator OrbitRoutine(float direction)
    {
        while (!StageInputModalGate.IsBlocked)
        {
            float delta = direction * orbitSpeed * Time.deltaTime;
            ApplyOrbitDelta(delta);
            yield return null;
        }
    }

    public void StartResetToDefaultOrbit()
    {
        StopReset();
        if (Mathf.Approximately(currentOrbitAngle, 0f))
        {
            currentOrbitAngle = 0f;
            resetCoroutine = null;
            return;
        }

        resetCoroutine = StartCoroutine(ResetToDefaultOrbit());
    }

    public IEnumerator ResetToDefaultOrbit()
    {
        StopOrbit();

        const float targetAngle = 0f;
        float startAngle = currentOrbitAngle;
        if (Mathf.Approximately(startAngle, targetAngle))
        {
            currentOrbitAngle = targetAngle;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < resetDuration)
        {
            float previousAngle = currentOrbitAngle;
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / resetDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            currentOrbitAngle = Mathf.Lerp(startAngle, targetAngle, eased);

            float delta = currentOrbitAngle - previousAngle;
            transform.Rotate(Vector3.up, delta, Space.World);
            yield return null;
        }

        float finalDelta = targetAngle - currentOrbitAngle;
        if (!Mathf.Approximately(finalDelta, 0f))
        {
            transform.Rotate(Vector3.up, finalDelta, Space.World);
        }

        currentOrbitAngle = targetAngle;
        resetCoroutine = null;
    }

    private void StopOrbit()
    {
        if (orbitCoroutine == null)
        {
            return;
        }

        StopCoroutine(orbitCoroutine);
        orbitCoroutine = null;
    }

    private void StopReset()
    {
        if (resetCoroutine == null)
        {
            return;
        }

        StopCoroutine(resetCoroutine);
        resetCoroutine = null;
    }

    private void HandleMouseDragOrbit()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            isDraggingMap = false;
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            isDraggingMap = CanStartMouseDragOrbit();
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            isDraggingMap = false;
            return;
        }

        if (!isDraggingMap || !mouse.leftButton.isPressed)
        {
            return;
        }

        if (!CanContinueMouseDragOrbit())
        {
            isDraggingMap = false;
            return;
        }

        Vector2 delta = mouse.delta.ReadValue();
        if (Mathf.Approximately(delta.x, 0f))
        {
            return;
        }

        StopReset();
        StopOrbit();
        ApplyOrbitDelta(delta.x * dragOrbitSpeed);
    }

    private bool CanStartMouseDragOrbit()
    {
        if (!CanContinueMouseDragOrbit())
        {
            return false;
        }

        return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
    }

    private bool CanContinueMouseDragOrbit()
    {
        if (StageInputModalGate.IsBlocked)
        {
            return false;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null ||
            !FixedAspectRatioController.ContainsScreenPoint(mouse.position.ReadValue()))
        {
            return false;
        }

        if (SimulationController.Instance?._isRunning == true)
        {
            return false;
        }

        if (placementController == null)
        {
            placementController = FindFirstObjectByType<PlacementController>();
        }

        return placementController == null || !placementController.IsPlacing;
    }

    private void ApplyOrbitDelta(float delta)
    {
        float newAngle = Mathf.Clamp(currentOrbitAngle + delta, minOrbitAngle, maxOrbitAngle);
        float actualDelta = newAngle - currentOrbitAngle;
        if (Mathf.Approximately(actualDelta, 0f))
        {
            currentOrbitAngle = newAngle;
            return;
        }

        currentOrbitAngle = newAngle;
        transform.Rotate(Vector3.up, actualDelta, Space.World);
    }
}
