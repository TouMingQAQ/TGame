using System;

namespace TGame.TUI.MVVM.Model
{
    /// <summary>
    /// 数据变化事件委托，传递新旧两个值。
    /// </summary>
    public delegate void ModelEvent<in T>(T oldValue, T newValue) where T : struct;

    /// <summary>
    /// 基础数据类。包装值类型数据，提供变化通知。
    /// 对应 TFrameworV2 的 BaseModel<T>。
    /// </summary>
    public abstract class BaseModel<T> where T : struct
    {
        private bool _enable = true;
        private T _model;

        /// <summary>数据变化事件</summary>
        public event ModelEvent<T> onValueChanged;

        /// <summary>数据是否激活，false 时 SetModel 不生效</summary>
        public bool Enable
        {
            get => _enable;
            set => _enable = value;
        }

        /// <summary>当前数据</summary>
        public T Model => _model;

        /// <summary>
        /// 设置数据。会检查 Enable 和 NeedUpdateValue，通过后触发通知。
        /// </summary>
        public void SetModel(T model)
        {
            if (!_enable)
                return;
            if (!NeedUpdateValue(model, _model))
                return;
            onValueChanged?.Invoke(model, _model);
            _model = model;
        }

        /// <summary>
        /// 判断数据是否需要更新。子类实现自定义 dirty 检查。
        /// </summary>
        protected abstract bool NeedUpdateValue(T newValue, T oldValue);
    }
}
