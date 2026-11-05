using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TGame.Gamepad
{
    /// <summary>
    /// 可选的接入辅助组件：给已有 uGUI 层级增量挂载 GamepadItem / GamepadAction，
    /// 不替换原控件，适合直接为没有手柄支持的游戏补上手柄操作。
    /// 放在 Canvas 下使用，会自动创建管理器、聚焦区域与共享光标。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TGame/Gamepad/Gamepad Auto Binder")]
    public sealed class GamepadAutoBinder : MonoBehaviour
    {
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private bool autoCollect = true;
        [SerializeField, Min(0.1f)] private float rescanInterval = 0.5f;
        [SerializeField] private bool createCursor = true;
        [SerializeField] private Vector2 cursorSize = new Vector2(48f, 48f);
        [SerializeField] private Color cursorColor = new Color(1f, 0.85f, 0.2f, 0.85f);
        [SerializeField] private bool clickOnSubmit = true;
        [SerializeField] private bool verboseLog = true;

        private GamepadUI ui;
        private GamepadFocus focus;
        private GamepadCursor cursor;
        private float nextScan;
        private float enableTime;
        private bool warnedNoCanvas;
        private bool warnedNoSelectable;
        private int lastBoundCount = -1;
        private readonly Dictionary<Selectable, GamepadItem> viewItems = new Dictionary<Selectable, GamepadItem>();
        private readonly List<Selectable> dead = new List<Selectable>();
        private readonly List<Selectable> selectableBuffer = new List<Selectable>();

        public GamepadUI Manager => ui;
        public GamepadFocus Focus => focus;
        public int BoundCount => viewItems.Count;

        private void OnEnable()
        {
            enableTime = Time.unscaledTime;
            EnsureAll();
            nextScan = Time.unscaledTime + Mathf.Max(0.1f, rescanInterval);
        }

        private void Update()
        {
            if (!ui || !focus)
            {
                EnsureAll();
                return;
            }
            if (!autoCollect) return;
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + Mathf.Max(0.1f, rescanInterval);
            Scan();
        }

        private void OnGUI()
        {
            if (!verboseLog) return;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            var selected = ui ? ui.SelectedItem : null;
            var text = $"[Gamepad] mgr={(ui ? ui.name : "none")} pad={(pad != null ? pad.displayName : "none")} bound={BoundCount} sel={(selected ? selected.name : "none")}";
            GUI.Box(new Rect(8f, 8f, 620f, 24f), text);
        }

        /// <summary>立即重新收集绑定，适合面板打开后主动调用。</summary>
        public void RebindNow()
        {
            EnsureAll();
            Scan();
        }

        private void EnsureAll()
        {
            EnsureManager();
            EnsureFocus();
            EnsureCursor();
            if (autoCollect) Scan();
        }

        private Canvas ResolveCanvas()
        {
            if (targetCanvas) return targetCanvas;
            targetCanvas = GetComponentInParent<Canvas>();
            if (!targetCanvas) targetCanvas = FindAnyObjectByType<Canvas>();
            return targetCanvas;
        }

        private void EnsureManager()
        {
            if (ui) return;
            ui = GamepadUI.Instance;
            if (ui) return;
            var go = new GameObject("GamepadUI");
            go.transform.SetParent(transform, false);
            ui = go.AddComponent<GamepadUI>();
            if (verboseLog) Debug.Log("[GamepadAutoBinder] 创建 GamepadUI 管理器。", this);
        }

        private void EnsureFocus()
        {
            if (focus && focus.isActiveAndEnabled) return;
            var canvas = ResolveCanvas();
            var parent = canvas ? canvas.transform : transform;
            if (!canvas && !warnedNoCanvas)
            {
                warnedNoCanvas = true;
                Debug.LogWarning("[GamepadAutoBinder] 未找到 Canvas，无法创建 UI 聚焦区域，请把本组件放到 Canvas 下。", this);
            }
            var go = new GameObject("GamepadFocus", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            focus = go.AddComponent<GamepadFocus>();
            focus.WrapNavigation = false;
            ui.Push(focus);
            if (verboseLog) Debug.Log("[GamepadAutoBinder] 创建 GamepadFocus 并压入聚焦栈。", this);
        }

        private void EnsureCursor()
        {
            if (!createCursor || !focus) return;
            if (cursor && cursor.isActiveAndEnabled) return;
            var go = new GameObject("GamepadCursor", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(focus.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = cursorSize;
            var image = go.GetComponent<Image>();
            image.color = cursorColor;
            image.raycastTarget = false;
            cursor = go.AddComponent<GamepadCursor>();
            focus.SetCursor(cursor);
        }

        private void Scan()
        {
            if (!focus) return;
            var root = ResolveCanvas();
            if (!root) return;

            dead.Clear();
            foreach (var pair in viewItems)
                if (!pair.Key) dead.Add(pair.Key);
            foreach (var key in dead) viewItems.Remove(key);

            selectableBuffer.Clear();
            // Include inactive so panels that are loaded but hidden are prepared too;
            // GamepadItem.IsNavigable gates them until their GameObject becomes active.
            root.GetComponentsInChildren(true, selectableBuffer);
            foreach (var selectable in selectableBuffer)
            {
                if (!selectable || viewItems.ContainsKey(selectable)) continue;
                Bind(selectable);
            }

            if (viewItems.Count != lastBoundCount)
            {
                lastBoundCount = viewItems.Count;
                if (verboseLog) Debug.Log($"[GamepadAutoBinder] 当前已绑定可导航控件: {viewItems.Count}", this);
            }
            if (viewItems.Count == 0 && !warnedNoSelectable && Time.unscaledTime - enableTime > 3f)
            {
                warnedNoSelectable = true;
                Debug.LogWarning("[GamepadAutoBinder] 3 秒内没有找到任何 Selectable，检查 Canvas 下是否有可点击控件。", this);
            }
        }

        private void Bind(Selectable selectable)
        {
            var go = selectable.gameObject;
            var item = go.GetComponent<GamepadItem>();
            if (!item) item = go.AddComponent<GamepadItem>();
            item.SetSelectable(selectable);
            item.SetCursorAnchor(selectable.transform as RectTransform);
            item.SetFocus(focus);
            viewItems[selectable] = item;

            if (clickOnSubmit && selectable is IPointerClickHandler)
            {
                var action = go.GetComponent<GamepadAction>();
                if (!action)
                {
                    action = go.AddComponent<GamepadAction>();
                    action.Configure(item, GamepadCommands.Submit, true);
                }
                action.InvokePointerClick = true;
            }
        }
    }
}
