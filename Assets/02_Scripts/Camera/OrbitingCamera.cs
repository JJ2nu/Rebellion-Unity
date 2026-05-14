using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitingCamera : MonoBehaviour
{
    [SerializeField] InputActionReference orbitLeftAction;
    [SerializeField] InputActionReference orbitRightAction;
    [SerializeField] private float orbitSpeed = 90f;

    private Coroutine orbitCoroutine;
    private float currentOrbitAngle = 0f;
    [SerializeField] private float minOrbitAngle = -30f;
    [SerializeField] private float maxOrbitAngle = 30f;

    private void OnEnable()
    {
        orbitLeftAction.action.started  += OnOrbitLeftStarted;
        orbitLeftAction.action.canceled += OnOrbitStopped;
        orbitRightAction.action.started  += OnOrbitRightStarted;
        orbitRightAction.action.canceled += OnOrbitStopped;
    }

    private void OnDisable()
    {
        orbitLeftAction.action.started  -= OnOrbitLeftStarted;
        orbitLeftAction.action.canceled -= OnOrbitStopped;
        orbitRightAction.action.started  -= OnOrbitRightStarted;
        orbitRightAction.action.canceled -= OnOrbitStopped;
    }

    private void OnOrbitLeftStarted(InputAction.CallbackContext ctx)  => StartOrbit(1f);
    private void OnOrbitRightStarted(InputAction.CallbackContext ctx) => StartOrbit(-1f);
    private void OnOrbitStopped(InputAction.CallbackContext ctx)
    {
        if (orbitCoroutine != null) StopCoroutine(orbitCoroutine);
    }

    private void StartOrbit(float direction)
    {
        if (orbitCoroutine != null) StopCoroutine(orbitCoroutine);
        orbitCoroutine = StartCoroutine(OrbitRoutine(direction));
    }

    private System.Collections.IEnumerator OrbitRoutine(float direction)
    {
        while (true)
        {
            float delta = direction * orbitSpeed * Time.deltaTime;
            float newAngle = Mathf.Clamp(currentOrbitAngle + delta, minOrbitAngle, maxOrbitAngle);
            float actualDelta = newAngle - currentOrbitAngle;
            currentOrbitAngle = newAngle;
            transform.Rotate(Vector3.up, actualDelta, Space.World);
            yield return null;
        }
    }
}
