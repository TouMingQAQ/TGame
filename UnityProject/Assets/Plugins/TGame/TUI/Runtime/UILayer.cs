using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// UI 面板层级枚举，值对应 Canvas.sortingOrder 范围
    /// </summary>
    public enum UILayer
    {
        /// <summary>背景层，sortingOrder=0</summary>
        Background = 0,
        /// <summary>场景层，sortingOrder=100</summary>
        Scene = 100,
        /// <summary>普通层，sortingOrder=200</summary>
        Normal = 200,
        /// <summary>弹窗层，sortingOrder=300</summary>
        Popup = 300,
        /// <summary>覆盖层，sortingOrder=400</summary>
        Overlay = 400,
        /// <summary>顶层，sortingOrder=500</summary>
        Top = 500
    }
}
