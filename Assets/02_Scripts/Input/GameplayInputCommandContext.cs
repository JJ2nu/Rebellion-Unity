using UnityEngine.InputSystem;

namespace Rebellion.Input
{
    public readonly struct GameplayInputCommandContext
    {
        public GameplayInputCommandContext(GameplayInputCommand command, InputAction.CallbackContext inputContext)
        {
            Command = command;
            InputContext = inputContext;
        }

        public GameplayInputCommand Command { get; }
        public InputAction.CallbackContext InputContext { get; }
    }
}
