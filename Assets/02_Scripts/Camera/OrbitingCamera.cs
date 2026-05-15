using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitingCamera : MonoBehaviour
{
    [SerializeField] InputActionReference orbitAction;
    [SerializeField] private float orbitSpeed = 90f;

    private Coroutine orbitCoroutine;
    private float currentOrbitAngle = 0f;
    [SerializeField] private float minOrbitAngle = -30f;
    [SerializeField] private float maxOrbitAngle = 30f;

    private void OnEnable()
    {
        orbitAction.action.started  += OnOrbitStarted;
        orbitAction.action.canceled += OnOrbitStopped;
    }

    private void OnDisable()
    {
        orbitAction.action.started  -= OnOrbitStarted;
        orbitAction.action.canceled -= OnOrbitStopped;
    }

    private void OnOrbitStarted(InputAction.CallbackContext ctx)
    {
        float orbitDirection = orbitAction.action.ReadValue<float>();
        StartOrbit(orbitDirection);

    } 
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
