using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TGame.Gamepad
{
    /// <summary>
    /// 把一个界面/面板变成独立的聚焦目标。挂在面板根节点上：
    /// 面板显示时由业务代码调用 <see cref="ApplyFocus"/>，隐藏时调用 <see cref="RemoveFocus"/>。
    /// 不依赖 GameObject 激活状态，聚焦/取消聚焦完全由代码显式驱动。
    /// 面板内的 Selectable 会自动挂 GamepadItem / GamepadAction，并归属于本面板的 GamepadFocus。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TGame/Gamepad/Gamepad Panel")]
    public sealed class GamepadPanel : MonoBehaviour
    {
        [SerializeField] private GamepadUI manager;
        [SerializeField] private GamepadFocus focus;
        [SerializeField] private bool autoBindSelectables = true;
        [Tooltip("本面板使用的游标预制体（可为每个面板配置不同游标）。为空则不显示游标。")]
        [SerializeField] private GameObject cursorPrefab;

        private GameObject cursorInstance;
        private bool applied;
        private readonly List<Selectable> buffer = new List<Selectable>();

        public GamepadUI UI => manager ? manager : GamepadUI.Instance;
        public GamepadFocus Focus => focus;
        public bool IsFocused => applied;

        private void Awake()
        {
            EnsureFocus();
        }

        private void OnDisable()
        {
            if (applied) RemoveFocus();
        }

        public void SetCursorPrefab(GameObject prefab) { cursorPrefab = prefab; }

        /// <summary>显式让本面板成为当前聚焦目标。</summary>
        public void ApplyFocus()
        {
            var ui = UI;
            if (!ui)
            {
                Debug.LogWarning("[GamepadPanel] 未找到 GamepadUI，无法应用聚焦。", this);
                return;
            }
            EnsureFocus();
            EnsureCursor();
            if (autoBindSelectables) Bind();
            if (applied && ui.CurrentFocus == focus) return;
            if (applied) ui.Remove(focus);
            ui.Push(focus);
            applied = true;
        }

        /// <summary>显式取消本面板的聚焦，交由下层界面恢复。</summary>
        public void RemoveFocus()
        {
            var ui = UI;
            if (ui && focus && applied) ui.Remove(focus);
            applied = false;
        }

        private void EnsureFocus()
        {
            if (!focus) focus = GetComponent<GamepadFocus>();
            if (!focus) focus = gameObject.AddComponent<GamepadFocus>();
        }

        private void EnsureCursor()
        {
            if (!focus || !cursorPrefab || cursorInstance) return;
            cursorInstance = Instantiate(cursorPrefab, focus.transform);
            cursorInstance.name = cursorPrefab.name;
            foreach (var behaviour in cursorInstance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is IGamepadCursor)
                {
                    focus.SetCursor(behaviour);
                    break;
                }
            }
        }

        private void Bind()
        {
            buffer.Clear();
            GetComponentsInChildren(true, buffer);
            foreach (var selectable in buffer)
            {
                if (!selectable) continue;
                var go = selectable.gameObject;
                var item = go.GetComponent<GamepadItem>();
                if (!item) item = go.AddComponent<GamepadItem>();
                item.SetSelectable(selectable);
                item.SetCursorAnchor(selectable.transform as RectTransform);
                item.SetFocus(focus);
                if (selectable is IPointerClickHandler)
                {
                    var action = go.GetComponent<GamepadAction>();
                    if (!action)
                    {
                        action = go.AddComponent<GamepadAction>();
                        action.Configure(item, GamepadCommands.Submit, true);
                    }
                    // 预制体里已配置的 onPressed 监听保留不动，只开启“自动点击自身”。
                    action.InvokePointerClick = true;
                }
            }
        }
    }
}
