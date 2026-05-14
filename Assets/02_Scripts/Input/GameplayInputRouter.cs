using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rebellion.Input
{
    /// <summary>
    /// Unity Input Action 에셋을 읽어 게임플레이 의미 단위의 입력으로 변환한다.
    /// 디바이스 바인딩은 .inputactions 에셋에 두고, 실제 게임 시스템은 이 라우터만 구독하도록 만든다.
    /// </summary>
    public class GameplayInputRouter : MonoBehaviour, IGameplayInput
    {
        [Header("입력 에셋 설정")]
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private string _gameplayMapName = "Player";
        [SerializeField] private string _mapRotateActionName = "MapRotate";
        [SerializeField] private string _crewRotateActionName = "CrewRotate";
        [SerializeField] private string _crewDeselectActionName = "CrewDeselect";

        private InputActionMap _gameplayMap;
        private InputAction _mapRotateAction;
        private InputAction _crewRotateAction;
        private InputAction _crewDeselectAction;
        private readonly Dictionary<GameplayInputCommand, List<CommandHandler>> _commandHandlers = new();
        private int _nextHandlerId;

        public float MapRotate { get; private set; }

        public event Action<float> OnMapRotateChanged;
        public event Action OnCrewRotateRequested;
        public event Action OnCrewDeselectRequested;

        private void Awake()
        {
            if (_inputActions == null)
            {
                Debug.LogError("[GameplayInputRouter] InputActionAsset is missing.", this);
                enabled = false;
                return;
            }

            _gameplayMap = _inputActions.FindActionMap(_gameplayMapName, true);
            _mapRotateAction = _gameplayMap.FindAction(_mapRotateActionName, true);
            _crewRotateAction = _gameplayMap.FindAction(_crewRotateActionName, true);
            _crewDeselectAction = _gameplayMap.FindAction(_crewDeselectActionName, true);
        }

        private void OnEnable()
        {
            if (_gameplayMap == null || _mapRotateAction == null || _crewRotateAction == null || _crewDeselectAction == null)
                return;

            _mapRotateAction.performed += OnMapRotatePerformed;
            _mapRotateAction.canceled += OnMapRotateCanceled;
            _crewRotateAction.performed += OnCrewRotatePerformed;
            _crewDeselectAction.performed += OnCrewDeselectPerformed;
            _gameplayMap.Enable();
        }

        private void OnDisable()
        {
            if (_gameplayMap == null || _mapRotateAction == null || _crewRotateAction == null || _crewDeselectAction == null)
                return;

            _mapRotateAction.performed -= OnMapRotatePerformed;
            _mapRotateAction.canceled -= OnMapRotateCanceled;
            _crewRotateAction.performed -= OnCrewRotatePerformed;
            _crewDeselectAction.performed -= OnCrewDeselectPerformed;

            MapRotate = 0f;
            _gameplayMap.Disable();
        }

        public GameplayInputHandlerRegistration RegisterCommandHandler(
            GameplayInputCommand command,
            Func<GameplayInputCommandContext, bool> handler,
            int priority = 0)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (!_commandHandlers.TryGetValue(command, out List<CommandHandler> handlers))
            {
                handlers = new List<CommandHandler>();
                _commandHandlers.Add(command, handlers);
            }

            int id = ++_nextHandlerId;
            handlers.Add(new CommandHandler(id, priority, handler));
            handlers.Sort((left, right) => right.Priority.CompareTo(left.Priority));

            return new GameplayInputHandlerRegistration(this, command, id);
        }

        internal void UnregisterCommandHandler(GameplayInputCommand command, int id)
        {
            if (!_commandHandlers.TryGetValue(command, out List<CommandHandler> handlers))
                return;

            handlers.RemoveAll(handler => handler.Id == id);
        }

        private void OnMapRotatePerformed(InputAction.CallbackContext context)
        {
            // Q/E 또는 게임패드 입력값을 읽어 현재 회전 입력 상태로 저장한다.
            MapRotate = context.ReadValue<float>();

            if (MapRotate < 0f)
                Debug.Log("[Input] MapRotate Left");
            else if (MapRotate > 0f)
                Debug.Log("[Input] MapRotate Right");

            OnMapRotateChanged?.Invoke(MapRotate);
        }

        private void OnMapRotateCanceled(InputAction.CallbackContext context)
        {
            // 입력이 끝나면 회전값을 0으로 되돌린다.
            MapRotate = 0f;
            OnMapRotateChanged?.Invoke(MapRotate);
        }

        private void OnCrewRotatePerformed(InputAction.CallbackContext context)
        {
            Debug.Log("[Input] CrewRotate");
            OnCrewRotateRequested?.Invoke();
            DispatchCommand(GameplayInputCommand.CrewRotate, context);
        }

        private void OnCrewDeselectPerformed(InputAction.CallbackContext context)
        {
            Debug.Log("[Input] CrewDeselect");
            OnCrewDeselectRequested?.Invoke();
            DispatchCommand(GameplayInputCommand.CrewDeselect, context);
        }

        private bool DispatchCommand(GameplayInputCommand command, InputAction.CallbackContext context)
        {
            if (!_commandHandlers.TryGetValue(command, out List<CommandHandler> handlers))
                return false;

            GameplayInputCommandContext commandContext = new GameplayInputCommandContext(command, context);
            for (int i = 0; i < handlers.Count; i++)
            {
                if (handlers[i].Handler.Invoke(commandContext))
                    return true;
            }

            return false;
        }

        private readonly struct CommandHandler
        {
            public CommandHandler(int id, int priority, Func<GameplayInputCommandContext, bool> handler)
            {
                Id = id;
                Priority = priority;
                Handler = handler;
            }

            public int Id { get; }
            public int Priority { get; }
            public Func<GameplayInputCommandContext, bool> Handler { get; }
        }
    }
}
