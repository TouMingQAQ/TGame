using System;
using System.Threading;
using UnityEngine;

namespace TGame.Gamepad
{
    [DisallowMultipleComponent]
    public sealed class GamepadList : MonoBehaviour, IGamepadFocusMember
    {
        [SerializeField] private GamepadFocus focus;
        [SerializeField] private MonoBehaviour adapter;
        [SerializeField] private bool bubbleAtBoundary = true;

        private sealed class Request
        {
            public IGamepadList Adapter;
            public CancellationTokenSource Cancellation;
            public int Version;
            public bool Completed;
        }

        private IGamepadList runtimeAdapter;
        private IGamepadList subscribedAdapter;
        private GamepadFocus registeredFocus;
        private Request request;
        private string selectedKey;
        private int version;
        private int callbackDepth;
        private bool activated;
        private bool awaitingView;
        private bool dirty;
        private bool hasBoundary;
        private string boundaryKey;
        private Vector2 boundaryDirection;

        public GamepadFocus Focus => registeredFocus ? registeredFocus : ResolveFocus();
        public string SelectedKey => selectedKey;
        public bool IsPending => request != null || awaitingView;
        public bool IsActive => isActiveAndEnabled && Focus && Focus.IsFocused &&
            Focus.UI && Focus.UI.CurrentFocus == Focus;

        private bool IsCurrentList => activated && IsActive && Focus.CurrentList == this;
        private IGamepadList Adapter => runtimeAdapter ?? adapter as IGamepadList;

        private GamepadFocus ResolveFocus()
        {
            return focus ? focus : GetComponentInParent<GamepadFocus>(true);
        }

        private void OnEnable()
        {
            registeredFocus = ResolveFocus();
            Subscribe();
            if (registeredFocus) registeredFocus.RegisterList(this);
        }

        private void OnDisable()
        {
            Deactivate();
            if (registeredFocus) registeredFocus.UnregisterList(this);
            registeredFocus = null;
            Unsubscribe();
        }

        private void OnDestroy()
        {
            CancelRequest();
            Unsubscribe();
        }

        private void LateUpdate()
        {
            if (!dirty || callbackDepth != 0) return;
            dirty = false;
            callbackDepth++;
            try { ProcessChanged(false); }
            finally { callbackDepth--; }
        }

        public void SetAdapter(IGamepadList value)
        {
            if (ReferenceEquals(Adapter, value)) return;
            bool wasActivated = activated;
            Deactivate();
            activated = wasActivated;
            Unsubscribe();
            runtimeAdapter = value;
            adapter = value as MonoBehaviour;
            if (isActiveAndEnabled) Subscribe();
            Refresh();
        }

        private void Subscribe()
        {
            var value = Adapter;
            if (ReferenceEquals(subscribedAdapter, value)) return;
            Unsubscribe();
            subscribedAdapter = value;
            if (subscribedAdapter != null) subscribedAdapter.Changed += OnChanged;
        }

        private void Unsubscribe()
        {
            if (subscribedAdapter != null) subscribedAdapter.Changed -= OnChanged;
            subscribedAdapter = null;
        }

        private void OnChanged()
        {
            hasBoundary = false;
            dirty = true;
            if (callbackDepth != 0) return;
            dirty = false;
            callbackDepth++;
            try { ProcessChanged(); }
            finally { callbackDepth--; }
        }

        public void NotifyBindingChanged()
        {
            OnChanged();
        }

        public void Activate()
        {
            if (!IsActive || Focus.CurrentList != this) return;
            activated = true;
            Refresh();
        }

        public void Deactivate()
        {
            activated = false;
            CancelRequest();
            awaitingView = false;
            hasBoundary = false;
            ClearOwnedSelection();
        }

        public void Refresh()
        {
            hasBoundary = false;
            CancelRequest();
            awaitingView = false;
            if (!IsCurrentList) return;
            Restore();
        }

        public bool Select(string key)
        {
            if (!IsActive || !IsValidKey(key)) return false;
            var previous = Focus.CurrentList;
            if (previous && previous != this) previous.Deactivate();
            Focus.CurrentList = this;
            activated = true;
            if (!IsCurrentList) return false;
            selectedKey = key;
            StartShow(GamepadListView.Margin);
            return true;
        }

        public void Adopt(GamepadItem item)
        {
            if (!IsActive || Focus.CurrentList != this || !IsBoundItem(item, item ? item.ListKey : null)) return;
            activated = true;
            selectedKey = item.ListKey;
            StartShow(GamepadListView.Margin);
        }

        public bool Owns(GamepadItem item)
        {
            return !IsPending && IsCurrentList && item && item.IsNavigable &&
                item.CanInteract && string.Equals(item.ListKey, selectedKey, StringComparison.Ordinal) &&
                IsBoundItem(item, selectedKey);
        }

        public bool HandleNavigate(Vector2 direction)
        {
            if (!IsCurrentList || direction.sqrMagnitude <= 0f) return false;
            if (IsPending) return true;
            direction = Cardinal(direction);
            if (!IsValidKey(selectedKey))
            {
                Restore();
                return true;
            }
            if (hasBoundary && boundaryKey == selectedKey && boundaryDirection == direction)
                return !bubbleAtBoundary;

            var current = BeginRequest();
            if (current == null) return true;
            bool boundary = false;
            callbackDepth++;
            try
            {
                current.Adapter.FindNeighbor(selectedKey, direction, current.Cancellation.Token, result =>
                {
                    if (!Accept(current)) return;
                    current.Completed = true;
                    callbackDepth++;
                    try
                    {
                        FinishRequest(current);
                        if (result.Status == GamepadListStatus.Boundary)
                        {
                            hasBoundary = true;
                            boundaryKey = selectedKey;
                            boundaryDirection = direction;
                            boundary = true;
                            RestoreVisibleSelection();
                        }
                        else if (result.Status == GamepadListStatus.Found && IsValidKey(result.Key))
                        {
                            selectedKey = result.Key;
                            StartShow(GamepadListView.Margin);
                        }
                        else
                        {
                            RestoreVisibleSelection();
                        }
                    }
                    catch (Exception exception)
                    {
                        RecoverException(current, exception);
                    }
                    finally
                    {
                        callbackDepth--;
                    }
                });
                if (ReferenceEquals(request, current)) ClearOwnedSelection();
            }
            catch (Exception exception)
            {
                RecoverException(current, exception);
            }
            finally
            {
                callbackDepth--;
            }
            return !(boundary && bubbleAtBoundary);
        }

        private static Vector2 Cardinal(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                return direction.x > 0f ? Vector2.right : Vector2.left;
            return direction.y > 0f ? Vector2.up : Vector2.down;
        }

        private void Restore()
        {
            if (!IsCurrentList) return;
            if (!IsValidKey(selectedKey))
            {
                selectedKey = null;
                var value = Adapter;
                if (value == null)
                {
                    ClearOwnedSelection();
                    return;
                }
                try
                {
                    if (!value.TryGetInitial(out var initial) || !IsValidKey(initial))
                    {
                        ClearOwnedSelection();
                        return;
                    }
                    selectedKey = initial;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    ClearOwnedSelection();
                    return;
                }
            }
            StartShow(GamepadListView.Margin);
        }

        private void StartShow(GamepadListView view)
        {
            CancelRequest();
            awaitingView = false;
            hasBoundary = false;
            if (!IsCurrentList || !IsValidKey(selectedKey)) return;
            var key = selectedKey;
            var current = BeginRequest();
            if (current == null) return;
            callbackDepth++;
            try
            {
                current.Adapter.Show(key, view, current.Cancellation.Token, succeeded =>
                {
                    if (!Accept(current)) return;
                    current.Completed = true;
                    callbackDepth++;
                    try
                    {
                        FinishRequest(current);
                        if (!IsValidKey(key))
                        {
                            dirty = true;
                            ClearOwnedSelection();
                            return;
                        }
                        if (succeeded)
                        {
                            awaitingView = !TryApplySelection(key);
                            if (awaitingView) ClearOwnedSelection();
                        }
                        else
                        {
                            ClearOwnedSelection();
                        }
                    }
                    catch (Exception exception)
                    {
                        RecoverException(current, exception);
                    }
                    finally
                    {
                        callbackDepth--;
                    }
                });
                if (ReferenceEquals(request, current)) ClearOwnedSelection();
            }
            catch (Exception exception)
            {
                RecoverException(current, exception);
            }
            finally
            {
                callbackDepth--;
            }
        }

        private Request BeginRequest()
        {
            var value = Adapter;
            if (value == null) return null;
            var current = new Request
            {
                Adapter = value,
                Cancellation = new CancellationTokenSource(),
                Version = ++version
            };
            request = current;
            return current;
        }

        private bool Accept(Request current)
        {
            if (current.Completed || !ReferenceEquals(request, current)) return false;
            if (current.Version == version && ReferenceEquals(current.Adapter, Adapter) && IsCurrentList)
                return true;
            CancelRequest();
            return false;
        }

        private void FinishRequest(Request current)
        {
            if (!ReferenceEquals(request, current)) return;
            request = null;
            current.Cancellation.Dispose();
        }

        private void CancelRequest()
        {
            ++version;
            var previous = request;
            request = null;
            if (previous == null) return;
            previous.Completed = true;
            try
            {
                previous.Cancellation.Cancel();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                previous.Cancellation.Dispose();
            }
        }

        private void RecoverException(Request current, Exception exception)
        {
            Debug.LogException(exception, this);
            if (ReferenceEquals(request, current)) CancelRequest();
            else if (current.Version != version) return;
            dirty = false;
            awaitingView = false;
            if (IsCurrentList) RestoreVisibleSelection();
        }

        private bool IsValidKey(string key)
        {
            var value = Adapter;
            if (value == null || string.IsNullOrEmpty(key)) return false;
            try
            {
                return value.IsValid(key);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        private bool IsBoundItem(GamepadItem item, string key)
        {
            if (!item || item.List != this || item.Focus != Focus ||
                !string.Equals(item.ListKey, key, StringComparison.Ordinal) || !IsValidKey(key)) return false;
            try
            {
                return Adapter.TryGetItem(key, out var current) && current == item;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        private bool TryApplySelection(string key)
        {
            if (!IsCurrentList || !IsValidKey(key)) return false;
            GamepadItem item;
            try
            {
                if (!Adapter.TryGetItem(key, out item)) return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
            if (!item || !item.IsNavigable || !item.CanInteract || !IsBoundItem(item, key)) return false;
            return Focus.UI.Select(item, false);
        }

        private void RestoreVisibleSelection()
        {
            if (!IsCurrentList) return;
            awaitingView = false;
            if (!TryApplySelection(selectedKey)) ClearOwnedSelection();
        }

        private void ClearOwnedSelection()
        {
            var target = Focus;
            if (!target || !target.UI) return;
            var selected = target.UI.SelectedItem;
            if (selected && (selected.List == this ||
                (IsCurrentList && selected.Focus == target))) target.UI.ClearSelection(false);
        }

        private void ProcessChanged(bool allowShow = true)
        {
            if (!IsCurrentList) return;
            if (!IsValidKey(selectedKey))
            {
                CancelRequest();
                awaitingView = false;
                ClearOwnedSelection();
                if (allowShow || !string.IsNullOrEmpty(selectedKey)) Restore();
                return;
            }
            var selected = Focus.UI.SelectedItem;
            if (selected && selected.List == this &&
                (!selected.IsNavigable || !selected.CanInteract || !IsBoundItem(selected, selectedKey)))
                ClearOwnedSelection();
            if (request != null) return;
            if (awaitingView)
            {
                awaitingView = false;
                if (!TryApplySelection(selectedKey)) awaitingView = true;
                return;
            }
            if (selected && selected == Focus.UI.SelectedItem && IsBoundItem(selected, selectedKey)) return;
            if (allowShow) StartShow(GamepadListView.Margin);
            else
            {
                awaitingView = false;
                if (!TryApplySelection(selectedKey)) awaitingView = true;
            }
        }

        GamepadItem IGamepadFocusMember.NavigableItem => null;

        void IGamepadFocusMember.OnFocusExit() => Deactivate();
    }
}
