using System;
using System.Collections.Generic;
using UnityEngine;

namespace TGame.TCore.Runtime
{
    [DefaultExecutionOrder(-8000)]
    public partial class BaseManager : MonoBehaviour,IModuleHost
    {
        protected Game game;

        #region Module

        private Dictionary<Type, BaseModule> _moduleMap = new();

        public T AddModule<T>() where T : BaseModule, new()
        {
            var type = typeof(T);
            if (_moduleMap.TryGetValue(type, out var value))
                return value as T;
            var module = new T();
            module.Init();
            module.Host = this;
            _moduleMap[type] = module;
            Debug.Log($"<color=#66ccff>[{GetType()}]</color> add module:{type}");
            return module;
        }

        public T RemoveModule<T>() where T : BaseModule,new()
        {
            var type = typeof(T);
            if (_moduleMap.Remove(type, out var value))
            {
                value.Destroy();
                return value as T;
            }
            return default;
        }

        public void ClearModule()
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
    }

    public interface IModuleHost
    {
        public T AddModule<T>() where T : BaseModule, new();
        public T RemoveModule<T>() where T : BaseModule, new();
        public T GetModule<T>() where T : BaseModule, new();
        public void ClearModule();
    }
    public class BaseModule
    {
        public IModuleHost Host { get; set; }

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
