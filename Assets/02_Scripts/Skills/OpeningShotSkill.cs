using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 시작의 총성 스킬.
/// 시뮬레이션 시작 전, 플레이어가 적 한 명을 선택해 즉시 제거한다.
/// 선택이 완료되면 시뮬레이션이 시작된다.
/// </summary>
[CreateAssetMenu(menuName = "Skills/Opening Shot Skill")]

public class OpeningShotSkill : SkillBase
{
    public override string SkillName => "시작의 총성";
    public override string Description =>
        "시뮬레이션 시작 전, 적 한 명을 선택하여 즉시 제거한다.";

    public override SkillTiming Timing => SkillTiming.PreSimulation;
    [SerializeField] public PieceBase Target = null;
    public bool isTargetingMode = false;

    // 타겟팅 시작, 확정, 취소를 UI가 즉시 따라갈 수 있도록 상태 변경 시마다 알린다.
    public event Action TargetStateChanged;

    // 선택은 끝났고 선처치 대상이 남아 있는 상태다. Order 버튼 Deact 표시와 스코프 종료 기준으로 쓴다.
    public bool HasConfirmedTarget => Target != null && !isTargetingMode;

    /// <summary>
    /// 살아있는 적이 한 명 이상일 때만 발동 가능.
    /// </summary>
    public override bool CanExecute(IReadOnlyList<PieceBase> allPieces)
    {
        return allPieces.Any(p => p.Faction == Faction.Enemy && !p.IsDead);
    }

    public override IEnumerator TargetMode(SimulationController controller, IReadOnlyList<PieceBase> allPieces)
    {
        // 기존 타겟 표시를 지우고, UI가 스코프/버튼 상태를 갱신할 수 있도록 타겟팅 시작을 알린다.
        isTargetingMode = true;
        if (Target != null)
        {
            Target._isTargeted = false;
        }

        Target = null;
        NotifyTargetStateChanged();

        // 플레이어가 선택할 때까지 대기 (선택되거나, 모드가 종료될 때까지)
        yield return new WaitUntil(() => Target != null || !isTargetingMode);

        // 타겟 확정 또는 취소 후에는 타겟팅 UI를 닫도록 상태 변경을 알린다.
        isTargetingMode = false;
        NotifyTargetStateChanged();
    }

    public void ConfirmTarget(PieceBase target)
    {
        if (target == null)
        {
            return;
        }

        Target = target;
        Target._isTargeted = true;

        // ConfirmTarget은 SimulationController의 클릭 경로에서 호출되며, 즉시 타겟팅 모드를 종료한다.
        isTargetingMode = false;
        NotifyTargetStateChanged();
    }

    public override void ResetTarget()
    {
        // 확정 타겟을 해제할 때는 피스 위 타겟 표시도 함께 지워 다음 선택을 깨끗하게 시작한다.
        if (Target != null)
        {
            Target._isTargeted = false;
        }

        Target = null;
        isTargetingMode = false;
        NotifyTargetStateChanged();
    }

    public override void Execute(SimulationController controller, IReadOnlyList<PieceBase> allPieces)
    {
        if (Target != null)
        {
            Debug.Log($"[Simulation] Phase -1: Executing Pre-Simulation Skill on {Target.name}");
            bool wasAlive = !Target.IsDead;
            Target.Die();
            if (wasAlive && Target.IsDead)
            {
                // OpeningShot은 일반 공격의 피격 VFX 경로를 지나지 않으므로 처치가 확정된 위치에 혈흔을 직접 남긴다.
                StageManager.Instance?.PlayGroundBloodDecal(Target.transform.position);
            }

            // 미션 판정은 타겟 선택이 아니라 실제 선처치 효과가 발생한 실행만 사용한다.
            controller?.RecordSkillExecution(SimulationController.Skills.OpeningShot);
            //Target = null;
        }
        else
        {
            Debug.LogWarning("[Simulation] Phase -1: No target selected for Opening Shot Skill.");
        }
    }

    private void NotifyTargetStateChanged()
    {
        TargetStateChanged?.Invoke();
    }
}
