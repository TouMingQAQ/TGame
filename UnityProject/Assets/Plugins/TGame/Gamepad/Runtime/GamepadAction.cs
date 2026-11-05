using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace TGame.Gamepad
{
    public sealed class GamepadAction : MonoBehaviour
    {
        [SerializeField] private GamepadItem item;
        [SerializeField] private string operation = GamepadCommands.Submit;
        [SerializeField] private int priority;
        [SerializeField] private bool consume = true;
        [Tooltip("按下时自动对同一对象触发一次 pointerClick，等价于点击原控件。预制体里勾选即可，无需手工绑 onPressed。")]
        [SerializeField] private bool invokePointerClick;
        public UnityEvent onPressed = new UnityEvent();
        public UnityEvent onRepeated = new UnityEvent();
        public UnityEvent onReleased = new UnityEvent();
        public UnityEvent onCanceled = new UnityEvent();

        private GamepadItem registeredItem;
        private GamepadUI owner;
        private bool held;
        public GamepadItem Item => registeredItem;
        public string Operation => operation;
        public int Priority => priority;
        public bool IsHeld => held;
        public bool InvokePointerClick { get => invokePointerClick; set => invokePointerClick = value; }
        public bool CanReceiveInput => isActiveAndEnabled && registeredItem && registeredItem.CanReceiveInput;
        public GamepadInput LastInput { get; private set; }

        private void Reset() { item = GetComponent<GamepadItem>(); }

        public void Configure(GamepadItem target, string actionOperation = GamepadCommands.Submit,
            bool consumeInput = true, int actionPriority = 0)
        {
            if (registeredItem && registeredItem != target) registeredItem.UnregisterAction(this);
            item = target;
            registeredItem = target;
            if (isActiveAndEnabled && registeredItem) registeredItem.RegisterAction(this);
            operation = string.IsNullOrEmpty(actionOperation) ? GamepadCommands.Submit : actionOperation;
            consume = consumeInput;
            priority = actionPriority;
        }

        private void OnEnable()
        {
            registeredItem = item ? item : GetComponent<GamepadItem>();
            if (registeredItem) registeredItem.RegisterAction(this);
        }

        private void OnDisable()
        {
            Cancel();
            if (registeredItem) registeredItem.UnregisterAction(this);
            registeredItem = null;
        }

        internal bool Handle(GamepadUI ui, GamepadInput input)
        {
            if (!CanReceiveInput || operation != input.Operation) return false;
            LastInput = input;
            if (input.Phase == GamepadPhase.Pressed)
            {
                if (held) return consume;
                held = true;
                owner = ui;
                ui.Track(this);
                onPressed.Invoke();
                if (invokePointerClick) ClickSelf();
            }
            else if (input.Phase == GamepadPhase.Repeated)
            {
                if (!held || owner != ui) return false;
                onRepeated.Invoke();
            }
            return consume;
        }

        private void ClickSelf()
        {
            if (!isActiveAndEnabled) return;
            var system = EventSystem.current;
            if (!system) return;
            var data = new PointerEventData(system) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        internal void Release(GamepadInput input)
        {
            if (!held) return;
            End();
            LastInput = input;
            onReleased.Invoke();
        }

        public void Cancel()
        {
            if (!held) return;
            End();
            LastInput = new GamepadInput(operation, GamepadPhase.Canceled);
            onCanceled.Invoke();
        }

        private void End()
        {
            held = false;
            var previous = owner;
            owner = null;
            if (previous) previous.Untrack(this);
        }
    }
}
