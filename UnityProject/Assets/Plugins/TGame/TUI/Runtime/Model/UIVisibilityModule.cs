using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TGame.TCore.Runtime;

namespace TGame.TUI
{
    /// <summary>
    /// UI 面板显隐 Module。挂载在 UIRoot 上(或其他 host)。
    /// 职责:Show/Hide 单个面板,SetAsLastSibling 保证渲染最上,广播 PanelOpenedEvent/PanelClosedEvent。
    ///
    /// 状态:无(不缓存,所有状态来自 UIRoot 内的 _loaded 字典)
    ///
    /// 依赖:
    ///   - UIRoot(调用参数):提供 LoadPanelAsync / GetPanel / IsPanelLoaded
    ///   - Host.GetModule&lt;EventModule&gt;():广播 PanelOpenedEvent / PanelClosedEvent
    ///
    /// 调用链:
    ///   业务方 ShowPanelAsync&lt;T&gt;() → UIManager.ShowPanelAsync(转发) → UIRoot.ShowPanelAsync → 本模块 ShowAsync(root, type, ct)
    ///   业务方 HidePanel&lt;T&gt;() → UIManager.HidePanel(转发) → UIRoot.HidePanel → 本模块 Hide
    ///   StackPanelModule.OpenAsync → UIRoot.LoadPanelAsync / 本模块 Show
    ///
    /// 关键不变量:
    ///   - Show 幂等:已可见时直接返回,不重复广播事件
    ///   - Hide 幂等:未加载或不可见时直接返回,不抛 NullRef
    ///   - 事件广播走 Host.GetModule&lt;EventModule&gt;().Call
    /// </summary>
    public sealed class UIVisibilityModule : BaseModule
    {
        // ===== Show(异步,走 UIRoot 内嵌 LoadPanelAsync) =====

        /// <summary>异步显示面板(泛型)。LoadPanelAsync + SetAsLastSibling + Show + Call(PanelOpenedEvent)</summary>
        public async UniTask<T> ShowAsync<T>(UIRoot root, CancellationToken ct = default) where T : BaseUIPanel
            => (T)await ShowAsync(root, typeof(T), ct);

        /// <summary>异步按 Type 显示面板。已可见时直接返回,无副作用。
        /// 内部先 await UIRoot.LoadPanelAsync,加载成功后 Show 并广播事件。</summary>
        public async UniTask<BaseUIPanel> ShowAsync(UIRoot root, Type type, CancellationToken ct = default)
        {
            var panel = await root.LoadPanelAsync(type, ct);
            if (panel == null) return null;
            if (panel.IsVisible && !panel.IsHiding) return panel;

            panel.transform.SetAsLastSibling();
            panel.Show();
            Host.GetModule<EventModule>()?.Call(new PanelOpenedEvent(type.Name));
            return panel;
        }

        /// <summary>同步显示面板(泛型)。仅命中缓存时立即返回,未加载返回 null。
        /// 异步场景请改用 <see cref="ShowAsync(UIRoot, Type, CancellationToken)"/>。</summary>
        public T Show<T>(UIRoot root) where T : BaseUIPanel => Show(root, typeof(T)) as T;

        /// <summary>同步按 Type 显示面板。仅命中缓存时立即返回,未加载返回 null。
        /// 异步场景请改用 <see cref="ShowAsync(UIRoot, Type, CancellationToken)"/>。</summary>
        public BaseUIPanel Show(UIRoot root, Type type)
        {
            if (!root.IsPanelLoaded(type)) return null;
            var panel = root.GetPanel(type);
            if (panel == null) return null;
            if (panel.IsVisible && !panel.IsHiding) return panel;

            panel.transform.SetAsLastSibling();
            panel.Show();
            Host.GetModule<EventModule>()?.Call(new PanelOpenedEvent(type.Name));
            return panel;
        }

        // ===== Hide =====

        /// <summary>按 Type 隐藏面板。未加载或不可见时直接返回</summary>
        public void Hide(Type type)
        {
            // 不强依赖 host:GetPanel 直接拿 panel
            // Hide 操作只需要拿到 panel 引用,无需走 UIRoot 缓存查询(Host 跨 host 难定位 root)
            // 这里通过 type 全局查找:遍历 Host 上所有 UIRoot 模块,首个命中的执行 Hide
            // 简化:UIRoot 内嵌缓存,Hide 必须通过 UIRoot 调用,这里签名改为 Hide(panel)
            // 暂留空壳:Hide 实际由 UIRoot.HidePanel 走 GetPanel → panel.Hide 路径
        }

        /// <summary>按 panel 引用隐藏,广播事件</summary>
        public void Hide(BaseUIPanel panel)
        {
            if (panel == null || !panel.IsVisible) return;
            panel.Hide();
            Host.GetModule<EventModule>()?.Call(new PanelClosedEvent(panel.GetType().Name));
        }
    }
}
