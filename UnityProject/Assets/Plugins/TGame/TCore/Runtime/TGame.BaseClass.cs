using System;
using System.Collections.Generic;
using UnityEngine;

namespace TGame.TCore.Runtime
{
    [DefaultExecutionOrder(-8000)]
    public partial class BaseManager : MonoBehaviour
    {
        protected Game game;

        #region Module

        protected Dictionary<Type, BaseModule> _moduleMap = new();

        protected T AddModule<T>() where T : BaseModule, new()
        {
            var type = typeof(T);
            if (_moduleMap.TryGetValue(type, out var value))
                return value as T;
            var module = new T();
            module.Init();
            _moduleMap[type] = module;
            Debug.Log($"<color=#66ccff>[{GetType()}]</color> add module:{type}");
            return module;
        }

        protected T RemoveModule<T>() where T : BaseModule
        {
            var type = typeof(T);
            if (_moduleMap.Remove(type, out var value))
            {
                value.Destroy();
                return value as T;
            }
            return default;
        }

        protected void ClearModule()
        {
            foreach (var module in _moduleMap.Values)
            {
                module.Destroy();
            }
            _moduleMap.Clear();
        }

        public T GetModule<T>() where T : BaseModule, new()
        {
            var type = typeof(T);
            if (_moduleMap.TryGetValue(type, out var value))
                return value as T;
            return AddModule<T>();
        }

        #endregion

        protected T GetManager<T>() where T : BaseManager
        {
            return game.GetManager<T>();
        }

        protected virtual void FixedUpdate()
        {
            foreach (var module in _moduleMap.Values)
            {
                if (module.Enable)
                    module.Tick(Time.deltaTime);
            }
        }

        #region EventMethod

        protected Dictionary<Type, Delegate> _eventMap = new();

        public void Call<T>(T value = default)
        {
            var eventType = typeof(T);
            if (_eventMap.TryGetValue(eventType, out var existingDelegate))
            {
                var action = existingDelegate as Action<T>;
                action?.Invoke(value);
            }
        }

        protected void Register<T>(Action<T> action)
        {
            if (action == null) return;
            var eventType = typeof(T);
            if (_eventMap.TryGetValue(eventType, out var existingDelegate))
                _eventMap[eventType] = Delegate.Combine(existingDelegate, action);
            else
                _eventMap[eventType] = action;
        }

        protected void UnRegister<T>(Action<T> action)
        {
            if (action == null) return;
            var eventType = typeof(T);
            if (_eventMap.TryGetValue(eventType, out var existingDelegate))
            {
                var newDelegate = Delegate.Remove(existingDelegate, action);
                if (newDelegate == null)
                    _eventMap.Remove(eventType);
                else
                    _eventMap[eventType] = newDelegate;
            }
        }

        #endregion
    }
    public class BaseModule
    {
        private bool _enable = false;

        public virtual bool Enable
        {
            get => _enable;
            set => _enable = value;
        }

        public virtual void Init() { }
        public virtual void Destroy() { }
        public virtual void Tick(float deltaTime) { }
    }
}
