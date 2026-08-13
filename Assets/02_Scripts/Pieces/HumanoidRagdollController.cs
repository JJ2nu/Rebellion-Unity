using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mixamo Humanoid 전투원의 현재 사망 자세를 물리 본으로 넘긴다.
/// 프리팹마다 물리 컴포넌트를 복제하지 않고 Avatar의 HumanBodyBones를 기준으로 런타임에 구성한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class HumanoidRagdollController : MonoBehaviour
{
    [Header("Animation To Physics")]
    [SerializeField, Range(0.1f, 0.8f)] private float transitionNormalizedTime = 0.22f;
    [SerializeField, Range(0f, 0.15f)] private float transitionPoseVariation = 0.04f;
    [SerializeField, Range(0f, 0.15f)] private float fallVariantPoseSpacing = 0.06f;
    [SerializeField, Min(0.1f)] private float transitionFallbackSeconds = 1.15f;
    [SerializeField, Range(0f, 1f)] private float inheritedAnimationVelocity = 0.18f;

    [Header("Impact")]
    [SerializeField, Min(0f)] private float bluntImpulse = 2.4f;
    [SerializeField, Min(0f)] private float slashImpulse = 1.35f;
    [SerializeField, Min(0f)] private float projectileImpulse = 2f;
    [SerializeField, Min(0f)] private float directionalPushMultiplier = 1.4f;
    [SerializeField, Min(0f)] private float poseVariationTorque = 0.55f;
    [SerializeField, Min(0f)] private float poseVariationYawTorque = 0.22f;

    [Header("Stability")]
    [SerializeField, Min(0f)] private float linearDrag = 0.35f;
    [SerializeField, Min(0f)] private float angularDrag = 1.1f;
    [SerializeField, Min(0f)] private float maxAngularVelocity = 10f;
    [SerializeField, Min(0f)] private float settlingDelay = 0.65f;
    [SerializeField, Min(0f)] private float settlingLinearDrag = 1.2f;
    [SerializeField, Min(0f)] private float settlingAngularDrag = 3f;
    [SerializeField, Min(0f)] private float sleepLinearSpeed = 0.12f;
    [SerializeField, Min(0f)] private float sleepAngularSpeed = 0.6f;
    [SerializeField, Min(0f)] private float stableTimeBeforeSleep = 0.18f;
    [SerializeField, Min(0.1f)] private float forceSleepAfter = 2.2f;

    [Header("Collision Handoff")]
    [SerializeField, Min(0f)] private float initialPenetrationClearance = 0.01f;
    [SerializeField, Min(0f)] private float maxInitialPenetrationCorrection = 0.15f;

    private readonly List<RagdollPart> parts = new();
    private readonly Dictionary<Collider, RagdollPart> ownedColliders = new();
    private Animator animator;
    private Coroutine transitionCoroutine;
    private Coroutine settlingCoroutine;
    private bool isRagdollActive;
    private bool hasPendingImpact;
    private Vector3 pendingImpactDirection;
    private Vector3 pendingHitPoint;
    private HitImpactAttackType pendingAttackType;
    private float activeTransitionNormalizedTime;
    private int activeFallVariant;
    private RagdollPart hipsPart;
    private RagdollPart chestPart;
    private RagdollPart headPart;
    private Transform skeletonRoot;
    private Vector3 initialSkeletonRootLocalPosition;
    private Quaternion initialSkeletonRootLocalRotation;

    public bool IsRagdollActive => isRagdollActive;
    public bool HasPendingImpact => hasPendingImpact;
    public int ActiveFallVariant => activeFallVariant;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            enabled = false;
            return;
        }

        BuildRagdoll();
        SetAnimationMode(true);
    }

    private void LateUpdate()
    {
        if (isRagdollActive)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        foreach (RagdollPart part in parts)
        {
            Vector3 currentPosition = part.Bone.position;
            if (deltaTime > 0.0001f && part.HasPreviousPosition)
            {
                part.SampledVelocity = (currentPosition - part.PreviousPosition) / deltaTime;
            }

            part.PreviousPosition = currentPosition;
            part.HasPreviousPosition = true;
        }
    }

    public void BeginDeathTransition()
    {
        if (!isActiveAndEnabled || parts.Count == 0)
        {
            return;
        }

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        SelectFallVariant();
        UpdateActiveTransitionTime();
        transitionCoroutine = StartCoroutine(TransitionAfterDeathPose());
    }

    private void UpdateActiveTransitionTime()
    {
        bool useDirectionalBluntFall = hasPendingImpact
            && pendingAttackType == HitImpactAttackType.Blunt;
        float subtleVariation = Mathf.Sin(GetInstanceID() * 12.9898f)
            * transitionPoseVariation
            * 0.25f;
        float variantPoseOffset = useDirectionalBluntFall
            ? 0f
            : (activeFallVariant - 1) * fallVariantPoseSpacing;
        activeTransitionNormalizedTime = Mathf.Clamp(
            transitionNormalizedTime + variantPoseOffset + subtleVariation,
            0.1f,
            0.8f);
    }

    public void SetPendingImpact(Vector3 hitPoint, Vector3 impactDirection, HitImpactAttackType attackType)
    {
        pendingHitPoint = hitPoint;
        pendingImpactDirection = impactDirection.sqrMagnitude > 0.0001f
            ? impactDirection.normalized
            : transform.forward;
        pendingAttackType = attackType;
        hasPendingImpact = true;

        if (isRagdollActive)
        {
            ApplyPendingImpact();
        }
        else if (transitionCoroutine != null)
        {
            // 에디터/디버그 경로처럼 사망 이벤트에서 뒤늦게 전달되더라도
            // 이미 진행 중인 전환이 공격 종류에 맞는 시점을 사용하게 한다.
            UpdateActiveTransitionTime();
        }
    }

    public void ResetToAnimationPose()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        if (settlingCoroutine != null)
        {
            StopCoroutine(settlingCoroutine);
            settlingCoroutine = null;
        }

        isRagdollActive = false;
        hasPendingImpact = false;

        foreach (RagdollPart part in parts)
        {
            Rigidbody body = part.Body;
            body.isKinematic = true;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.linearDamping = linearDrag;
            body.angularDamping = angularDrag;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            part.Collider.enabled = false;
        }

        if (animator != null)
        {
            animator.enabled = true;
        }

        RestoreSkeletonRootTransform();

        foreach (RagdollPart part in parts)
        {
            part.Bone.SetLocalPositionAndRotation(part.InitialLocalPosition, part.InitialLocalRotation);
            part.PreviousPosition = part.Bone.position;
            part.SampledVelocity = Vector3.zero;
            part.HasPreviousPosition = true;
        }
    }

    public bool OwnsCollider(Collider candidate)
    {
        return candidate != null && ownedColliders.ContainsKey(candidate);
    }

    public void EnsureAnimationCollisionState()
    {
        if (isRagdollActive)
        {
            return;
        }

        foreach (RagdollPart part in parts)
        {
            part.Collider.enabled = false;
        }
    }

    private IEnumerator TransitionAfterDeathPose()
    {
        float elapsed = 0f;
        bool enteredDeathState = false;

        while (!isRagdollActive && elapsed < transitionFallbackSeconds)
        {
            if (animator == null || !animator.enabled)
            {
                break;
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Hit"))
            {
                enteredDeathState = true;
                if (state.normalizedTime >= activeTransitionNormalizedTime)
                {
                    break;
                }
            }
            else if (enteredDeathState)
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        transitionCoroutine = null;
        ActivateRagdoll();
    }

    private void ActivateRagdoll()
    {
        if (isRagdollActive || parts.Count == 0)
        {
            return;
        }

        isRagdollActive = true;

        // Animator를 끄기 직전 자세를 그대로 유지한 채 물리 제어권만 넘긴다.
        animator.applyRootMotion = false;
        animator.enabled = false;

        Vector3 inheritedVelocity = Vector3.zero;
        RagdollPart hips = parts[0];
        if (hips != null)
        {
            inheritedVelocity = Vector3.ClampMagnitude(
                hips.SampledVelocity * inheritedAnimationVelocity,
                1.5f);
            // 사망 모션의 위쪽 루트/골반 속도도 래그돌에 넘기지 않는다.
            inheritedVelocity.y = Mathf.Min(0f, inheritedVelocity.y);
        }

        // 동적 전환 전에 콜라이더만 켜고, 사망 모션에서 바닥 안으로 들어간
        // 발/다리 깊이를 먼저 해소한다. 그렇지 않으면 PhysX가 첫 프레임에
        // 전신을 위로 밀어내면서 외력이 없어도 캐릭터가 튀어 오른다.
        foreach (RagdollPart part in parts)
        {
            part.Collider.enabled = true;
        }

        IgnoreGameplayPieceCollisions();
        Physics.SyncTransforms();
        ResolveInitialEnvironmentPenetration();

        foreach (RagdollPart part in parts)
        {
            part.Body.isKinematic = false;
            part.Body.useGravity = true;
            part.Body.linearDamping = linearDrag;
            part.Body.angularDamping = angularDrag;
            part.Body.collisionDetectionMode = part.IsCore
                ? CollisionDetectionMode.Continuous
                : CollisionDetectionMode.Discrete;
            // 본별 애니메이션 속도를 각각 넘기면 팔과 다리가 서로 다른 방향으로 튀며
            // 관절에 에너지가 계속 남는다. 골반의 낮은 공통 속도만 전신에 넘긴다.
            part.Body.linearVelocity = inheritedVelocity;
            part.Body.angularVelocity = Vector3.zero;
        }

        ApplyPendingImpact();
        settlingCoroutine = StartCoroutine(SettleRagdoll());
    }

    private void IgnoreGameplayPieceCollisions()
    {
        PieceBase owner = GetComponent<PieceBase>();
        PieceBase[] scenePieces = FindObjectsByType<PieceBase>(FindObjectsSortMode.None);
        foreach (PieceBase otherPiece in scenePieces)
        {
            if (otherPiece == null || otherPiece == owner)
            {
                continue;
            }

            // 기물의 루트/선택 콜라이더는 Transform으로 직접 이동한다.
            // 공격자의 전진 중 이 콜라이더가 막 활성화된 물리 본을 관통하면
            // PhysX가 큰 분리 속도를 주므로, 래그돌은 환경과만 충돌하게 한다.
            foreach (Collider gameplayCollider in otherPiece.GetComponentsInChildren<Collider>(true))
            {
                if (gameplayCollider == null)
                {
                    continue;
                }

                foreach (RagdollPart part in parts)
                {
                    Physics.IgnoreCollision(part.Collider, gameplayCollider, true);
                }
            }
        }
    }

    private void ResolveInitialEnvironmentPenetration()
    {
        if (skeletonRoot == null || maxInitialPenetrationCorrection <= 0f)
        {
            return;
        }

        const int maxIterations = 2;
        float totalCorrection = 0f;
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            float remainingCorrection = maxInitialPenetrationCorrection - totalCorrection;
            if (remainingCorrection <= 0.0001f)
            {
                break;
            }

            float requiredUpwardCorrection = FindRequiredUpwardCorrection(remainingCorrection);
            if (requiredUpwardCorrection <= 0.0001f)
            {
                break;
            }

            float correction = Mathf.Min(requiredUpwardCorrection, remainingCorrection);
            skeletonRoot.position += Vector3.up * correction;
            totalCorrection += correction;
            Physics.SyncTransforms();
        }
    }

    private float FindRequiredUpwardCorrection(float searchDistance)
    {
        float requiredCorrection = 0f;
        Vector3 padding = Vector3.one * Mathf.Max(initialPenetrationClearance, 0.005f);

        foreach (RagdollPart part in parts)
        {
            Bounds bounds = part.Collider.bounds;
            Collider[] overlaps = Physics.OverlapBox(
                bounds.center,
                bounds.extents + padding,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);

            foreach (Collider other in overlaps)
            {
                if (other == null
                    || other == part.Collider
                    || ownedColliders.ContainsKey(other)
                    || other.transform.IsChildOf(transform)
                    || other.GetComponentInParent<PieceBase>() != null)
                {
                    continue;
                }

                if (!Physics.ComputePenetration(
                        part.Collider,
                        part.Collider.transform.position,
                        part.Collider.transform.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out Vector3 separationDirection,
                        out float separationDistance))
                {
                    continue;
                }

                // 벽이나 오브제 옆면은 수직 보정 대상으로 삼지 않는다.
                // 위쪽을 향하는 접촉만 바닥 침투로 판정한다.
                if (separationDirection.y < 0.6f)
                {
                    continue;
                }

                float upwardDistance = separationDistance / separationDirection.y
                    + initialPenetrationClearance;
                requiredCorrection = Mathf.Max(
                    requiredCorrection,
                    Mathf.Min(upwardDistance, searchDistance));
            }
        }

        return requiredCorrection;
    }

    private void SelectFallVariant()
    {
        PieceBase piece = GetComponent<PieceBase>();
        int patternSeed = piece != null
            ? piece.GridX + piece.GridY * 2
            : GetInstanceID();
        activeFallVariant = PositiveModulo(patternSeed, 3);
    }

    private static int PositiveModulo(int value, int divisor)
    {
        int remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }

    private void ApplyPendingImpact()
    {
        if (!hasPendingImpact || !isRagdollActive)
        {
            return;
        }

        hasPendingImpact = false;
        RagdollPart closest = null;
        float closestDistance = float.MaxValue;
        foreach (RagdollPart part in parts)
        {
            if (!part.IsCore)
            {
                continue;
            }

            float distance = (part.Collider.ClosestPoint(pendingHitPoint) - pendingHitPoint).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = part;
            }
        }

        if (closest == null)
        {
            return;
        }

        // 연속 사격에서 모두 같은 중심축으로 접히지 않도록 투사체의 충격 중심만
        // 골반/가슴 사이에서 바꾼다. 머리 피격은 원래 피격 부위를 유지한다.
        RagdollPart impactPart = closest;
        if (pendingAttackType == HitImpactAttackType.Projectile && closest != headPart)
        {
            impactPart = activeFallVariant == 1
                ? hipsPart ?? closest
                : chestPart ?? closest;
        }
        else if (pendingAttackType == HitImpactAttackType.Blunt)
        {
            impactPart = chestPart ?? closest;
        }

        float impulse = pendingAttackType switch
        {
            HitImpactAttackType.Blunt => bluntImpulse,
            HitImpactAttackType.Projectile => projectileImpulse,
            _ => slashImpulse,
        };

        Vector3 horizontalDirection = Vector3.ProjectOnPlane(pendingImpactDirection, Vector3.up);
        if (horizontalDirection.sqrMagnitude <= 0.0001f)
        {
            horizontalDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }
        horizontalDirection.Normalize();
        Vector3 horizontalImpulse = horizontalDirection * (impulse * directionalPushMultiplier);

        // 가벼운 팔·다리나 먼 피격점에 힘을 가하면 과한 회전력 때문에 몸이 날아간다.
        // 중심부 질량에 수평 충격만 적용해 피격 벡터에 Y값이 있어도 위로 뜨지 않게 한다.
        impactPart.Body.AddForce(horizontalImpulse, ForceMode.Impulse);

        if (horizontalDirection.sqrMagnitude > 0.0001f && poseVariationTorque > 0f)
        {
            horizontalDirection.Normalize();
            Vector3 rollAxis = Vector3.Cross(Vector3.up, horizontalDirection).normalized;
            float hitSide = Vector3.Dot(pendingHitPoint - impactPart.Body.worldCenterOfMass, rollAxis);
            float fallbackSide = Mathf.Sin((GetInstanceID() + 17) * 7.173f) >= 0f ? 1f : -1f;
            float sideSign = Mathf.Abs(hitSide) > 0.03f ? Mathf.Sign(hitSide) : fallbackSide;

            RagdollPart torquePart = chestPart ?? impactPart;
            if (pendingAttackType == HitImpactAttackType.Blunt)
            {
                // 주먹은 좌우 변형보다 타격 진행 방향을 우선한다.
                // rollAxis의 양의 회전은 상체를 수평 충격 벡터 쪽으로 눕힌다.
                torquePart.Body.AddTorque(
                    rollAxis * (poseVariationTorque * 0.65f),
                    ForceMode.Impulse);
                return;
            }

            // 0은 피격점에 따른 기본 낙상, 1/2는 서로 반대 방향으로 몸통이 접히는 변형이다.
            // 팔다리에 직접 힘을 주지 않아 안정화 이후 파닥임은 다시 만들지 않는다.
            float rollScale = 0.45f;
            float yawSign = 0f;
            if (activeFallVariant == 1)
            {
                sideSign = -1f;
                rollScale = 1.15f;
                yawSign = 1f;
            }
            else if (activeFallVariant == 2)
            {
                sideSign = 1f;
                rollScale = 1f;
                yawSign = -1f;
            }

            torquePart.Body.AddTorque(
                rollAxis * (poseVariationTorque * rollScale * sideSign),
                ForceMode.Impulse);
            if (yawSign != 0f && poseVariationYawTorque > 0f)
            {
                torquePart.Body.AddTorque(
                    Vector3.up * (poseVariationYawTorque * yawSign),
                    ForceMode.Impulse);
            }
        }
    }

    private IEnumerator SettleRagdoll()
    {
        float elapsed = 0f;
        while (elapsed < settlingDelay)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        foreach (RagdollPart part in parts)
        {
            part.Body.linearDamping = settlingLinearDrag;
            part.Body.angularDamping = settlingAngularDrag;
        }

        float stableTime = 0f;
        float settleElapsed = 0f;
        float linearThresholdSqr = sleepLinearSpeed * sleepLinearSpeed;
        float angularThresholdSqr = sleepAngularSpeed * sleepAngularSpeed;

        while (isRagdollActive && settleElapsed < forceSleepAfter)
        {
            bool isStable = true;
            foreach (RagdollPart part in parts)
            {
                if (part.Body.linearVelocity.sqrMagnitude > linearThresholdSqr
                    || part.Body.angularVelocity.sqrMagnitude > angularThresholdSqr)
                {
                    isStable = false;
                    break;
                }
            }

            stableTime = isStable ? stableTime + Time.deltaTime : 0f;
            if (stableTime >= stableTimeBeforeSleep)
            {
                break;
            }

            settleElapsed += Time.deltaTime;
            yield return null;
        }

        if (isRagdollActive)
        {
            foreach (RagdollPart part in parts)
            {
                part.Body.linearVelocity = Vector3.zero;
                part.Body.angularVelocity = Vector3.zero;
                part.Body.useGravity = false;
                part.Body.isKinematic = true;
            }
        }

        settlingCoroutine = null;
    }

    private void BuildRagdoll()
    {
        hipsPart = AddBoxPart(HumanBodyBones.Hips, 12f, new Vector3(0.34f, 0.22f, 0.24f), true, Vector3.up);
        chestPart = AddBoxPart(HumanBodyBones.Chest, 14f, new Vector3(0.40f, 0.32f, 0.25f), true, Vector3.up);
        headPart = AddCapsulePart(HumanBodyBones.Head, null, 5f, 0.11f, 0.24f, true);

        RagdollPart leftUpperArm = AddCapsulePart(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 3f, 0.075f, 0f, false);
        RagdollPart leftLowerArm = AddCapsulePart(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 2f, 0.065f, 0f, false);
        RagdollPart rightUpperArm = AddCapsulePart(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 3f, 0.075f, 0f, false);
        RagdollPart rightLowerArm = AddCapsulePart(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 2f, 0.065f, 0f, false);

        RagdollPart leftUpperLeg = AddCapsulePart(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 8f, 0.11f, 0f, false);
        RagdollPart leftLowerLeg = AddCapsulePart(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 5f, 0.09f, 0f, false);
        RagdollPart leftFoot = AddBoxPart(HumanBodyBones.LeftFoot, 1f, new Vector3(0.16f, 0.12f, 0.28f), false, Vector3.forward);
        RagdollPart rightUpperLeg = AddCapsulePart(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 8f, 0.11f, 0f, false);
        RagdollPart rightLowerLeg = AddCapsulePart(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 5f, 0.09f, 0f, false);
        RagdollPart rightFoot = AddBoxPart(HumanBodyBones.RightFoot, 1f, new Vector3(0.16f, 0.12f, 0.28f), false, Vector3.forward);

        CacheSkeletonRootTransform();

        // 관절별 가동 범위를 사람의 움직임에 가깝게 제한한다.
        Connect(chestPart, hipsPart, -20f, 20f, 20f, 15f);
        Connect(headPart, chestPart, -40f, 40f, 30f, 25f);
        Connect(leftUpperArm, chestPart, -45f, 60f, 80f, 50f);
        Connect(leftLowerArm, leftUpperArm, -5f, 135f, 8f, 8f);
        Connect(rightUpperArm, chestPart, -45f, 60f, 80f, 50f);
        Connect(rightLowerArm, rightUpperArm, -5f, 135f, 8f, 8f);
        Connect(leftUpperLeg, hipsPart, -30f, 45f, 55f, 35f);
        Connect(leftLowerLeg, leftUpperLeg, -5f, 130f, 7f, 7f);
        Connect(leftFoot, leftLowerLeg, -30f, 20f, 12f, 10f);
        Connect(rightUpperLeg, hipsPart, -30f, 45f, 55f, 35f);
        Connect(rightLowerLeg, rightUpperLeg, -5f, 130f, 7f, 7f);
        Connect(rightFoot, rightLowerLeg, -30f, 20f, 12f, 10f);

        // 저폴리 캐릭터에서는 자체 충돌보다 환경 충돌이 중요하다. 자체 충돌을 끄면 관절 폭주도 줄어든다.
        for (int first = 0; first < parts.Count; first++)
        {
            for (int second = first + 1; second < parts.Count; second++)
            {
                Physics.IgnoreCollision(parts[first].Collider, parts[second].Collider, true);
            }
        }

        // 루트 선택 콜라이더와 공격 판정은 애니메이션/게임 규칙용이다.
        // 물리 본과 충돌시키지 않아 손·무기가 몸을 밀어내는 현상을 방지한다.
        foreach (Collider externalCollider in GetComponentsInChildren<Collider>(true))
        {
            if (externalCollider == null || ownedColliders.ContainsKey(externalCollider))
            {
                continue;
            }

            foreach (RagdollPart part in parts)
            {
                Physics.IgnoreCollision(part.Collider, externalCollider, true);
            }
        }
    }


    private RagdollPart AddBoxPart(
        HumanBodyBones boneId,
        float mass,
        Vector3 size,
        bool isCore,
        Vector3 jointAxis)
    {
        Transform bone = animator.GetBoneTransform(boneId);
        if (bone == null)
        {
            return null;
        }

        BoxCollider collider = bone.gameObject.AddComponent<BoxCollider>();
        collider.size = size;
        return RegisterPart(bone, collider, mass, isCore, jointAxis);
    }

    private RagdollPart AddCapsulePart(
        HumanBodyBones boneId,
        HumanBodyBones? childBoneId,
        float mass,
        float radius,
        float fixedHeight,
        bool isCore)
    {
        Transform bone = animator.GetBoneTransform(boneId);
        if (bone == null)
        {
            return null;
        }

        CapsuleCollider collider = bone.gameObject.AddComponent<CapsuleCollider>();
        collider.direction = 1;
        collider.radius = radius;

        float height = fixedHeight;
        if (childBoneId.HasValue)
        {
            Transform child = animator.GetBoneTransform(childBoneId.Value);
            if (child != null)
            {
                Vector3 childLocalPosition = bone.InverseTransformPoint(child.position);
                height = Mathf.Max(radius * 2f, childLocalPosition.magnitude);
                collider.center = childLocalPosition * 0.5f;
                collider.direction = GetDominantAxis(childLocalPosition);
                collider.height = height;
                return RegisterPart(bone, collider, mass, isCore, childLocalPosition.normalized);
            }
        }

        collider.height = Mathf.Max(radius * 2f, height);
        return RegisterPart(bone, collider, mass, isCore, Vector3.up);
    }

    private static int GetDominantAxis(Vector3 direction)
    {
        Vector3 absolute = new Vector3(
            Mathf.Abs(direction.x),
            Mathf.Abs(direction.y),
            Mathf.Abs(direction.z));
        if (absolute.x >= absolute.y && absolute.x >= absolute.z)
        {
            return 0;
        }

        return absolute.y >= absolute.z ? 1 : 2;
    }

    private RagdollPart RegisterPart(
        Transform bone,
        Collider collider,
        float mass,
        bool isCore,
        Vector3 jointAxis)
    {
        Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
        body.mass = mass;
        body.linearDamping = linearDrag;
        body.angularDamping = angularDrag;
        body.maxAngularVelocity = maxAngularVelocity;
        body.sleepThreshold = 0.02f;
        body.solverIterations = 12;
        body.solverVelocityIterations = 4;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.isKinematic = true;
        body.useGravity = false;

        var part = new RagdollPart(bone, body, collider, isCore, jointAxis);
        parts.Add(part);
        ownedColliders.Add(collider, part);
        return part;
    }

    private static void Connect(
        RagdollPart child,
        RagdollPart parent,
        float lowTwist,
        float highTwist,
        float swing1,
        float swing2)
    {
        if (child == null || parent == null)
        {
            return;
        }

        CharacterJoint joint = child.Bone.gameObject.AddComponent<CharacterJoint>();
        joint.connectedBody = parent.Body;
        joint.axis = child.JointAxis;
        joint.swingAxis = GetPerpendicularAxis(child.JointAxis);
        joint.enableCollision = false;
        joint.enableProjection = true;
        joint.projectionDistance = 0.05f;
        joint.projectionAngle = 15f;
        joint.lowTwistLimit = CreateLimit(lowTwist);
        joint.highTwistLimit = CreateLimit(highTwist);
        joint.swing1Limit = CreateLimit(swing1);
        joint.swing2Limit = CreateLimit(swing2);
    }

    private static Vector3 GetPerpendicularAxis(Vector3 axis)
    {
        Vector3 normalizedAxis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.right;
        Vector3 reference = Mathf.Abs(Vector3.Dot(normalizedAxis, Vector3.up)) < 0.9f
            ? Vector3.up
            : Vector3.forward;
        return Vector3.ProjectOnPlane(reference, normalizedAxis).normalized;
    }

    private static SoftJointLimit CreateLimit(float value)
    {
        var limit = new SoftJointLimit { limit = value, bounciness = 0f, contactDistance = 1f };
        return limit;
    }

    private void SetAnimationMode(bool restorePose)
    {
        isRagdollActive = false;
        if (restorePose)
        {
            RestoreSkeletonRootTransform();
        }

        foreach (RagdollPart part in parts)
        {
            part.Body.isKinematic = true;
            part.Body.useGravity = false;
            part.Collider.enabled = false;
            if (restorePose)
            {
                part.Bone.SetLocalPositionAndRotation(part.InitialLocalPosition, part.InitialLocalRotation);
            }
        }
    }

    private void CacheSkeletonRootTransform()
    {
        if (hipsPart == null)
        {
            return;
        }

        skeletonRoot = hipsPart.Bone;
        while (skeletonRoot.parent != null && skeletonRoot.parent != transform)
        {
            skeletonRoot = skeletonRoot.parent;
        }

        initialSkeletonRootLocalPosition = skeletonRoot.localPosition;
        initialSkeletonRootLocalRotation = skeletonRoot.localRotation;
    }

    private void RestoreSkeletonRootTransform()
    {
        if (skeletonRoot != null)
        {
            skeletonRoot.SetLocalPositionAndRotation(
                initialSkeletonRootLocalPosition,
                initialSkeletonRootLocalRotation);
        }
    }

    private sealed class RagdollPart
    {
        public Transform Bone { get; }
        public Rigidbody Body { get; }
        public Collider Collider { get; }
        public bool IsCore { get; }
        public Vector3 JointAxis { get; }
        public Vector3 InitialLocalPosition { get; }
        public Quaternion InitialLocalRotation { get; }
        public Vector3 PreviousPosition { get; set; }
        public Vector3 SampledVelocity { get; set; }
        public bool HasPreviousPosition { get; set; }

        public RagdollPart(
            Transform bone,
            Rigidbody body,
            Collider collider,
            bool isCore,
            Vector3 jointAxis)
        {
            Bone = bone;
            Body = body;
            Collider = collider;
            IsCore = isCore;
            JointAxis = jointAxis.sqrMagnitude > 0.0001f ? jointAxis.normalized : Vector3.right;
            InitialLocalPosition = bone.localPosition;
            InitialLocalRotation = bone.localRotation;
            PreviousPosition = bone.position;
            HasPreviousPosition = true;
        }
    }
}
