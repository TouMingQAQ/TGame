using TGame.TCore.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TGame.TUI
{
    /// <summary>
    /// DefaultToolTip 触发器 — 挂到 UI 元素上自动处理 IPointerEnter/Exit。
    ///
    /// 关键：浮窗必须弹在“该控件所属面板的 UIRoot”上，而不是全局 UIManager 的默认根，
    /// 否则在多 UIRoot / 多 Canvas 场景里会生成到另一个 Canvas，被当前面板盖住而看不见。
    /// 若控件不在任何面板内，则回退到 UIManager 的默认根。
    ///
    /// boundsArea: 指定边界 RectTransform,浮窗限定在该区域内。null = 全屏(默认)。
    /// followMouse: true = UIRoot 每帧拉鼠标坐标重定位; false = 固定进入点(默认)。
    /// </summary>
    public class DefaultToolTipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] [TextArea] private string _tooltipText = "Tooltip";
        [SerializeField] private PopupFlipDirection _direction = PopupFlipDirection.BottomRight;
        [SerializeField] private Vector2 _offset = new Vector2(15f, 15f);
        [SerializeField] private RectTransform _boundsArea;
        [SerializeField] private bool _followMouse;

        private UIRoot _root;

        /// <summary>提示文本（手柄聚焦时可用固定文本显示同一段内容）。</summary>
        public string TooltipText => _tooltipText;

        private UIRoot ResolveRoot()
        {
            if (_root) return _root;
            var panel = GetComponentInParent<BaseUIPanel>();
            if (panel && panel.Root)
            {
                _root = panel.Root;
                return _root;
            }
            var ui = Game.Instance ? Game.Instance.GetManager<UIManager>() : null;
            _root = ui ? ui.UIRoot : null;
            return _root;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var root = ResolveRoot();
            if (!root)
            {
                Debug.LogWarning("[DefaultToolTipTrigger] 未找到可用的 UIRoot，无法显示浮窗。", this);
                return;
            }
            var tip = root.ShowPopup<DefaultToolTip>(eventData.position, p => p.SetText(_tooltipText),
                boundsArea: _boundsArea, followMouse: _followMouse, flip: _direction, offset: _offset);
            if (tip == null)
                Debug.LogWarning("[DefaultToolTipTrigger] ShowPopup 返回 null，检查 DefaultToolTip 是否已注册。", this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var root = ResolveRoot();
            if (root) root.HidePopup<DefaultToolTip>();
        }
    }
}
