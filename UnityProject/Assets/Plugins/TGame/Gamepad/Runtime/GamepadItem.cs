using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TGame.Gamepad
{
    [DisallowMultipleComponent]
    public sealed class GamepadItem : MonoBehaviour, IGamepadFocusMember
    {
        [SerializeField] private GamepadFocus focus;
        [SerializeField] private bool navigable = true;
        [SerializeField] private bool interactable = true;
        [Tooltip("选中本目标时是否接受游标指向；false 时管理器会隐藏游标，直到下一个接受游标的目标。")]
        [SerializeField] private bool acceptsCursor = true;
        [SerializeField] private Selectable selectable;
        [SerializeField] private RectTransform cursorAnchor;
        [SerializeField] private Vector2 cursorOffset;
        [SerializeField] private GamepadItem up;
        [SerializeField] private GamepadItem down;
        [SerializeField] private GamepadItem left;
        [SerializeField] private GamepadItem right;
        public UnityEvent onSelected = new UnityEvent();
        public UnityEvent onDeselected = new UnityEvent();

        private readonly List<GamepadAction> actions = new List<GamepadAction>();
        private GamepadFocus registeredFocus;
        private bool listView;
        public GamepadFocus Focus => registeredFocus ? registeredFocus : ResolveFocus();
        public GamepadList List { get; private set; }
        public string ListKey { get; private set; }
        public bool IsSelected { get; private set; }
        public bool IsNavigable => isActiveAndEnabled && navigable && (!listView || (List && List.isActiveAndEnabled && List.Focus == Focus && !string.IsNullOrEmpty(ListKey))) &&
            (!selectable || (selectable.IsActive() && selectable.IsInteractable()));
        public bool CanInteract => IsNavigable && interactable;
        public bool AcceptsCursor => acceptsCursor;
        public bool CanReceiveInput => CanInteract && IsSelected && Focus && Focus.UI &&
            Focus.UI.SelectedItem == this && Focus.UI.CurrentFocus == Focus &&
            (!List || List.Owns(this));
        public RectTransform CursorAnchor => cursorAnchor ? cursorAnchor : transform as RectTransform;
        public Vector2 CursorOffset => cursorOffset;
        internal IReadOnlyList<GamepadAction> Actions => actions;

        private void Reset()
        {
            cursorAnchor = transform as RectTransform;
            selectable = GetComponent<Selectable>();
        }

        private GamepadFocus ResolveFocus()
        {
            return focus ? focus : GetComponentInParent<GamepadFocus>(true);
        }

        private void OnEnable()
        {
            if (!registeredFocus) registeredFocus = ResolveFocus();
            if (registeredFocus) registeredFocus.RegisterItem(this);
            if (List) List.NotifyBindingChanged();
        }

        private void OnDisable()
        {
            if (registeredFocus) registeredFocus.UnregisterItem(this);
            if (List) List.NotifyBindingChanged();
        }

        private void OnTransformParentChanged()
        {
            if (!isActiveAndEnabled || registeredFocus) return;
            var next = ResolveFocus();
            if (!next) return;
            registeredFocus = next;
            if (registeredFocus) registeredFocus.RegisterItem(this);
            if (List) List.NotifyBindingChanged();
        }

        public void SetNavigable(bool value) { navigable = value; }
        public void SetInteractable(bool value) { interactable = value; }

        public void SetSelectable(Selectable value) { selectable = value; }

        public void SetCursorAnchor(RectTransform value) { cursorAnchor = value; }

        public void SetFocus(GamepadFocus value)
        {
            if (registeredFocus == value) return;
            if (registeredFocus) registeredFocus.UnregisterItem(this);
            registeredFocus = value;
            if (isActiveAndEnabled && registeredFocus) registeredFocus.RegisterItem(this);
            if (List) List.NotifyBindingChanged();
        }

        public void Bind(GamepadList list, string key)
        {
            if (List == list && ListKey == key) return;
            Unbind();
            listView = true;
            List = list;
            ListKey = key;
            if (List) List.NotifyBindingChanged();
        }

        public void Unbind()
        {
            var previous = List;
            if (IsSelected && Focus && Focus.UI) Focus.UI.ClearSelection(false);
            List = null;
            ListKey = null;
            if (previous) previous.NotifyBindingChanged();
        }

        internal void RegisterAction(GamepadAction action)
        {
            if (!actions.Contains(action)) actions.Add(action);
        }

        internal void UnregisterAction(GamepadAction action) { actions.Remove(action); }

        internal void SetSelected(bool value)
        {
            if (IsSelected == value) return;
            IsSelected = value;
            if (value) onSelected.Invoke(); else onDeselected.Invoke();
        }

        internal GamepadItem Neighbor(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)) return direction.x > 0 ? right : left;
            return direction.y > 0 ? up : down;
        }

        GamepadItem IGamepadFocusMember.NavigableItem => IsNavigable ? this : null;

        void IGamepadFocusMember.OnFocusExit() { }
    }
}
