using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class OrbitingCamera : MonoBehaviour
{
    [SerializeField] InputActionReference orbitAction;
    [SerializeField] private float orbitSpeed = 90f;
    [SerializeField] private float resetSpeed = 180f;

    private Coroutine orbitCoroutine;
    private float currentOrbitAngle = 0f;
    [SerializeField] private float minOrbitAngle = -30f;
    [SerializeField] private float maxOrbitAngle = 30f;

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
        if (orbitAction?.action == null)
        {
            return;
        }

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
        StopOrbit();
    }

    private void StartOrbit(float direction)
    {
        StopOrbit();
        orbitCoroutine = StartCoroutine(OrbitRoutine(direction));
    }

    private IEnumerator OrbitRoutine(float direction)
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

    public IEnumerator ResetToDefaultOrbit()
    {
        StopOrbit();

        const float targetAngle = 0f;
        while (!Mathf.Approximately(currentOrbitAngle, targetAngle))
        {
            float previousAngle = currentOrbitAngle;
            currentOrbitAngle = Mathf.MoveTowards(currentOrbitAngle, targetAngle, resetSpeed * Time.deltaTime);
            float delta = currentOrbitAngle - previousAngle;
            transform.Rotate(Vector3.up, delta, Space.World);
            yield return null;
        }

        currentOrbitAngle = targetAngle;
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
}
