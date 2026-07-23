using System.Collections;
using UnityEngine;

/// <summary>
/// 선처리 스킬별 Presentation Controller가 구현하는 공용 계약이다.
/// 구체 연출은 스킬 지원 여부, 재생 코루틴과 취소 정리만 제공하고 전체 실행 순서는 알지 않는다.
/// </summary>
public abstract class PreSimulationSkillPresentation : MonoBehaviour
{
    /// <summary>
    /// 이 Presentation이 전달된 스킬을 표현할 수 있는지 반환한다.
    /// </summary>
    public abstract bool CanPresent(SkillBase skill);

    /// <summary>
    /// 연출이 끝나거나 스킵 정리가 완료될 때까지 대기하는 코루틴을 반환한다.
    /// 필요한 효과 시점에는 context.TryApplyEffect()를 호출할 수 있다.
    /// </summary>
    public abstract IEnumerator Play(PreSimulationPresentationContext context);

    /// <summary>
    /// Reset, Scene 종료처럼 정상 완료 전에 흐름이 중단될 때 화면과 입력 상태를 즉시 정리한다.
    /// </summary>
    public abstract void CancelPresentation();
}
