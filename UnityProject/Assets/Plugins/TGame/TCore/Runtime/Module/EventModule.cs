using System;
using System.Collections.Generic;

namespace TGame.TCore.Runtime
{
    /// <summary>
    /// 事件模块
    /// </summary>
    public class EventModule : BaseModule
    {
        protected Dictionary<Type, Delegate> _eventMap = new();

        /// <summary>
        /// 广播事件
        /// </summary>
        /// <param name="value"></param>
        /// <typeparam name="T"></typeparam>
        public void Call<T>(T value)
        {
            var eventType = typeof(T);

            // 如果事件存在于字典中，转换为 Action<T> 并调用
            if (_eventMap.TryGetValue(eventType, out var existingDelegate))
            {
                var action = existingDelegate as Action<T>;
                action?.Invoke(value);
            }
        }
        /// <summary>
        /// 注册事件
        /// </summary>
        /// <param name="action"></param>
        /// <typeparam name="T"></typeparam>
        public void Register<T>(Action<T> action)
        {
            if (action == null) return;

            var eventType = typeof(T);

            // 如果当前事件类型已经存在于字典中，将新的事件委托与现有的委托组合
            if (_eventMap.TryGetValue(eventType, out var existingDelegate))
            {
                _eventMap[eventType] = Delegate.Combine(existingDelegate, action);
            }
            else
            {
                // 否则直接添加新的事件委托
                _eventMap[eventType] = action;
            }
        }
        /// <summary>
        /// 注销事件
        /// </summary>
        /// <param name="action"></param>
        /// <typeparam name="T"></typeparam>
        public void UnRegister<T>(Action<T> action)
        {
            if (action == null) return;

            var eventType = typeof(T);

            // 如果事件存在于字典中，移除指定的事件委托
            if (_eventMap.TryGetValue(eventType, out var existingDelegate))
            {
                var newDelegate = Delegate.Remove(existingDelegate, action);
                if (newDelegate == null)
                {
                    _eventMap.Remove(eventType);
                }
                else
                {
                    _eventMap[eventType] = newDelegate;
                }
            }
        }
    }
}