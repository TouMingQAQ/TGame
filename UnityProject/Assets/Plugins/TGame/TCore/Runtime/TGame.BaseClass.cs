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
            // 固定帧驱动：必须传 Time.fixedDeltaTime，否则 Time.deltaTime 在同一物理帧内
            // 可能被多次调用（脚本重入/插帧），导致定时器与结算出现累积误差。
            TickModules(Time.fixedDeltaTime);
        }

        /// <summary>
        /// 推进本 Manager 下所有已启用模块的 Tick。
        /// </summary>
        /// <param name="deltaTime">本帧时长（秒）</param>
        protected virtual void TickModules(float deltaTime)
        {
            // 遍历时复制 key，允许模块在 Tick 内增加/移除模块而不抛异常
            _tickKeyCache.Clear();
            foreach (var key in _moduleMap.Keys)
                _tickKeyCache.Add(key);

            for (int i = 0; i < _tickKeyCache.Count; i++)
            {
                if (_moduleMap.TryGetValue(_tickKeyCache[i], out var module) && module != null && module.Enable)
                    module.Tick(deltaTime);
            }
        }

        private readonly List<Type> _tickKeyCache = new();
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
