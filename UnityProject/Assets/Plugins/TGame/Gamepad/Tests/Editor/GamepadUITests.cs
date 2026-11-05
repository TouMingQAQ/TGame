using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace TGame.Gamepad.Tests
{
    public sealed class GamepadUITests
    {
        private const string FirstKey = "item:101";
        private const string SecondKey = "item:205";
        private readonly List<GameObject> objects = new List<GameObject>();
        private GamepadUI ui;

        [SetUp]
        public void SetUp()
        {
            Assert.That(GamepadUI.Instance, Is.Null, "The fixture needs an isolated GamepadUI.");
            ui = CreateObject("UI").AddComponent<GamepadUI>();
            if (GamepadUI.Instance != ui) InvokeLifecycle(ui, "Awake");
            if (GetField(ui, "map") == null) InvokeLifecycle(ui, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (ui && GamepadUI.Instance == ui) InvokeLifecycle(ui, "OnDisable");
            for (var i = objects.Count - 1; i >= 0; i--)
            {
                if (!objects[i]) continue;
                var action = objects[i].GetComponent<GamepadAction>();
                if (action && GetField(action, "registeredItem") != null) InvokeLifecycle(action, "OnDisable");
                var item = objects[i].GetComponent<GamepadItem>();
                if (item && GetField(item, "registeredFocus") != null) InvokeLifecycle(item, "OnDisable");
                var list = objects[i].GetComponent<GamepadList>();
                if (list && GetField(list, "registeredFocus") != null) InvokeLifecycle(list, "OnDisable");
                UnityEngine.Object.DestroyImmediate(objects[i]);
            }
            objects.Clear();
            ui = null;
        }

        [Test]
        public void PopupRuntimeCancelListenerPopsAndRestoresThePreviouslySelectedItem()
        {
            var root = CreateFocus("Root");
            var first = CreateItem(root, "First");
            var remembered = CreateItem(root, "Remembered");
            var popup = CreateFocus("Popup");
            var popupItem = CreateItem(popup, "PopupItem");
            SetField(root, "defaultItem", first);
            Assert.That(ui.Push(root), Is.True);
            Assert.That(ui.Select(remembered), Is.True);

            Assert.That(ui.Push(popup), Is.True);
            Assert.That(ui.CurrentFocus, Is.SameAs(popup));
            Assert.That(ui.SelectedItem, Is.SameAs(popupItem));
            Assert.That(remembered.IsSelected, Is.False);
            Assert.That(root.IsFocused, Is.False);

            popup.onCancel.AddListener(ui.Pop);
            ui.Dispatch(new GamepadInput(GamepadCommands.Cancel, GamepadPhase.Pressed));

            Assert.That(ui.CurrentFocus, Is.SameAs(root));
            Assert.That(ui.SelectedItem, Is.SameAs(remembered));
            Assert.That(remembered.IsSelected, Is.True);
            Assert.That(popupItem.IsSelected, Is.False);
            Assert.That(popup.IsFocused, Is.False);
        }

        [Test]
        public void TwoPushesInsideTheSameSelectionCallbackDoNotDuplicateThePopup()
        {
            var root = CreateFocus("Root");
            var rootItem = CreateItem(root, "RootItem");
            var popup = CreateFocus("Popup");
            CreateItem(popup, "PopupItem");
            var entered = 0;
            var selectionCallbacks = 0;
            popup.onEntered.AddListener(() => entered++);
            rootItem.onSelected.AddListener(() =>
            {
                if (++selectionCallbacks != 1) return;
                ui.Push(popup);
                ui.Push(popup);
            });

            Assert.That(ui.Push(root), Is.True);
            Assert.That(ui.CurrentFocus, Is.SameAs(popup));
            Assert.That(entered, Is.EqualTo(1));

            ui.Pop();
            Assert.That(ui.CurrentFocus, Is.SameAs(root), "One Pop must remove the popup completely.");
            Assert.That(ui.SelectedItem, Is.SameAs(rootItem));
            ui.Pop();
            Assert.That(ui.CurrentFocus, Is.Null);
        }

        [Test]
        public void QueuedFocusFromTheOldRootCannotOverrideAPopupPush()
        {
            var root = CreateFocus("Root");
            var rootItem = CreateItem(root, "RootItem");
            var popup = CreateFocus("Popup");
            var popupItem = CreateItem(popup, "PopupItem");
            var queuedFocusAccepted = false;
            rootItem.onSelected.AddListener(() =>
            {
                ui.Push(popup);
                queuedFocusAccepted = ui.Focus(root);
            });

            ui.Push(root);

            Assert.That(queuedFocusAccepted, Is.True, "The request is admitted before the queued Push runs.");
            Assert.That(ui.CurrentFocus, Is.SameAs(popup));
            Assert.That(ui.SelectedItem, Is.SameAs(popupItem));
            Assert.That(ui.Focus(root), Is.False, "The old root is outside the current popup path.");
            Assert.That(ui.CurrentFocus, Is.SameAs(popup));
        }

        [Test]
        public void FocusLossCancelsAHeldActionAndReleaseDoesNotReachTheNewItem()
        {
            var root = CreateFocus("Root");
            var item = CreateItem(root, "RootItem");
            var action = CreateAction(item);
            var popup = CreateFocus("Popup");
            var popupItem = CreateItem(popup, "PopupItem");
            var popupAction = CreateAction(popupItem);
            var pressed = 0;
            var canceled = 0;
            var released = 0;
            var popupReleased = 0;
            action.onPressed.AddListener(() => pressed++);
            action.onCanceled.AddListener(() => canceled++);
            action.onReleased.AddListener(() => released++);
            popupAction.onReleased.AddListener(() => popupReleased++);
            ui.Push(root);

            Submit(GamepadPhase.Pressed);
            Assert.That(action.IsHeld, Is.True);
            ui.Push(popup);
            Submit(GamepadPhase.Released);

            Assert.That(pressed, Is.EqualTo(1));
            Assert.That(canceled, Is.EqualTo(1));
            Assert.That(action.IsHeld, Is.False);
            Assert.That(action.LastInput.Phase, Is.EqualTo(GamepadPhase.Canceled));
            Assert.That(released, Is.Zero);
            Assert.That(popupReleased, Is.Zero);
            Assert.That(popupAction.IsHeld, Is.False);
            Submit(GamepadPhase.Pressed);
            Submit(GamepadPhase.Released);
            Assert.That(popupReleased, Is.EqualTo(1), "A fresh press/release still works on the popup.");
        }

        [Test]
        public void UnbindARecycledItemClearsSelectionBindingAndHeldInputImmediately()
        {
            var fixture = CreateListFixture();
            var action = CreateAction(fixture.First);
            var canceled = 0;
            action.onCanceled.AddListener(() => canceled++);
            Submit(GamepadPhase.Pressed);
            Assert.That(action.IsHeld, Is.True);
            fixture.Adapter.Items.Remove(FirstKey);

            fixture.First.Unbind();

            Assert.That(ui.SelectedItem, Is.Null);
            Assert.That(fixture.First.IsSelected, Is.False);
            Assert.That(fixture.First.List, Is.Null);
            Assert.That(fixture.First.ListKey, Is.Null);
            Assert.That(fixture.First.CanReceiveInput, Is.False);
            Assert.That(action.IsHeld, Is.False);
            Assert.That(canceled, Is.EqualTo(1));
            Assert.That(fixture.List.SelectedKey, Is.EqualTo(FirstKey), "The data key survives view recycling.");
        }

        [Test]
        public void AsyncNeighborAndShowSelectByStableKeyAfterDataOrderChanges()
        {
            var fixture = CreateListFixture();
            fixture.Adapter.DeferFind = true;
            fixture.Adapter.DeferShow = true;
            fixture.Adapter.Keys.Reverse();

            Assert.That(fixture.List.HandleNavigate(Vector2.down), Is.True);
            var find = fixture.Adapter.Finds[0];
            Assert.That(find.Key, Is.EqualTo(FirstKey));
            Assert.That(find.Direction, Is.EqualTo(Vector2.down));
            Assert.That(fixture.List.IsPending, Is.True);
            Assert.That(ui.SelectedItem, Is.Null);

            find.Complete(new GamepadListResult(GamepadListStatus.Found, SecondKey));
            var show = fixture.Adapter.Shows[fixture.Adapter.Shows.Count - 1];
            Assert.That(show.Key, Is.EqualTo(SecondKey));
            Assert.That(show.View, Is.EqualTo(GamepadListView.Margin));
            Assert.That(fixture.List.SelectedKey, Is.EqualTo(SecondKey));
            Assert.That(fixture.List.IsPending, Is.True);
            Assert.That(ui.SelectedItem, Is.Null);

            show.Complete(true);
            Assert.That(fixture.List.IsPending, Is.False);
            Assert.That(ui.SelectedItem, Is.SameAs(fixture.Second));
            Assert.That(fixture.List.Owns(fixture.Second), Is.True);
            var showCount = fixture.Adapter.Shows.Count;
            fixture.Adapter.RaiseChangedDuringShow = true;
            fixture.Adapter.DeferShow = false;
            fixture.List.Activate();
            Assert.That(fixture.Adapter.Shows.Count, Is.EqualTo(showCount + 1));
            InvokeLifecycle(fixture.List, "LateUpdate");
            Assert.That(fixture.Adapter.Shows.Count, Is.EqualTo(showCount + 1),
                "Changed inside Show must not schedule another Show on the deferred dirty pass.");
            Assert.That(ui.SelectedItem, Is.SameAs(fixture.Second));
            find.Complete(new GamepadListResult(GamepadListStatus.Found, FirstKey));
            Assert.That(fixture.List.SelectedKey, Is.EqualTo(SecondKey), "Duplicate callbacks are ignored.");
        }

        [Test]
        public void PopupCancelsPendingNeighborAndItsLateCallbackCannotStealSelection()
        {
            var fixture = CreateListFixture();
            fixture.Adapter.DeferFind = true;
            fixture.List.HandleNavigate(Vector2.down);
            var find = fixture.Adapter.Finds[0];
            var showsBeforePopup = fixture.Adapter.Shows.Count;
            var popup = CreateFocus("Popup");
            var popupItem = CreateItem(popup, "PopupItem");

            ui.Push(popup);

            Assert.That(find.Cancellation.IsCancellationRequested, Is.True);
            Assert.That(fixture.List.IsPending, Is.False);
            find.Complete(new GamepadListResult(GamepadListStatus.Found, SecondKey));
            Assert.That(fixture.Adapter.Shows.Count, Is.EqualTo(showsBeforePopup));
            Assert.That(ui.CurrentFocus, Is.SameAs(popup));
            Assert.That(ui.SelectedItem, Is.SameAs(popupItem));
            Assert.That(fixture.List.SelectedKey, Is.EqualTo(FirstKey));
            ui.Pop();
            Assert.That(ui.SelectedItem, Is.SameAs(fixture.First));
        }

        [Test]
        public void SelectingAnotherKeyCancelsShowAndRejectsItsLateCallback()
        {
            var fixture = CreateListFixture();
            fixture.Adapter.DeferShow = true;
            Assert.That(fixture.List.Select(SecondKey), Is.True);
            var obsolete = fixture.Adapter.Shows[fixture.Adapter.Shows.Count - 1];
            Assert.That(fixture.List.Select(FirstKey), Is.True);
            var current = fixture.Adapter.Shows[fixture.Adapter.Shows.Count - 1];

            Assert.That(obsolete.Cancellation.IsCancellationRequested, Is.True);
            obsolete.Complete(true);
            Assert.That(fixture.List.IsPending, Is.True);
            Assert.That(ui.SelectedItem, Is.Null);
            Assert.That(fixture.List.SelectedKey, Is.EqualTo(FirstKey));
            current.Complete(true);
            Assert.That(fixture.List.IsPending, Is.False);
            Assert.That(ui.SelectedItem, Is.SameAs(fixture.First));
            obsolete.Complete(true);
            Assert.That(ui.SelectedItem, Is.SameAs(fixture.First));
        }

        [Test]
        public void SubmitIsBlockedWhileShowOrTheRealizedViewIsPending()
        {
            var fixture = CreateListFixture();
            var action = CreateAction(fixture.Second);
            var pressed = 0;
            action.onPressed.AddListener(() => pressed++);
            fixture.Adapter.Items.Remove(SecondKey);
            fixture.Adapter.DeferShow = true;
            fixture.List.Select(SecondKey);
            var show = fixture.Adapter.Shows[fixture.Adapter.Shows.Count - 1];

            Submit(GamepadPhase.Pressed);
            Assert.That(pressed, Is.Zero);
            show.Complete(true);
            Assert.That(fixture.List.IsPending, Is.True, "Show succeeded but no bound view exists yet.");
            Assert.That(ui.SelectedItem, Is.Null);
            Submit(GamepadPhase.Pressed);
            Assert.That(pressed, Is.Zero);
            Assert.That(fixture.List.Owns(fixture.Second), Is.False);

            fixture.Adapter.Items[SecondKey] = fixture.Second;
            fixture.Adapter.RaiseChanged();
            Assert.That(fixture.List.IsPending, Is.False);
            Assert.That(ui.SelectedItem, Is.SameAs(fixture.Second));
            Submit(GamepadPhase.Pressed);
            Assert.That(pressed, Is.EqualTo(1));
            Submit(GamepadPhase.Released);
        }

        [Test]
        public void SynchronousBoundaryBubblesToTheExplicitNeighborOutsideTheList()
        {
            var fixture = CreateListFixture();
            var outside = CreateItem(fixture.Focus, "Outside");
            SetField(fixture.First, "right", outside);
            fixture.Adapter.NeighborResult = new GamepadListResult(GamepadListStatus.Boundary);

            ui.Dispatch(new GamepadInput(GamepadCommands.Navigate, GamepadPhase.Pressed, Vector2.right));

            Assert.That(fixture.Adapter.Finds.Count, Is.EqualTo(1));
            Assert.That(fixture.List.IsPending, Is.False);
            Assert.That(ui.SelectedItem, Is.SameAs(outside));
            Assert.That(fixture.Focus.CurrentList, Is.Null);
            Assert.That(fixture.First.IsSelected, Is.False);
        }

        private void Submit(GamepadPhase phase)
        {
            ui.Dispatch(new GamepadInput(GamepadCommands.Submit, phase));
        }

        private GameObject CreateObject(string name, Transform parent = null)
        {
            var value = new GameObject(name, typeof(RectTransform));
            objects.Add(value);
            if (parent) value.transform.SetParent(parent, false);
            return value;
        }

        private GamepadFocus CreateFocus(string name)
        {
            var focus = CreateObject(name).AddComponent<GamepadFocus>();
            SetField(focus, "manager", ui);
            return focus;
        }

        private GamepadItem CreateItem(GamepadFocus focus, string name)
        {
            var item = CreateObject(name, focus.transform).AddComponent<GamepadItem>();
            if (GetField(item, "registeredFocus") == null) InvokeLifecycle(item, "OnEnable");
            return item;
        }

        private static GamepadAction CreateAction(GamepadItem item)
        {
            var action = item.gameObject.AddComponent<GamepadAction>();
            if (GetField(action, "registeredItem") == null) InvokeLifecycle(action, "OnEnable");
            return action;
        }

        private ListFixture CreateListFixture()
        {
            var focus = CreateFocus("ListFocus");
            var list = CreateObject("List", focus.transform).AddComponent<GamepadList>();
            if (GetField(list, "registeredFocus") == null) InvokeLifecycle(list, "OnEnable");
            var first = CreateItem(focus, "FirstView");
            var second = CreateItem(focus, "SecondView");
            var adapter = new FakeListAdapter();
            adapter.Keys.Add(FirstKey);
            adapter.Keys.Add(SecondKey);
            adapter.Items.Add(FirstKey, first);
            adapter.Items.Add(SecondKey, second);
            list.SetAdapter(adapter);
            first.Bind(list, FirstKey);
            second.Bind(list, SecondKey);
            SetField(focus, "defaultList", list);
            Assert.That(ui.Push(focus), Is.True);
            list.Activate();
            Assert.That(focus.CurrentList, Is.SameAs(list));
            Assert.That(ui.SelectedItem, Is.SameAs(first));
            Assert.That(list.IsPending, Is.False);
            return new ListFixture(focus, list, adapter, first, second);
        }

        private static object GetField(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private field: " + name);
            return field.GetValue(target);
        }

        private static void InvokeLifecycle(object target, string name)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing lifecycle method: " + name);
            method.Invoke(target, null);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing serialized field: " + name);
            field.SetValue(target, value);
        }

        private sealed class ListFixture
        {
            public readonly GamepadFocus Focus;
            public readonly GamepadList List;
            public readonly FakeListAdapter Adapter;
            public readonly GamepadItem First;
            public readonly GamepadItem Second;

            public ListFixture(GamepadFocus focus, GamepadList list, FakeListAdapter adapter,
                GamepadItem first, GamepadItem second)
            {
                Focus = focus;
                List = list;
                Adapter = adapter;
                First = first;
                Second = second;
            }
        }

        private sealed class FakeListAdapter : IGamepadList
        {
            public readonly List<string> Keys = new List<string>();
            public readonly Dictionary<string, GamepadItem> Items = new Dictionary<string, GamepadItem>();
            public readonly List<FindCall> Finds = new List<FindCall>();
            public readonly List<ShowCall> Shows = new List<ShowCall>();
            public bool DeferFind;
            public bool DeferShow;
            public bool RaiseChangedDuringShow;
            public GamepadListResult NeighborResult = new GamepadListResult(GamepadListStatus.Boundary);
            public event Action Changed;

            public bool IsValid(string key)
            {
                return Keys.Contains(key);
            }

            public bool TryGetInitial(out string key)
            {
                key = Keys.Count > 0 ? Keys[0] : null;
                return key != null;
            }

            public bool TryGetItem(string key, out GamepadItem item)
            {
                return Items.TryGetValue(key, out item) && item;
            }

            public void FindNeighbor(string key, Vector2 direction, CancellationToken cancellation,
                Action<GamepadListResult> completed)
            {
                var call = new FindCall(key, direction, cancellation, completed);
                Finds.Add(call);
                if (!DeferFind && !cancellation.IsCancellationRequested) call.Complete(NeighborResult);
            }

            public void Show(string key, GamepadListView view, CancellationToken cancellation,
                Action<bool> completed)
            {
                var call = new ShowCall(key, view, cancellation, completed);
                Shows.Add(call);
                if (RaiseChangedDuringShow) RaiseChanged();
                if (!DeferShow && !cancellation.IsCancellationRequested) call.Complete(true);
            }

            public void RaiseChanged()
            {
                Changed?.Invoke();
            }
        }

        private sealed class FindCall
        {
            public readonly string Key;
            public readonly Vector2 Direction;
            public readonly CancellationToken Cancellation;
            private readonly Action<GamepadListResult> completed;

            public FindCall(string key, Vector2 direction, CancellationToken cancellation,
                Action<GamepadListResult> completed)
            {
                Key = key;
                Direction = direction;
                Cancellation = cancellation;
                this.completed = completed;
            }

            public void Complete(GamepadListResult result)
            {
                completed(result);
            }
        }

        private sealed class ShowCall
        {
            public readonly string Key;
            public readonly GamepadListView View;
            public readonly CancellationToken Cancellation;
            private readonly Action<bool> completed;

            public ShowCall(string key, GamepadListView view, CancellationToken cancellation,
                Action<bool> completed)
            {
                Key = key;
                View = view;
                Cancellation = cancellation;
                this.completed = completed;
            }

            public void Complete(bool succeeded)
            {
                completed(succeeded);
            }
        }
    }
}
