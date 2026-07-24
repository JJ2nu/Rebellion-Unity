using System;

namespace Rebellion.Input
{
    public readonly struct GameplayInputHandlerRegistration : IDisposable
    {
        private readonly GameplayInputRouter _router;
        private readonly GameplayInputCommand _command;
        private readonly int _id;

        public GameplayInputHandlerRegistration(GameplayInputRouter router, GameplayInputCommand command, int id)
        {
            _router = router;
            _command = command;
            _id = id;
        }

        public void Dispose()
        {
            _router?.UnregisterCommandHandler(_command, _id);
        }
    }
}
