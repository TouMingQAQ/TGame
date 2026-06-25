using System;
using System.Collections.Generic;
using UnityEngine;

namespace TGame.TCore.Runtime
{
     /// <summary>
    /// 通用 Model 挂载器。可挂在任意 MonoBehaviour 上(无需注册到 Game 单例),
    /// 用于给非 Manager 组件(如 UIRoot)提供 BaseModule 容器能力。
    ///
    /// 与 BaseManager 的差异:
    ///   - 不持有 Game 引用,无 Game.AddManager / Game.GetManager 参与
    ///   - 不在 Game 单例注册表中,生命周期与宿主 MonoBehaviour 绑定
    ///   - Tick 走 Update + unscaledDeltaTime(UI 场景下用)
    ///   - 独立的事件总线(host.Call / host.Register),Module 通过 host 广播
    ///
    /// 调用方:
    ///   - 在宿主组件的 Init/Awake 中调 AddModule&lt;T&gt;() 预热模块
    ///   - 通过 GetModule&lt;T&gt;() 获取已注册模块
    ///   - OnDestroy 由基类自动 ClearModule
    /// </summary>
    public class ModuleEntity : MonoBehaviour,IModuleHost
    {
        private readonly Dictionary<Type, BaseModule> _moduleMap = new();

        #region Module

        public T AddModule<T>() where T : BaseModule, new()
        {
            var type = typeof(T);
            if (_moduleMap.TryGetValue(type, out var value))
                return value as T;
            var module = new T();
            module.Init();
            module.Host = this;
            _moduleMap[type] = module;
            return module;
        }

        public T RemoveModule<T>() where T : BaseModule, new()
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
        

        protected virtual void Update()
        {
            // 拷贝 keys,防止模块在 Tick 内增减导致枚举异常
            var keys = new List<Type>(_moduleMap.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                if (_moduleMap.TryGetValue(keys[i], out var module) && module.Enable)
                {
                    module.Tick(Time.unscaledDeltaTime);
                }
            }
        }

        protected virtual void OnDestroy()
        {
            ClearModule();
        }
    }
}