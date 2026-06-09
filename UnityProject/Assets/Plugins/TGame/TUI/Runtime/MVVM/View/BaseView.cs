using UnityEngine;

namespace TGame.TUI.MVVM.View
{
    /// <summary>
    /// 基础视图类。决定是否响应数据变化并刷新界面。
    /// 对应 TFrameworV2 的 BaseView<T>。
    /// </summary>
    public abstract class BaseView<T> : MonoBehaviour where T : struct
    {
        /// <summary>
        /// 判断当前是否需要更新页面。
        /// 返回 true 时 OnRefreshView 会被调用。
        /// </summary>
        public abstract bool NeedRefreshView();

        /// <summary>
        /// 数据变化时刷新页面。
        /// </summary>
        /// <param name="newModel">新数据</param>
        /// <param name="oldModel">旧数据</param>
        public virtual void OnRefreshView(T newModel, T oldModel) { }
    }
}
