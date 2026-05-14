using System;

namespace Rebellion.Input
{
    /// <summary>
    /// 원시 디바이스 입력이 아니라 게임플레이 의미 단위의 입력을 노출한다.
    /// 다른 시스템은 키보드/패드를 직접 읽지 말고 이 계약을 통해 입력을 받는다.
    /// </summary>
    public interface IGameplayInput
    {
        float MapRotate { get; }

        event Action<float> OnMapRotateChanged;
        event Action OnCrewRotateRequested;
        event Action OnCrewDeselectRequested;

        GameplayInputHandlerRegistration RegisterCommandHandler(
            GameplayInputCommand command,
            Func<GameplayInputCommandContext, bool> handler,
            int priority = 0);
    }
}
