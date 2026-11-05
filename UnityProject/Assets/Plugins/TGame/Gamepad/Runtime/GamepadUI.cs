using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TGame.Gamepad
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GamepadUI : MonoBehaviour
    {
        [SerializeField] private GamepadFocus initialFocus;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMap = "UI";
        [SerializeField, Range(0.1f, 1f)] private float deadzone = 0.5f;
        [SerializeField, Min(0f)] private float repeatDelay = 0.4f;
        [SerializeField, Min(0.02f)] private float repeatInterval = 0.1f;
        [SerializeField] private bool suppressEventSystemNavigation = true;

        private readonly List<GamepadFocus> stack = new List<GamepadFocus>();
        private readonly List<GamepadFocus> path = new List<GamepadFocus>();
        private readonly List<GamepadAction> held = new List<GamepadAction>();
        private readonly Queue<System.Action> changes = new Queue<System.Action>();
        private readonly HashSet<string> pressed = new HashSet<string>();
        private InputActionAsset ownedAsset;
        private InputActionMap map;
        private InputAction navigate;
        private Vector2 lastDirection;
        private float nextRepeat;
        private bool changing;
        private bool started;
        private EventSystem suppressedSystem;
        private bool previousNavigation;

        public static GamepadUI Instance { get; private set; }
        public GamepadFocus CurrentFocus => path.Count > 0 ? path[path.Count - 1] : null;
        public GamepadItem SelectedItem { get; private set; }
        public int Revision { get; private set; }
        public event System.Action<GamepadItem> SelectionChanged;
        public event System.Action<GamepadFocus> FocusChanged;

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Debug.LogError("Only one enabled GamepadUI is supported.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (Instance && Instance != this) { enabled = false; return; }
            Instance = this;
            BuildInput();
            InputSystem.onDeviceChange += DeviceChanged;
            LogGamepadState();
            if (started && initialFocus) Push(initialFocus);
        }

        private void LogGamepadState()
        {
            var device = UnityEngine.InputSystem.Gamepad.current;
            if (device != null)
                Debug.Log($"[GamepadUI] 已就绪，当前手柄: {device.displayName}", this);
            else
                Debug.LogWarning("[GamepadUI] 未检测到手柄设备，界面手柄输入不会响应。连接手柄后会自动恢复。", this);
        }

        private void Start()
        {
            started = true;
            if (stack.Count == 0 && initialFocus) Push(initialFocus);
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= DeviceChanged;
            Change(() =>
            {
                ClearCore(true);
                for (var i = path.Count - 1; i >= 0; i--) if (path[i]) path[i].Exit();
                path.Clear();
                stack.Clear();
                Revision++;
            });
            pressed.Clear();
            lastDirection = Vector2.zero;
            if (map != null) map.Disable();
            if (ownedAsset) Destroy(ownedAsset);
            else map?.Dispose();
            map = null;
            ownedAsset = null;
            RestoreEventSystem();
            if (Instance == this) Instance = null;
        }

        private void BuildInput()
        {
            if (inputActions)
            {
                ownedAsset = Instantiate(inputActions);
                ownedAsset.Disable();
                map = ownedAsset.FindActionMap(actionMap, false);
                if (map == null)
                {
                    Debug.LogError("GamepadUI action map was not found: " + actionMap, this);
                    return;
                }
            }
            else
            {
                map = new InputActionMap("GamepadUI");
                var move = map.AddAction(GamepadCommands.Navigate, InputActionType.Value);
                move.AddBinding("<Gamepad>/leftStick");
                move.AddBinding("<Gamepad>/dpad");
                map.AddAction(GamepadCommands.Submit, InputActionType.Button, "<Gamepad>/buttonSouth");
                map.AddAction(GamepadCommands.Cancel, InputActionType.Button, "<Gamepad>/buttonEast");
                map.AddAction(GamepadCommands.Previous, InputActionType.Button, "<Gamepad>/leftShoulder");
                map.AddAction(GamepadCommands.Next, InputActionType.Button, "<Gamepad>/rightShoulder");
            }
            navigate = map.FindAction(GamepadCommands.Navigate, false);
            map.Enable();
        }

        private void Update()
        {
            if (suppressEventSystemNavigation && stack.Count > 0) SuppressEventSystem();
            else RestoreEventSystem();
            var invalidPath = false;
            foreach (var focus in path) if (!focus || !focus.isActiveAndEnabled) { invalidPath = true; break; }
            if (invalidPath)
            {
                var root = stack.Count > 0 ? stack[stack.Count - 1] : null;
                if (!root || !root.isActiveAndEnabled) Pop(); else Focus(root);
            }
            if (SelectedItem && (!SelectedItem.IsNavigable || SelectedItem.Focus != CurrentFocus ||
                (SelectedItem.List && !SelectedItem.List.Owns(SelectedItem)))) ClearSelection(false);
            foreach (var action in held.ToArray())
                if (action && !action.CanReceiveInput) action.Cancel();
            if (CurrentFocus && !SelectedItem && !CurrentFocus.CurrentList) CurrentFocus.Restore();
            if (!isActiveAndEnabled || map == null) return;
            var currentMap = map;

            var value = navigate != null ? navigate.ReadValue<Vector2>() : Vector2.zero;
            var direction = value.magnitude >= deadzone ? Cardinal(value) : Vector2.zero;
            if (direction != lastDirection)
            {
                if (lastDirection != Vector2.zero)
                    Dispatch(new GamepadInput(GamepadCommands.Navigate, GamepadPhase.Released, lastDirection));
                if (!isActiveAndEnabled || map != currentMap) return;
                lastDirection = direction;
                if (direction != Vector2.zero)
                {
                    nextRepeat = Time.unscaledTime + repeatDelay;
                    Dispatch(new GamepadInput(GamepadCommands.Navigate, GamepadPhase.Pressed, direction));
                }
            }
            else if (direction != Vector2.zero && Time.unscaledTime >= nextRepeat)
            {
                nextRepeat = Time.unscaledTime + repeatInterval;
                Dispatch(new GamepadInput(GamepadCommands.Navigate, GamepadPhase.Repeated, direction));
            }

            if (!isActiveAndEnabled || map != currentMap) return;
            foreach (var input in currentMap.actions)
            {
                if (!isActiveAndEnabled || map != currentMap) return;
                if (input == navigate || input.type != InputActionType.Button) continue;
                var down = input.IsPressed();
                if ((down || input.WasPressedThisFrame()) && pressed.Add(input.name))
                    Dispatch(new GamepadInput(input.name, GamepadPhase.Pressed));
                if (!isActiveAndEnabled || map != currentMap) return;
                if (!down && pressed.Remove(input.name))
                    Dispatch(new GamepadInput(input.name, GamepadPhase.Released));
            }
        }

        private static Vector2 Cardinal(Vector2 value)
        {
            return Mathf.Abs(value.x) > Mathf.Abs(value.y)
                ? new Vector2(Mathf.Sign(value.x), 0) : new Vector2(0, Mathf.Sign(value.y));
        }

        public void Dispatch(GamepadInput input)
        {
            if (!isActiveAndEnabled) return;
            if (input.Phase == GamepadPhase.Released || input.Phase == GamepadPhase.Canceled)
            {
                foreach (var action in held.ToArray())
                {
                    if (!action || action.Operation != input.Operation) continue;
                    if (input.Phase == GamepadPhase.Canceled || !action.CanReceiveInput) action.Cancel();
                    else action.Release(input);
                }
                return;
            }
            var version = Revision;
            var target = SelectedItem;
            if (target && target.CanReceiveInput)
            {
                var listeners = new List<GamepadAction>(target.Actions);
                listeners.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                foreach (var action in listeners)
                {
                    if (action && action.Handle(this, input)) return;
                    if (Revision != version || SelectedItem != target) return;
                }
            }
            var leaf = CurrentFocus;
            var list = leaf ? leaf.CurrentList : null;
            if (input.Operation == GamepadCommands.Navigate && list && list.isActiveAndEnabled &&
                list.HandleNavigate(input.Value)) return;
            if (Revision != version) return;
            for (var i = path.Count - 1; i >= 0; i--)
            {
                if (path[i] && path[i].Handle(input)) return;
                if (Revision != version) return;
            }
        }

        public bool Push(GamepadFocus root)
        {
            if (!isActiveAndEnabled || !root || !root.isActiveAndEnabled || stack.Contains(root)) return false;
            Change(() =>
            {
                if (!isActiveAndEnabled || !root || !root.isActiveAndEnabled || stack.Contains(root)) return;
                if (!TryPath(root.ResolveLeaf(), root, out _)) return;
                stack.Add(root);
                ApplyFocus(root.ResolveLeaf(), root);
            });
            return true;
        }

        public void Pop()
        {
            Change(() =>
            {
                if (stack.Count == 0) return;
                stack.RemoveAt(stack.Count - 1);
                while (stack.Count > 0 && (!stack[stack.Count - 1] || !stack[stack.Count - 1].isActiveAndEnabled))
                    stack.RemoveAt(stack.Count - 1);
                var root = stack.Count > 0 ? stack[stack.Count - 1] : null;
                ApplyFocus(root ? root.ResolveLeaf() : null, root);
            });
        }

        /// <summary>从聚焦栈移除指定区域（支持乱序关闭），并回退到新的栈顶。</summary>
        public bool Remove(GamepadFocus focus)
        {
            if (!isActiveAndEnabled || !focus || !stack.Contains(focus)) return false;
            Change(() =>
            {
                if (!stack.Remove(focus)) return;
                while (stack.Count > 0 && (!stack[stack.Count - 1] || !stack[stack.Count - 1].isActiveAndEnabled))
                    stack.RemoveAt(stack.Count - 1);
                var root = stack.Count > 0 ? stack[stack.Count - 1] : null;
                ApplyFocus(root ? root.ResolveLeaf() : null, root);
            });
            return true;
        }

        public bool Focus(GamepadFocus focus)
        {
            if (!isActiveAndEnabled || !focus || !focus.isActiveAndEnabled) return false;
            if (stack.Count == 0) return Push(focus);
            var root = stack[stack.Count - 1];
            if (!TryPath(focus, root, out _)) return false;
            Change(() =>
            {
                if (!isActiveAndEnabled || !focus || !focus.isActiveAndEnabled ||
                    stack.Count == 0 || stack[stack.Count - 1] != root) return;
                ApplyFocus(focus.ResolveLeaf(), root);
            });
            return true;
        }

        private static bool TryPath(GamepadFocus leaf, GamepadFocus root, out List<GamepadFocus> result)
        {
            result = new List<GamepadFocus>();
            var visited = new HashSet<GamepadFocus>();
            for (var node = leaf; node && visited.Add(node); node = node.Parent)
            {
                if (!node.isActiveAndEnabled) return false;
                result.Add(node);
                if (node == root) { result.Reverse(); return true; }
            }
            return !leaf && !root;
        }

        private void ApplyFocus(GamepadFocus leaf, GamepadFocus root)
        {
            if (!TryPath(leaf, root, out var next)) return;
            Revision++;
            ClearCore(true);
            var common = 0;
            while (common < path.Count && common < next.Count && path[common] == next[common]) common++;
            // Even a common parent stops its own list when it becomes a container.
            if (CurrentFocus && CurrentFocus.CurrentList) CurrentFocus.CurrentList.Deactivate();
            for (var i = path.Count - 1; i >= common; i--) if (path[i]) path[i].Exit();
            path.Clear();
            path.AddRange(next);
            for (var i = 0; i + 1 < path.Count; i++) path[i].RememberChild(path[i + 1]);
            for (var i = common; i < path.Count; i++) path[i].Enter(this);
            if (CurrentFocus) CurrentFocus.Restore();
            FocusChanged?.Invoke(CurrentFocus);
        }

        public bool Select(GamepadItem item, bool reveal = true)
        {
            if (!isActiveAndEnabled || !item || !item.IsNavigable || item.Focus != CurrentFocus) return false;
            Change(() =>
            {
                if (!isActiveAndEnabled || !item || !item.IsNavigable || item.Focus != CurrentFocus) return;
                if (SelectedItem == item) return;
                Revision++;
                ClearCore(true);
                if (!isActiveAndEnabled || !item || !item.IsNavigable || item.Focus != CurrentFocus) return;
                var previousList = CurrentFocus.CurrentList;
                if (previousList && previousList != item.List) previousList.Deactivate();
                CurrentFocus.CurrentList = item.List;
                SelectedItem = item;
                CurrentFocus.Remember(item);
                item.SetSelected(true);
                UpdateCursor(item);
                SelectionChanged?.Invoke(item);
                if (reveal && item.List) item.List.Adopt(item);
            });
            return true;
        }

        private void UpdateCursor(GamepadItem item)
        {
            var cursor = CurrentFocus ? CurrentFocus.Cursor : null;
            if (cursor == null) return;
            if (item != null && item.AcceptsCursor)
            {
                cursor.Show();
                cursor.Move(item.gameObject);
            }
            else
            {
                cursor.Hide();
            }
        }

        public void ClearSelection(bool remember = true) { Change(() => { Revision++; ClearCore(remember); }); }

        private void ClearCore(bool remember)
        {
            var previous = SelectedItem;
            SelectedItem = null;
            foreach (var action in held.ToArray()) if (action) action.Cancel();
            if (previous)
            {
                if (remember && previous.Focus) previous.Focus.Remember(previous);
                previous.SetSelected(false);
            }
            foreach (var focus in path)
            {
                if (!focus) continue;
                var cursor = focus.Cursor;
                if (cursor != null) cursor.Hide();
            }
            if (previous) SelectionChanged?.Invoke(null);
        }

        private void Change(System.Action mutation)
        {
            changes.Enqueue(mutation);
            if (changing) return;
            changing = true;
            try
            {
                var count = 0;
                while (changes.Count > 0)
                {
                    if (++count > 128)
                    {
                        changes.Clear();
                        Debug.LogError("Recursive Gamepad UI selection callbacks exceeded the limit.", this);
                        break;
                    }
                    changes.Dequeue().Invoke();
                }
            }
            finally { changing = false; }
        }

        internal void Track(GamepadAction action) { if (!held.Contains(action)) held.Add(action); }
        internal void Untrack(GamepadAction action) { held.Remove(action); }

        private void DeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (!(device is UnityEngine.InputSystem.Gamepad)) return;
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected ||
                change == InputDeviceChange.Enabled)
            {
                Debug.Log($"[GamepadUI] 手柄已连接: {device.displayName}", this);
                return;
            }
            if (change != InputDeviceChange.Disconnected && change != InputDeviceChange.Removed &&
                change != InputDeviceChange.Disabled) return;
            Debug.LogWarning($"[GamepadUI] 手柄已断开: {device.displayName}", this);
            foreach (var action in held.ToArray()) if (action) action.Cancel();
            pressed.Clear();
            lastDirection = Vector2.zero;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            foreach (var action in held.ToArray()) if (action) action.Cancel();
        }

        private void SuppressEventSystem()
        {
            if (suppressedSystem != EventSystem.current)
            {
                RestoreEventSystem();
                suppressedSystem = EventSystem.current;
                if (suppressedSystem) previousNavigation = suppressedSystem.sendNavigationEvents;
            }
            if (suppressedSystem) suppressedSystem.sendNavigationEvents = false;
        }

        private void RestoreEventSystem()
        {
            if (suppressedSystem) suppressedSystem.sendNavigationEvents = previousNavigation;
            suppressedSystem = null;
        }
    }
}
