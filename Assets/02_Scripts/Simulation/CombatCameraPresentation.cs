using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 Presentation 중 카메라의 로컬 위치, 회전, 렌즈만 제어한다.
/// OrbitingCamera의 부모 회전과 전투 판정에는 관여하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatCameraPresentation : MonoBehaviour
{
    [Header("Focus")]
    [SerializeField, Range(0f, 1f)] private float focusPositionWeight = 0.15f;
    [SerializeField, Min(0f)] private float maxFocusOffset = 0.8f;
    [SerializeField, Min(0f)] private float focusLookHeight = 0.45f;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float focusTransitionDuration = 0.5f;
    [SerializeField, Min(0.01f)] private float returnTransitionDuration = 0.5f;

    [Header("Lens")]
    [SerializeField, Range(0.5f, 1f)] private float zoomMultiplier = 0.97f;

    [Header("Projectile Focus")]
    [SerializeField, Range(0f, 1f)] private float projectileFocusPositionWeight = 0.55f;
    [SerializeField, Min(0f)] private float projectileMaxFocusOffset = 1.7f;
    [SerializeField, Min(0.01f)] private float projectileFocusTransitionDuration = 0.22f;

    [Header("Hit Impact - Blunt")]
    [SerializeField, Min(0.01f)] private float bluntShakeDuration = 0.10f;
    [SerializeField, Min(0f)] private float bluntPositionAmplitude = 0.025f;
    [SerializeField, Min(0f)] private float bluntRotationAmplitude = 0.30f;

    [Header("Hit Impact - Slash")]
    [SerializeField, Min(0.01f)] private float slashShakeDuration = 0.07f;
    [SerializeField, Min(0f)] private float slashPositionAmplitude = 0.015f;
    [SerializeField, Min(0f)] private float slashRotationAmplitude = 0.20f;

    [Header("Hit Impact - Projectile")]
    [SerializeField, Min(0.01f)] private float projectileShakeDuration = 0.05f;
    [SerializeField, Min(0f)] private float projectilePositionAmplitude = 0.005f;
    [SerializeField, Min(0f)] private float projectileRotationAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float projectileLensPunch = 0f;

    [Header("Hit Impact - Overlap Limits")]
    [SerializeField, Min(0f)] private float maxShakePositionOffset = 0.04f;
    [SerializeField, Min(0f)] private float maxShakeRotationOffset = 0.35f;
    [SerializeField, Min(0f)] private float maxLensPunch = 0f;

    private readonly HashSet<PieceBase> focusPieces = new();
    private readonly List<CameraShakeImpulse> shakeImpulses = new();
    private readonly List<BulletController> trackedProjectiles = new();

    private Camera presentedCamera;
    private Transform cameraTransform;
    private Transform cameraParent;
    private bool baselineCaptured;
    private bool warnedAboutUnavailableCamera;
    private Vector3 baselineLocalPosition;
    private Quaternion baselineLocalRotation;
    private float baselineFieldOfView;
    private float baselineOrthographicSize;
    private bool baselineWasOrthographic;
    private Vector3 targetLocalPosition;
    private Quaternion targetLocalRotation;
    private float targetLensSize;
    private float transitionDuration;
    private Vector3 smoothAnchorLocalPosition;
    private Quaternion smoothAnchorLocalRotation;
    private float smoothAnchorLensSize;
    private uint hitSequence;
    private bool isGunmanPhase;

    private void LateUpdate()
    {
        if (!baselineCaptured || presentedCamera == null || cameraTransform == null)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, transitionDuration);
        UpdateProjectileFocusTarget();
        float easing = 1f - Mathf.Exp(-Time.unscaledDeltaTime / duration);

        smoothAnchorLocalPosition = Vector3.Lerp(
            smoothAnchorLocalPosition,
            targetLocalPosition,
            easing);
        smoothAnchorLocalRotation = Quaternion.Slerp(
            smoothAnchorLocalRotation,
            targetLocalRotation,
            easing);
        smoothAnchorLensSize = Mathf.Lerp(smoothAnchorLensSize, targetLensSize, easing);

        ApplyAnchorWithShake();
    }

    private void OnDisable()
    {
        RestoreImmediately();
    }

    private void OnDestroy()
    {
        RestoreImmediately();
    }

    public void BeginRun(CombatSimulationContext context)
    {
        RestoreImmediately();
        focusPieces.Clear();
        trackedProjectiles.Clear();
        isGunmanPhase = false;

        if (!TryCaptureBaseline())
        {
            return;
        }

        transitionDuration = focusTransitionDuration;
        targetLocalPosition = baselineLocalPosition;
        targetLocalRotation = baselineLocalRotation;
        targetLensSize = GetZoomedLensSize();
        smoothAnchorLocalPosition = baselineLocalPosition;
        smoothAnchorLocalRotation = baselineLocalRotation;
        smoothAnchorLensSize = baselineWasOrthographic
            ? baselineOrthographicSize
            : baselineFieldOfView;
    }

    public void BeginPhase(CombatPhaseContext context)
    {
        if (!baselineCaptured)
        {
            return;
        }

        trackedProjectiles.Clear();
        isGunmanPhase = ContainsGunman(context.ActivePieces);
        focusPieces.Clear();
        foreach (CombatPieceSnapshot piece in context.ActivePieces)
        {
            if (!isGunmanPhase || piece.PieceType == PieceType.Gunman)
            {
                AddFocusPiece(piece.Piece);
            }
        }

        RecalculateFocusTarget(focusTransitionDuration);
    }

    public void IncludeAttack(CombatAttackContext context)
    {
        if (!baselineCaptured)
        {
            return;
        }

        AddFocusPiece(context.Attacker.Piece);
        if (isGunmanPhase && context.Attacker.PieceType == PieceType.Gunman)
        {
            RecalculateFocusTarget(focusTransitionDuration);
            return;
        }

        if (context.HasTarget)
        {
            AddFocusPiece(context.Target.Piece);
        }

        RecalculateFocusTarget(focusTransitionDuration);
    }

    public void RegisterProjectile(CombatProjectileSpawnedContext context)
    {
        if (!baselineCaptured
            || !isGunmanPhase
            || context.Shooter.Faction != Faction.Ally
            || context.Projectile == null
            || !context.Projectile.IsFlying)
        {
            return;
        }

        if (!trackedProjectiles.Contains(context.Projectile))
        {
            trackedProjectiles.Add(context.Projectile);
        }
    }

    public void ReturnToBaseline()
    {
        if (!baselineCaptured)
        {
            return;
        }

        focusPieces.Clear();
        trackedProjectiles.Clear();
        isGunmanPhase = false;
        transitionDuration = returnTransitionDuration;
        targetLocalPosition = baselineLocalPosition;
        targetLocalRotation = baselineLocalRotation;
        targetLensSize = baselineWasOrthographic
            ? baselineOrthographicSize
            : baselineFieldOfView;
        shakeImpulses.Clear();
    }

    public void PlayHitImpact(CombatHitContext context)
    {
        if (!baselineCaptured || cameraTransform == null)
        {
            return;
        }

        GetImpactProfile(
            context.AttackType,
            out float duration,
            out float positionAmplitude,
            out float rotationAmplitude,
            out float lensPunch);

        if (positionAmplitude <= 0f && rotationAmplitude <= 0f && lensPunch <= 0f)
        {
            return;
        }

        Vector3 localImpactDirection = cameraTransform.InverseTransformDirection(context.ImpactDirection);
        if (localImpactDirection.sqrMagnitude <= 0.0001f)
        {
            localImpactDirection = Vector3.right;
        }

        shakeImpulses.Add(new CameraShakeImpulse(
            localImpactDirection.normalized,
            duration,
            positionAmplitude,
            rotationAmplitude,
            lensPunch,
            hitSequence++));
    }

    public void RestoreImmediately()
    {
        if (baselineCaptured && presentedCamera != null && cameraTransform != null)
        {
            cameraTransform.localPosition = baselineLocalPosition;
            cameraTransform.localRotation = baselineLocalRotation;

            if (baselineWasOrthographic)
            {
                presentedCamera.orthographicSize = baselineOrthographicSize;
            }
            else
            {
                presentedCamera.fieldOfView = baselineFieldOfView;
            }
        }

        baselineCaptured = false;
        focusPieces.Clear();
        shakeImpulses.Clear();
        trackedProjectiles.Clear();
        isGunmanPhase = false;
        hitSequence = 0;
        presentedCamera = null;
        cameraTransform = null;
        cameraParent = null;
    }

    private bool TryCaptureBaseline()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            WarnCameraUnavailable("Combat camera presentation could not find Camera.main.");
            return false;
        }

        Transform mainCameraTransform = mainCamera.transform;
        if (mainCameraTransform.parent == null)
        {
            WarnCameraUnavailable(
                "Combat camera presentation requires Camera.main to be a child of an orbit or camera rig transform.");
            return false;
        }

        presentedCamera = mainCamera;
        cameraTransform = mainCameraTransform;
        cameraParent = mainCameraTransform.parent;
        baselineLocalPosition = cameraTransform.localPosition;
        baselineLocalRotation = cameraTransform.localRotation;
        baselineFieldOfView = presentedCamera.fieldOfView;
        baselineOrthographicSize = presentedCamera.orthographicSize;
        baselineWasOrthographic = presentedCamera.orthographic;
        targetLocalPosition = baselineLocalPosition;
        targetLocalRotation = baselineLocalRotation;
        targetLensSize = baselineWasOrthographic
            ? baselineOrthographicSize
            : baselineFieldOfView;
        smoothAnchorLocalPosition = baselineLocalPosition;
        smoothAnchorLocalRotation = baselineLocalRotation;
        smoothAnchorLensSize = targetLensSize;
        baselineCaptured = true;
        return true;
    }

    private void RecalculateFocusTarget(float nextTransitionDuration)
    {
        if (cameraParent == null || focusPieces.Count == 0)
        {
            return;
        }

        Vector3 focusCenter = Vector3.zero;
        int pieceCount = 0;
        foreach (PieceBase piece in focusPieces)
        {
            if (piece == null || !piece.gameObject.activeInHierarchy)
            {
                continue;
            }

            focusCenter = focusCenter + piece.transform.position;
            pieceCount++;
        }

        if (pieceCount == 0)
        {
            return;
        }

        SetFocusTarget(
            focusCenter / pieceCount,
            focusPositionWeight,
            maxFocusOffset,
            nextTransitionDuration);
    }

    private void UpdateProjectileFocusTarget()
    {
        if (!isGunmanPhase || trackedProjectiles.Count == 0)
        {
            return;
        }

        Vector3 projectileCenter = Vector3.zero;
        int projectileCount = 0;
        for (int index = trackedProjectiles.Count - 1; index >= 0; index--)
        {
            BulletController projectile = trackedProjectiles[index];
            if (projectile == null || !projectile.IsFlying || !projectile.gameObject.activeInHierarchy)
            {
                trackedProjectiles.RemoveAt(index);
                continue;
            }

            projectileCenter += projectile.transform.position;
            projectileCount++;
        }

        if (projectileCount == 0)
        {
            return;
        }

        SetFocusTarget(
            projectileCenter / projectileCount,
            projectileFocusPositionWeight,
            projectileMaxFocusOffset,
            projectileFocusTransitionDuration);
    }

    private void SetFocusTarget(
        Vector3 focusCenter,
        float positionWeight,
        float maxOffset,
        float nextTransitionDuration)
    {
        if (cameraParent == null)
        {
            return;
        }

        Vector3 baselineFocusPoint = GetBaselineFocusPoint(focusCenter.y);
        Vector3 worldOffset = Vector3.ProjectOnPlane(focusCenter - baselineFocusPoint, Vector3.up);
        worldOffset *= positionWeight;
        worldOffset = Vector3.ClampMagnitude(worldOffset, maxOffset);

        targetLocalPosition = baselineLocalPosition + cameraParent.InverseTransformVector(worldOffset);

        Vector3 desiredWorldPosition = cameraParent.TransformPoint(targetLocalPosition);
        Vector3 lookDirection = focusCenter + Vector3.up * focusLookHeight - desiredWorldPosition;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredWorldRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            targetLocalRotation = Quaternion.Inverse(cameraParent.rotation) * desiredWorldRotation;
        }

        targetLensSize = GetZoomedLensSize();
        transitionDuration = nextTransitionDuration;
    }

    private static bool ContainsGunman(IReadOnlyList<CombatPieceSnapshot> pieces)
    {
        foreach (CombatPieceSnapshot piece in pieces)
        {
            if (piece.PieceType == PieceType.Gunman)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetBaselineFocusPoint(float focusHeight)
    {
        Vector3 baselineWorldPosition = cameraParent.TransformPoint(baselineLocalPosition);
        Quaternion baselineWorldRotation = cameraParent.rotation * baselineLocalRotation;
        Vector3 forward = baselineWorldRotation * Vector3.forward;

        if (Mathf.Abs(forward.y) > 0.0001f)
        {
            float distance = (focusHeight - baselineWorldPosition.y) / forward.y;
            if (distance > 0f)
            {
                return baselineWorldPosition + forward * distance;
            }
        }

        return baselineWorldPosition + Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
    }

    private float GetZoomedLensSize()
    {
        return baselineWasOrthographic
            ? Mathf.Max(0.01f, baselineOrthographicSize * zoomMultiplier)
            : Mathf.Clamp(baselineFieldOfView * zoomMultiplier, 1f, 179f);
    }

    private void AddFocusPiece(PieceBase piece)
    {
        if (piece != null)
        {
            focusPieces.Add(piece);
        }
    }

    private void ApplyAnchorWithShake()
    {
        Vector3 positionOffset = Vector3.zero;
        Vector3 rotationOffset = Vector3.zero;
        float lensPunch = 0f;

        for (int index = shakeImpulses.Count - 1; index >= 0; index--)
        {
            CameraShakeImpulse impulse = shakeImpulses[index];
            impulse.Elapsed += Time.unscaledDeltaTime;
            if (impulse.Elapsed >= impulse.Duration)
            {
                shakeImpulses.RemoveAt(index);
                continue;
            }

            shakeImpulses[index] = impulse;
            float normalizedTime = impulse.Elapsed / impulse.Duration;
            float envelope = 1f - normalizedTime;
            float phase = normalizedTime * Mathf.PI * 3f + impulse.Sequence * 1.6180339f;
            float primaryWave = Mathf.Sin(phase) * envelope;
            float secondaryWave = Mathf.Cos(phase * 1.37f) * envelope;
            Vector3 lateral = Vector3.Cross(Vector3.forward, impulse.LocalDirection);
            if (lateral.sqrMagnitude <= 0.0001f)
            {
                lateral = Vector3.up;
            }
            lateral.Normalize();

            positionOffset += (impulse.LocalDirection * primaryWave + lateral * secondaryWave * 0.35f)
                * impulse.PositionAmplitude;
            rotationOffset += new Vector3(-impulse.LocalDirection.y, impulse.LocalDirection.x, secondaryWave * 0.35f)
                * (primaryWave >= 0f ? 1f : -1f) * impulse.RotationAmplitude * envelope;
            lensPunch += impulse.LensPunch * envelope * envelope;
        }

        positionOffset = Vector3.ClampMagnitude(positionOffset, maxShakePositionOffset);
        rotationOffset = Vector3.ClampMagnitude(rotationOffset, maxShakeRotationOffset);
        lensPunch = Mathf.Min(lensPunch, maxLensPunch);

        cameraTransform.localPosition = smoothAnchorLocalPosition + positionOffset;
        cameraTransform.localRotation = smoothAnchorLocalRotation * Quaternion.Euler(rotationOffset);
        float finalLensSize = Mathf.Max(0.01f, smoothAnchorLensSize - lensPunch);
        if (baselineWasOrthographic)
        {
            presentedCamera.orthographicSize = finalLensSize;
        }
        else
        {
            presentedCamera.fieldOfView = Mathf.Clamp(finalLensSize, 1f, 179f);
        }
    }

    private void GetImpactProfile(
        HitImpactAttackType attackType,
        out float duration,
        out float positionAmplitude,
        out float rotationAmplitude,
        out float lensPunch)
    {
        switch (attackType)
        {
            case HitImpactAttackType.Blunt:
                duration = bluntShakeDuration;
                positionAmplitude = bluntPositionAmplitude;
                rotationAmplitude = bluntRotationAmplitude;
                lensPunch = 0f;
                break;
            case HitImpactAttackType.Projectile:
                duration = projectileShakeDuration;
                positionAmplitude = projectilePositionAmplitude;
                rotationAmplitude = projectileRotationAmplitude;
                lensPunch = projectileLensPunch;
                break;
            default:
                duration = slashShakeDuration;
                positionAmplitude = slashPositionAmplitude;
                rotationAmplitude = slashRotationAmplitude;
                lensPunch = 0f;
                break;
        }
    }

    private struct CameraShakeImpulse
    {
        public readonly Vector3 LocalDirection;
        public readonly float Duration;
        public readonly float PositionAmplitude;
        public readonly float RotationAmplitude;
        public readonly float LensPunch;
        public readonly uint Sequence;
        public float Elapsed;

        public CameraShakeImpulse(
            Vector3 localDirection,
            float duration,
            float positionAmplitude,
            float rotationAmplitude,
            float lensPunch,
            uint sequence)
        {
            LocalDirection = localDirection;
            Duration = Mathf.Max(0.01f, duration);
            PositionAmplitude = positionAmplitude;
            RotationAmplitude = rotationAmplitude;
            LensPunch = lensPunch;
            Sequence = sequence;
            Elapsed = 0f;
        }
    }

    private void WarnCameraUnavailable(string message)
    {
        if (warnedAboutUnavailableCamera)
        {
            return;
        }

        warnedAboutUnavailableCamera = true;
        Debug.LogWarning(message, this);
    }
}
