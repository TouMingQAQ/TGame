using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TGame.Gamepad
{
    [DisallowMultipleComponent]
    public sealed class GamepadFocus : MonoBehaviour
    {
        [SerializeField] private GamepadUI manager;
        [SerializeField] private GamepadFocus parent;
        [SerializeField] private GamepadFocus[] children = new GamepadFocus[0];
        [SerializeField] private GamepadItem defaultItem;
        [SerializeField] private GamepadList defaultList;
        [SerializeField] private MonoBehaviour cursorSource;
        [SerializeField] private bool wrapChildren = true;
        [SerializeField] private bool wrapNavigation;
        [SerializeField] private bool consumeCancel;
        public UnityEvent onEntered = new UnityEvent();
        public UnityEvent onExited = new UnityEvent();
        public UnityEvent onCancel = new UnityEvent();

        private readonly List<IGamepadFocusMember> members = new List<IGamepadFocusMember>();
        private GamepadItem rememberedItem;
        private GamepadFocus rememberedChild;
        public GamepadUI UI => manager ? manager : GamepadUI.Instance;
        public GamepadFocus Parent => parent ? parent :
            (transform.parent ? transform.parent.GetComponentInParent<GamepadFocus>(true) : null);
        public bool IsFocused { get; private set; }
        public GamepadList CurrentList { get; set; }
        public IGamepadCursor Cursor => cursorSource as IGamepadCursor;
        public void SetCursor(MonoBehaviour source) { cursorSource = source; }
        public GamepadItem DefaultItem { get => defaultItem; set => defaultItem = value; }
        public bool WrapNavigation { get => wrapNavigation; set => wrapNavigation = value; }

        internal GamepadFocus ResolveLeaf()
        {
            var node = this;
            var visited = new HashSet<GamepadFocus>();
            while (node && visited.Add(node))
            {
                GamepadFocus next = null;
                if (node.rememberedChild && node.rememberedChild.isActiveAndEnabled)
                    next = node.rememberedChild;
                else
                    foreach (var child in node.children)
                        if (child && child.isActiveAndEnabled) { next = child; break; }
                if (!next || visited.Contains(next)) break;
                node = next;
            }
            return node;
        }

        internal void Enter(GamepadUI ui)
        {
            manager = ui;
            IsFocused = true;
            onEntered.Invoke();
        }

        internal void Restore()
        {
            if (!UI || UI.CurrentFocus != this) return;
            if (!CurrentList || !CurrentList.isActiveAndEnabled)
                CurrentList = defaultList && defaultList.isActiveAndEnabled ? defaultList : null;
            if (CurrentList && CurrentList.isActiveAndEnabled)
            {
                CurrentList.Activate();
                return;
            }
            var candidate = rememberedItem && rememberedItem.IsNavigable ? rememberedItem : defaultItem;
            if (!candidate || !candidate.IsNavigable || candidate.Focus != this)
            {
                candidate = null;
                foreach (var member in members)
                {
                    var item = member.NavigableItem;
                    if (item && item.IsNavigable) { candidate = item; break; }
                }
            }
            if (candidate) UI.Select(candidate);
        }

        internal void Exit()
        {
            IsFocused = false;
            foreach (var member in members.ToArray())
            {
                if (member is UnityEngine.Object obj && obj == null) continue;
                member.OnFocusExit();
            }
            if (Cursor != null) Cursor.Hide();
            onExited.Invoke();
        }

        internal void Remember(GamepadItem item) { rememberedItem = item; }
        internal void RememberChild(GamepadFocus child) { rememberedChild = child; }
        public void RegisterItem(GamepadItem item) { if (item) RegisterMember(item); }
        public void UnregisterItem(GamepadItem item)
        {
            UnregisterMember(item);
            if (UI && UI.SelectedItem == item) UI.ClearSelection(false);
        }
        public void RegisterList(GamepadList list) { if (list) RegisterMember(list); }
        public void UnregisterList(GamepadList list)
        {
            UnregisterMember(list);
            if (CurrentList == list) CurrentList = null;
        }
        private void RegisterMember(IGamepadFocusMember member)
        {
            if (member != null && !members.Contains(member)) members.Add(member);
        }
        private void UnregisterMember(IGamepadFocusMember member)
        {
            if (member != null) members.Remove(member);
        }

        internal bool Handle(GamepadInput input)
        {
            if (input.Phase != GamepadPhase.Pressed && input.Phase != GamepadPhase.Repeated) return false;
            if (input.Operation == GamepadCommands.Navigate)
                return Navigate(input.Value);
            if (input.Phase != GamepadPhase.Pressed) return false;
            if (input.Operation == GamepadCommands.Previous) return SwitchChild(-1);
            if (input.Operation == GamepadCommands.Next) return SwitchChild(1);
            if (input.Operation == GamepadCommands.Cancel)
            {
                onCancel.Invoke();
                CancelRequested?.Invoke();
                return consumeCancel;
            }
            return false;
        }

        public event System.Action CancelRequested;

        public bool SwitchChild(int step)
        {
            if (!UI || children.Length == 0) return false;
            var index = System.Array.IndexOf(children, rememberedChild);
            if (index < 0) index = step > 0 ? -1 : 0;
            for (var count = 0; count < children.Length; count++)
            {
                index += step > 0 ? 1 : -1;
                if (!wrapChildren && (index < 0 || index >= children.Length)) return false;
                index = (index + children.Length) % children.Length;
                var child = children[index];
                if (!child) continue;
                // This callback may enable a previously hidden tab.
                var ui = UI;
                var previousFocus = ui.CurrentFocus;
                ChildRequested?.Invoke(child);
                onChildRequested.Invoke(child);
                if (!ui || !ui.isActiveAndEnabled || ui.CurrentFocus != previousFocus) return false;
                if (!child || !child.isActiveAndEnabled) continue;
                if (!ui.Focus(child)) return false;
                rememberedChild = child;
                return true;
            }
            return false;
        }

        [System.Serializable] public sealed class ChildEvent : UnityEvent<GamepadFocus> { }
        public ChildEvent onChildRequested = new ChildEvent();
        public event System.Action<GamepadFocus> ChildRequested;

        private bool Navigate(Vector2 direction)
        {
            if (!UI || UI.CurrentFocus != this || direction.sqrMagnitude < 0.01f) return false;
            var current = UI.SelectedItem;
            var explicitItem = current ? current.Neighbor(direction) : null;
            if (explicitItem && explicitItem.Focus == this && explicitItem.IsNavigable)
                return UI.Select(explicitItem);
            if (!current) { Restore(); return UI.SelectedItem; }
            var anchor = current.CursorAnchor;
            if (!anchor) return false;
            var origin = ScreenPoint(anchor);
            var axis = direction.normalized;
            GamepadItem best = null;
            float bestScore = float.PositiveInfinity;
            GamepadItem wrapped = null;
            float wrapScore = float.PositiveInfinity;
            foreach (var member in members)
            {
                var item = member.NavigableItem;
                if (!item || item == current || !item.CursorAnchor) continue;
                // Virtualized neighbors are resolved by the list data source.
                if (current.List && item.List == current.List) continue;
                var delta = ScreenPoint(item.CursorAnchor) - origin;
                var forward = Vector2.Dot(delta, axis);
                var side = Mathf.Abs(axis.x * delta.y - axis.y * delta.x);
                if (forward > 0.01f)
                {
                    var score = delta.sqrMagnitude / forward + side * 2f;
                    if (score < bestScore) { best = item; bestScore = score; }
                }
                var edgeScore = forward + side * 4f;
                if (edgeScore < wrapScore) { wrapped = item; wrapScore = edgeScore; }
            }
            if (!best && wrapNavigation) best = wrapped;
            return best && UI.Select(best);
        }

        private static Vector2 ScreenPoint(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        }
    }
}
