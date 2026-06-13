using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// UIManager 配置资产(ScriptableObject)。
    /// 装载"非场景配置"类资源:默认浮窗 prefab、全局浮窗参数等。
    /// **不**装场景引用(layer root、canvas 等)— 那些仍由 UIManager.prefab 自身 SerializeField 管理。
    ///
    /// 使用方式:
    ///   1. Project 视图右键 → Create → TGame/UI/UI Config → 命名(如 UIConfig.asset)
    ///   2. Inspector 填 defaultTooltip prefab + 全局参数
    ///   3. 拖到 UIManager.prefab 的 _config 字段
    ///   4. UIManager.Awake 自动读取并 RegisterPopup(defaultTooltip)
    /// </summary>
    [CreateAssetMenu(fileName = "UIConfig", menuName = "TGame/UI/UI Config")]
    public class UIConfig : ScriptableObject
    {
        [Header("Default Tooltip")]
        [Tooltip("默认浮窗 prefab。UIManager.Awake 会自动注册到 PopupModule,业务方可直接 ShowPopup<DefaultToolTip> 使用")]
        [SerializeField] private DefaultToolTip _defaultTooltip;

        [Header("Global Popup Settings")]
        [Tooltip("浮窗边到鼠标点的最小距离(像素)。ShowPopup 传入的 offset 参数可临时覆盖")]
        [SerializeField] private float _tooltipOffset = 8f;

        [Tooltip("浮窗默认首选翻转方向。ShowPopup 传入的 flip 参数可临时覆盖")]
        [SerializeField] private PopupFlipDirection _tooltipFlipDirection = PopupFlipDirection.BottomRight;

        /// <summary>默认浮窗 prefab(只读)</summary>
        public DefaultToolTip DefaultTooltip => _defaultTooltip;

        /// <summary>全局浮窗默认偏移(像素)</summary>
        public float TooltipOffset => _tooltipOffset;

        /// <summary>全局浮窗默认首选翻转方向</summary>
        public PopupFlipDirection TooltipFlipDirection => _tooltipFlipDirection;
    }
}
