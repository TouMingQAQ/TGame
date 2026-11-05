using TMPro;
using TGame.Gamepad;
using TGame.TUI;
using UnityEngine;

namespace TGame.Sample
{
    /// <summary>
    /// 手柄聚焦提示文本：当手柄选中本界面内的目标时，把该目标的提示文本写到界面固定的文本位置，
    /// 而不是弹出跟随鼠标的浮窗。挂在面板根节点上；未指定 target 时可自动创建一个底部文本。
    /// 鼠标悬停仍走 DefaultToolTipTrigger 的浮窗逻辑，两者互不影响。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamepadTooltipHint : MonoBehaviour
    {
        [SerializeField] private TMP_Text target;
        [SerializeField] private Transform scope;
        [SerializeField] private GamepadUI manager;
        [SerializeField] private bool autoCreate = true;
        [SerializeField] private bool clearWhenNoSelection = true;
        [SerializeField] private string format = "{0}";
        [SerializeField] private Vector2 padding = new Vector2(24f, 20f);
        [SerializeField] private float height = 36f;

        private GamepadUI ui;
        private GamepadItem current;

        private Transform Scope => scope ? scope : transform;

        private void Update()
        {
            var active = manager ? manager : (ui ? ui : ui = GamepadUI.Instance);
            if (!active)
            {
                Clear();
                return;
            }

            var selected = active.SelectedItem;
            if (selected == current) return;

            var wasOurs = current && current.transform.IsChildOf(Scope);
            current = selected;

            if (selected && selected.transform.IsChildOf(Scope))
            {
                Show(ResolveText(selected));
            }
            else if (wasOurs && clearWhenNoSelection)
            {
                Clear();
            }
        }

        private static string ResolveText(GamepadItem item)
        {
            var trigger = item.GetComponent<DefaultToolTipTrigger>();
            if (trigger != null && !string.IsNullOrEmpty(trigger.TooltipText)) return trigger.TooltipText;
            return item.gameObject.name;
        }

        private void Show(string text)
        {
            if (!EnsureTarget()) return;
            target.text = string.IsNullOrEmpty(text) ? string.Empty : string.Format(format, text);
        }

        private void Clear()
        {
            if (target) target.text = string.Empty;
        }

        private bool EnsureTarget()
        {
            if (target) return true;
            if (!autoCreate)
            {
                Debug.LogWarning("[GamepadTooltipHint] 未指定 Target，且未开启 Auto Create。", this);
                return false;
            }

            var go = new GameObject("GamepadHintText", typeof(RectTransform));
            go.transform.SetParent(Scope, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(padding.x, padding.y);
            rect.offsetMax = new Vector2(-padding.x, padding.y + height);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 24f;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;

            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                var existing = Scope.GetComponentInChildren<TMP_Text>(true);
                if (existing) font = existing.font;
            }
            if (font != null) text.font = font;
            else Debug.LogWarning("[GamepadTooltipHint] 没有可用的 TMP 字体，自动创建的提示文本可能不可见。", this);

            target = text;
            return true;
        }
    }
}
