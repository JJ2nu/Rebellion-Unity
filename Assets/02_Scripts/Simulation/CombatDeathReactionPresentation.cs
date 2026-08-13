using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 판정과 기물 루트 Transform을 건드리지 않고, 사망 순간의 모델 반동만 표현한다.
/// Animator가 붙은 PieceBase 루트의 실제 렌더 모델 자식만 오프셋하므로 그리드/충돌/루트 모션은 유지된다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatDeathReactionPresentation : MonoBehaviour
{
    [Header("Blunt Death Reaction")]
    [SerializeField, Min(0.01f)] private float bluntDuration = 0.24f;
    [SerializeField, Min(0f)] private float bluntPositionDistance = 0.25f;
    [SerializeField, Min(0f)] private float bluntVerticalLift = 0.025f;
    [SerializeField, Min(0f)] private float bluntRotationAngle = 12f;

    [Header("Slash Death Reaction")]
    [SerializeField, Min(0.01f)] private float slashDuration = 0.18f;
    [SerializeField, Min(0f)] private float slashPositionDistance = 0.12f;
    [SerializeField, Min(0f)] private float slashVerticalLift = 0.012f;
    [SerializeField, Min(0f)] private float slashRotationAngle = 16f;

    [Header("Projectile Death Reaction")]
    [SerializeField, Min(0.01f)] private float projectileDuration = 0.15f;
    [SerializeField, Min(0f)] private float projectilePositionDistance = 0.10f;
    [SerializeField, Min(0f)] private float projectileVerticalLift = 0.008f;
    [SerializeField, Min(0f)] private float projectileRotationAngle = 8f;

    private readonly Dictionary<PieceBase, ReactionState> activeReactions = new();

    /// <summary>
    /// PieceDied 직후 호출한다. 최대 반동을 즉시 적용하고, Time.deltaTime 기준으로 감쇠하므로
    /// 히트스톱 중에는 해당 자세가 거의 유지된다.
    /// </summary>
    public void Play(
        PieceBase victim,
        Vector3 hitPoint,
        Vector3 impactDirection,
        HitImpactAttackType attackType)
    {
        if (victim == null)
        {
            return;
        }

        HumanoidRagdollController ragdoll = victim.GetComponent<HumanoidRagdollController>();
        Restore(victim);

        if (ragdoll != null)
        {
            // AttackHitbox가 사망 처리 전에 전달하는 것이 정상 경로다.
            // 에디터 테스트처럼 공격 판정을 거치지 않는 경로만 여기서 보완한다.
            if (!ragdoll.HasPendingImpact && !ragdoll.IsRagdollActive)
            {
                ragdoll.SetPendingImpact(hitPoint, impactDirection, attackType);
            }

            // 래그돌 대상은 사망 모션과 물리 충격 자체가 반동을 표현한다.
            // 별도의 시각 루트 오프셋을 병행하면 동적 전환 뒤 부모 Transform이
            // 다시 움직여 전신에 가짜 속도와 위쪽 반발을 만들 수 있다.
            return;
        }

        Transform visualRoot = FindVisualRoot(victim);
        if (visualRoot == null || visualRoot.parent == null)
        {
            return;
        }

        ReactionProfile profile = GetProfile(attackType);
        if (profile.Duration <= 0f)
        {
            return;
        }

        Vector3 localImpactDirection = visualRoot.parent.InverseTransformDirection(impactDirection);
        localImpactDirection.y = 0f;
        if (localImpactDirection.sqrMagnitude <= 0.0001f)
        {
            localImpactDirection = Vector3.forward;
        }
        else
        {
            localImpactDirection.Normalize();
        }

        var state = new ReactionState(
            visualRoot,
            visualRoot.localPosition,
            visualRoot.localRotation,
            localImpactDirection,
            profile);
        activeReactions.Add(victim, state);

        // HitConfirmed에서 히트스톱을 건 뒤 바로 PieceDied가 호출된다.
        // 다음 LateUpdate까지 기다리지 않아 첫 정지 프레임부터 충격 자세가 보이게 한다.
        ApplyReaction(state, 1f);
    }

    /// <summary>
    /// 재시작/시뮬레이션 초기화/비활성화 시 누적된 시각 오프셋을 즉시 원복한다.
    /// </summary>
    public void RestoreImmediately()
    {
        foreach (ReactionState state in activeReactions.Values)
        {
            Restore(state);
        }

        activeReactions.Clear();
    }

    private void LateUpdate()
    {
        if (activeReactions.Count == 0)
        {
            return;
        }

        List<PieceBase> completedPieces = null;
        foreach (KeyValuePair<PieceBase, ReactionState> pair in activeReactions)
        {
            ReactionState state = pair.Value;
            if (state.VisualRoot == null)
            {
                (completedPieces ??= new List<PieceBase>()).Add(pair.Key);
                continue;
            }

            state.Elapsed += Time.deltaTime;
            float normalizedTime = state.Profile.Duration <= 0f
                ? 1f
                : Mathf.Clamp01(state.Elapsed / state.Profile.Duration);
            ApplyReaction(state, EvaluateEnvelope(normalizedTime));

            if (normalizedTime >= 1f)
            {
                Restore(state);
                (completedPieces ??= new List<PieceBase>()).Add(pair.Key);
            }
        }

        if (completedPieces == null)
        {
            return;
        }

        foreach (PieceBase piece in completedPieces)
        {
            activeReactions.Remove(piece);
        }
    }

    private void OnDisable()
    {
        RestoreImmediately();
    }

    private ReactionProfile GetProfile(HitImpactAttackType attackType)
    {
        return attackType switch
        {
            HitImpactAttackType.Blunt => new ReactionProfile(
                bluntDuration,
                bluntPositionDistance,
                bluntVerticalLift,
                bluntRotationAngle),
            HitImpactAttackType.Projectile => new ReactionProfile(
                projectileDuration,
                projectilePositionDistance,
                projectileVerticalLift,
                projectileRotationAngle),
            _ => new ReactionProfile(
                slashDuration,
                slashPositionDistance,
                slashVerticalLift,
                slashRotationAngle),
        };
    }

    private void Restore(PieceBase piece)
    {
        if (piece != null && activeReactions.TryGetValue(piece, out ReactionState state))
        {
            Restore(state);
            activeReactions.Remove(piece);
        }
    }

    private static void Restore(ReactionState state)
    {
        if (state.VisualRoot != null)
        {
            state.VisualRoot.SetLocalPositionAndRotation(state.BaseLocalPosition, state.BaseLocalRotation);
        }
    }

    private static void ApplyReaction(ReactionState state, float intensity)
    {
        state.VisualRoot.localPosition = state.BaseLocalPosition
            + state.LocalImpactDirection * (state.Profile.PositionDistance * intensity)
            + Vector3.up * (state.Profile.VerticalLift * intensity);

        Vector3 rotationAxis = Vector3.Cross(Vector3.up, state.LocalImpactDirection);
        if (rotationAxis.sqrMagnitude > 0.0001f)
        {
            state.VisualRoot.localRotation = state.BaseLocalRotation
                * Quaternion.AngleAxis(state.Profile.RotationAngle * intensity, rotationAxis.normalized);
        }
    }

    private static float EvaluateEnvelope(float normalizedTime)
    {
        // 70% 동안 peak를 유지한다. Stage의 timeScale 0.1 / 0.5초 히트스톱에서는
        // scaled 시간이 0.05초만 흐르므로 반동이 정지 중에 사라지지 않는다.
        const float holdFraction = 0.70f;
        if (normalizedTime <= holdFraction)
        {
            return 1f;
        }

        float fadeTime = Mathf.InverseLerp(holdFraction, 1f, normalizedTime);
        return 1f - Mathf.SmoothStep(0f, 1f, fadeTime);
    }

    private static Transform FindVisualRoot(PieceBase piece)
    {
        foreach (Transform child in piece.transform)
        {
            if (child.name == "HUD" || child.name == "DirectionIndicator" || child.GetComponent<AttackHitbox>() != null)
            {
                continue;
            }

            if (child.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            {
                return child;
            }
        }

        return null;
    }

    private sealed class ReactionState
    {
        public Transform VisualRoot { get; }
        public Vector3 BaseLocalPosition { get; }
        public Quaternion BaseLocalRotation { get; }
        public Vector3 LocalImpactDirection { get; }
        public ReactionProfile Profile { get; }
        public float Elapsed { get; set; }

        public ReactionState(
            Transform visualRoot,
            Vector3 baseLocalPosition,
            Quaternion baseLocalRotation,
            Vector3 localImpactDirection,
            ReactionProfile profile)
        {
            VisualRoot = visualRoot;
            BaseLocalPosition = baseLocalPosition;
            BaseLocalRotation = baseLocalRotation;
            LocalImpactDirection = localImpactDirection;
            Profile = profile;
        }
    }

    private readonly struct ReactionProfile
    {
        public float Duration { get; }
        public float PositionDistance { get; }
        public float VerticalLift { get; }
        public float RotationAngle { get; }

        public ReactionProfile(float duration, float positionDistance, float verticalLift, float rotationAngle)
        {
            Duration = duration;
            PositionDistance = positionDistance;
            VerticalLift = verticalLift;
            RotationAngle = rotationAngle;
        }
    }
}
