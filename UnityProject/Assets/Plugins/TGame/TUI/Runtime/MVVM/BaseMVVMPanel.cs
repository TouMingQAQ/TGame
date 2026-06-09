using UnityEngine;
using TGame.TUI;
using TGame.TUI.MVVM.Model;
using TGame.TUI.MVVM.View;

namespace TGame.TUI.MVVM
{
    /// <summary>
    /// MVVM 面板基类。融合 ViewModel 和面板功能：
    /// - 继承 BaseUIPanel 获得 DOTween 动画
    /// - 通过 BindModel() 连接 BaseModel 数据层
    /// - 通过 SerializeField 引用 BaseView 视图层
    /// - 数据变化时自动推送至 View（NeedRefreshView → OnRefreshView）
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public abstract class BaseMVVMPanel<T> : BaseUIPanel where T : struct
    {
        protected BaseModel<T> _model;

        [SerializeField]
        protected BaseView<T> _view;

        /// <summary>当前数据</summary>
        public T Data => _model?.Model ?? default;

        /// <summary>绑定的 View</summary>
        public BaseView<T> View => _view;

        #region Model Binding

        /// <summary>
        /// 绑定数据模型。
        /// </summary>
        public void BindModel(BaseModel<T> model)
        {
            if (_model != null)
                UnBindModel();

            _model = model;
            if (_model != null)
                _model.onValueChanged += OnSetModel;
        }

        /// <summary>
        /// 解绑数据模型。
        /// </summary>
        public void UnBindModel()
        {
            if (_model == null)
                return;

            _model.onValueChanged -= OnSetModel;
            _model = null;
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// 激活时自动刷新视图
        /// </summary>
        protected virtual void OnEnable()
        {
            if (_model != null && _view != null)
                _view.OnRefreshView(_model.Model, _model.Model);
        }

        /// <summary>
        /// 数据变化时由模型通知触发。
        /// </summary>
        protected virtual void OnSetModel(T newModel, T oldModel)
        {
            if (_view == null)
                return;

            if (_view.NeedRefreshView())
                _view.OnRefreshView(newModel, oldModel);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnBindModel();
        }

        #endregion
    }
}

