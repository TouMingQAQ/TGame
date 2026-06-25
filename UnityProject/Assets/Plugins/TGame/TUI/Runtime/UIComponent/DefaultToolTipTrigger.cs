using TGame.TCore.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TGame.TUI
{
    /// <summary>
    /// DefaultToolTip 触发器 — 挂到 UI 元素上自动处理 IPointerEnter/Exit。
    ///
    /// boundsArea: 指定边界 RectTransform,浮窗限定在该区域内。
    ///            null = 全屏(默认)。
    /// followMouse: true = UIRoot 每帧拉鼠标坐标重定位; false = 固定进入点(默认)。
    /// </summary>
    public class DefaultToolTipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] [TextArea] private string _tooltipText = "Tooltip";
        [SerializeField] private PopupFlipDirection _direction = PopupFlipDirection.BottomRight;
        [SerializeField] private Vector2 _offset = new Vector2(15f, 15f);
        [SerializeField] private RectTransform _boundsArea;
        [SerializeField] private bool _followMouse;

        private UIManager _ui;

        private void Start()
        {
            _ui = Game.Instance.GetManager<UIManager>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _ui?.UIRoot.ShowPopup<DefaultToolTip>(eventData.position, p => p.SetText(_tooltipText),
                boundsArea: _boundsArea, followMouse: _followMouse, flip: _direction, offset: _offset);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _ui?.UIRoot.HidePopup<DefaultToolTip>();
        }
    }
}
