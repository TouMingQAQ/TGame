using System;
using System.Collections.Generic;

namespace TGame.TCore.Runtime
{
    /// <summary>
    /// 事件模块。
    ///
    /// 支持两级分发：
    ///   1. 全局总线（不带 scope）：<see cref="Call{T}(T)"/> / <see cref="Register{T}(Action{T})"/>
    ///   2. 作用域总线（带 scope，如 playerId / 单位 Id）：
    ///      <see cref="Call{T}(object,T)"/> / <see cref="Register{T}(object,Action{T})"/>
    ///
    /// 约定：
    ///   - 作用域事件只发给订阅了该 scope 的监听者，不会广播给全局监听者；
    ///   - 全局事件只发给全局监听者，不会下发给各 scope；
    ///   - 同一对象重复 Register 同一委托会被忽略，避免重复触发；
    ///   - Call 时对委托做了快照，允许监听者在回调里注册/注销而不抛异常。
    /// </summary>
    public class EventModule : BaseModule
    {
        protected Dictionary<Type, Delegate> _eventMap = new();

        /// <summary>
        /// 作用域事件表：scope -> (事件类型 -> 委托)
        /// </summary>
        protected Dictionary<object, Dictionary<Type, Delegate>> _scopedEventMap = new();

        #region 全局事件

        /// <summary>
        /// 广播全局事件
        /// </summary>
        public void Call<T>(T value)
        {
            if (!_eventMap.TryGetValue(typeof(T), out var existingDelegate))
                return;

            Notify(existingDelegate as Action<T>, value);
        }

        /// <summary>
        /// 注册全局事件
        /// </summary>
        public void Register<T>(Action<T> action)
        {
            if (action == null) return;
            Combine(ref _eventMap, typeof(T), action);
        }

        /// <summary>
        /// 注销全局事件
        /// </summary>
        public void UnRegister<T>(Action<T> action)
        {
            if (action == null) return;
            Remove(ref _eventMap, typeof(T), action);
        }

        #endregion

        #region 作用域事件

        /// <summary>
        /// 广播作用域事件（只发给订阅了该 scope 的监听者）。
        /// </summary>
        /// <param name="scope">作用域标识，通常是 playerId / 单位 Id / 连接对象</param>
        /// <param name="value">事件数据</param>
        public void Call<T>(object scope, T value)
        {
            if (scope == null)
            {
                // null scope 退化为全局事件，避免静默丢事件
                Call(value);
                return;
            }
            if (!_scopedEventMap.TryGetValue(scope, out var typeMap))
                return;
            if (!typeMap.TryGetValue(typeof(T), out var existingDelegate))
                return;

            Notify(existingDelegate as Action<T>, value);
        }

        /// <summary>
        /// 注册作用域事件
        /// </summary>
        /// <param name="scope">作用域标识</param>
        /// <param name="action">回调</param>
        public void Register<T>(object scope, Action<T> action)
        {
            if (scope == null || action == null) return;

            if (!_scopedEventMap.TryGetValue(scope, out var typeMap))
            {
                typeMap = new Dictionary<Type, Delegate>();
                _scopedEventMap[scope] = typeMap;
            }
            Combine(ref typeMap, typeof(T), action);
        }

        /// <summary>
        /// 注销作用域事件
        /// </summary>
        public void UnRegister<T>(object scope, Action<T> action)
        {
            if (scope == null || action == null) return;
            if (!_scopedEventMap.TryGetValue(scope, out var typeMap))
                return;

            Remove(ref typeMap, typeof(T), action);
            if (typeMap.Count == 0)
                _scopedEventMap.Remove(scope);
        }

        /// <summary>
        /// 一次性注销某个作用域下的所有监听（实体销毁时调用，防止泄漏）。
        /// </summary>
        public void ClearScope(object scope)
        {
            if (scope == null) return;
            _scopedEventMap.Remove(scope);
        }

        /// <summary>
        /// 注销所有全局与作用域监听。
        /// </summary>
        public void ClearAll()
        {
            _eventMap.Clear();
            _scopedEventMap.Clear();
        }

        /// <summary>
        /// 该作用域是否还有监听者。
        /// </summary>
        public bool HasScope(object scope)
        {
            return scope != null && _scopedEventMap.ContainsKey(scope);
        }

        #endregion

        public override void Destroy()
        {
            ClearAll();
        }

        #region 内部工具

        private static void Notify<T>(Action<T> action, T value)
        {
            if (action == null)
                return;

            // 快照：监听者可能在回调里改订阅，直接 Invoke 多播委托会导致无效委托被调用
            var invocationList = action.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<T>)invocationList[i]).Invoke(value);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[EventModule] 事件 {typeof(T)} 监听者抛异常: {e}");
                }
            }
        }

        private static void Combine(ref Dictionary<Type, Delegate> map, Type eventType, Delegate action)
        {
            if (map.TryGetValue(eventType, out var existingDelegate))
            {
                // 同一委托重复注册直接忽略，避免一次 Call 触发多次
                if (existingDelegate != null && existingDelegate.GetInvocationList().Length > 0)
                {
                    foreach (var item in existingDelegate.GetInvocationList())
                    {
                        if (item.Equals(action) || item.Target == action.Target && item.Method == action.Method)
                            return;
                    }
                }
                map[eventType] = Delegate.Combine(existingDelegate, action);
            }
            else
            {
                map[eventType] = action;
            }
        }

        private static void Remove(ref Dictionary<Type, Delegate> map, Type eventType, Delegate action)
        {
            if (!map.TryGetValue(eventType, out var existingDelegate))
                return;

            var newDelegate = Delegate.Remove(existingDelegate, action);
            if (newDelegate == null)
                map.Remove(eventType);
            else
                map[eventType] = newDelegate;
        }

        #endregion
    }
}
