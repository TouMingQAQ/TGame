using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TGame.TCore.Runtime;

namespace TGame.TUI
{
    /// <summary>
    /// UI 面板显隐 Module。
    /// 职责:Show/Hide 单个面板,SetAsLastSibling 保证渲染最上,广播 PanelOpenedEvent/PanelClosedEvent。
    ///
    /// 状态:无(不缓存,所有状态来自 UILoaderModule)
    ///
    /// 依赖:
    ///   - UILoaderModule:Load / LoadAsync / Get / IsLoaded
    ///   - UIManager.Call:广播 PanelOpenedEvent / PanelClosedEvent
    ///
    /// 调用链:
    ///   业务方 ShowPanel<T>() → UIManager.ShowPanel(转发) → 本模块 Show
    ///   业务方 ShowPanelAsync<T>() → UIManager.ShowPanelAsync(转发) → 本模块 ShowAsync
    ///   业务方 HidePanel<T>() → UIManager.HidePanel(转发) → 本模块 Hide
    ///   StackPanelModel.Open / OpenAsync → UIManager.LoadPanel / 本模块 Show(同步路径)
    ///     内部 panel.Show 也会广播 PanelOpenedEvent
    ///
    /// 关键不变量:
    ///   - Show 幂等:已可见时直接返回,不重复广播事件
    ///   - Hide 幂等:未加载或不可见时直接返回,不抛 NullRef
    ///   - 事件广播走 _ui.Call,业务方可注册监听(同 PanelOpenedEvent 等)
    /// </summary>
    public sealed class UIVisibilityModule : BaseModule
    {
        private UIManager _ui;

        /// <summary>由 UIManager.Start 注入自身引用</summary>
        public void SetUIManager(UIManager ui) => _ui = ui;

        // ===== Show(同步) =====

        /// <summary>显示面板(泛型)。Load + SetAsLastSibling + Show + Call(PanelOpenedEvent)</summary>
        public T Show<T>() where T : BaseUIPanel => Show(typeof(T)) as T;

        /// <summary>按 Type 显示面板。已可见时直接返回,无副作用。
        /// Addressable 模式下面必须改用 <see cref="ShowAsync(Type, CancellationToken)"/>。</summary>
        public BaseUIPanel Show(Type type)
        {
            var loader = _ui.GetModule<UILoaderModule>();
            var panel = loader.Load(type);
            if (panel == null) return null;
            if (panel.IsVisible && !panel.IsHiding) return panel;

            // 移至同层末尾 = 渲染在最上层
            panel.transform.SetAsLastSibling();
            panel.Show();
            _ui.Call(new PanelOpenedEvent(type.Name));
            return panel;
        }

        // ===== Show(异步,Addressable 模式走 AddressableModel.LoadAsync) =====

        /// <summary>异步显示面板(泛型)。LoadAsync + SetAsLastSibling + Show + Call(PanelOpenedEvent)</summary>
        public async UniTask<T> ShowAsync<T>(CancellationToken ct = default) where T : BaseUIPanel
            => (T)await ShowAsync(typeof(T), ct);

        /// <summary>异步按 Type 显示面板。已可见时直接返回,无副作用。
        /// 内部先 await UILoaderModule.LoadAsync,加载成功后 Show 并广播事件。</summary>
        public async UniTask<BaseUIPanel> ShowAsync(Type type, CancellationToken ct = default)
        {
            var loader = _ui.GetModule<UILoaderModule>();
            var panel = await loader.LoadAsync(type, ct);
            if (panel == null) return null;
            if (panel.IsVisible && !panel.IsHiding) return panel;

            panel.transform.SetAsLastSibling();
            panel.Show();
            _ui.Call(new PanelOpenedEvent(type.Name));
            return panel;
        }

        // ===== Hide =====

        /// <summary>隐藏面板(泛型)</summary>
        public void Hide<T>() where T : BaseUIPanel => Hide(typeof(T));

        /// <summary>按 Type 隐藏面板。未加载或不可见时直接返回</summary>
        public void Hide(Type type)
        {
            var loader = _ui.GetModule<UILoaderModule>();
            if (!loader.IsLoaded(type)) return;
            var panel = loader.Get(type);
            if (panel == null || !panel.IsVisible) return;

            panel.Hide();
            _ui.Call(new PanelClosedEvent(type.Name));
        }
    }
}
