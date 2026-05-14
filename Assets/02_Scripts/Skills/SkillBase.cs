using System.Collections;
using System.Collections.Generic;

namespace Rebellion
{
    /// <summary>
    /// 모든 스킬의 베이스 클래스.
    /// 새로운 스킬 추가 시 이 클래스를 상속하고 Execute()를 구현한다.
    /// </summary>
    public abstract class SkillBase
    {
        public abstract string SkillName { get; }
        public abstract string Description { get; }

        /// <summary>
        /// 이 스킬이 시뮬레이션의 어느 단계에 발동되는지 정의한다.
        /// </summary>
        public abstract SkillTiming Timing { get; }

        /// <summary>
        /// 스킬 발동 조건을 검사한다. false이면 Execute()가 호출되지 않는다.
        /// </summary>
        public virtual bool CanExecute(IReadOnlyList<PieceBase> allPieces) => true;

        /// <summary>
        /// 스킬 효과를 코루틴으로 실행한다.
        /// </summary>
        //public abstract IEnumerator Execute(SimulationController controller, IReadOnlyList<PieceBase> allPieces);
    }
}
