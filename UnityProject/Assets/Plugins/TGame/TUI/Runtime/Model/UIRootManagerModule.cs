using System;
using System.Collections.Generic;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// UIRoot 管理 Module。挂在 UIManager 下,管理所有自定义 UIRoot 实例(按 Type 键索引)。
    /// 默认 UIRoot 通过 RegisterDefault 注入,不参与 Type 键字典。
    ///
    /// 使用方式:
    ///   1. 业务方在启动时创建自定义 UIRoot 子类实例,调用 Init(UIManager) 初始化
    ///   2. 通过 Register&lt;T&gt;(root) 注册到本 Module
    ///   3. 其他组件通过 Get&lt;T&gt;() 按类型查找 UIRoot 并调用其面板 API
    /// </summary>
    public sealed class UIRootManagerModule : BaseModule
    {
        private readonly Dictionary<Type, UIRoot> _roots = new();

        /// <summary>
        /// 注册自定义 UIRoot,以其具体类型为键。
        /// 同 Type 重复注册会被拒绝并 LogWarning。
        /// </summary>
        public void Register<T>(T root) where T : UIRoot
        {
            var type = typeof(T);
            if (_roots.ContainsKey(type))
            {
                Debug.LogWarning($"[UIRootManagerModule] UIRoot {type.Name} already registered, skipping");
                return;
            }
            _roots[type] = root;
        }

        /// <summary>
        /// 以任意 Type 为键注册自定义 UIRoot。
        /// </summary>
        public void Register(Type key, UIRoot root)
        {
            if (_roots.ContainsKey(key))
            {
                Debug.LogWarning($"[UIRootManagerModule] UIRoot key {key.Name} already registered, skipping");
                return;
            }
            _roots[key] = root;
        }

        /// <summary>按具体类型获取自定义 UIRoot,未找到返回 null</summary>
        public T Get<T>() where T : UIRoot
        {
            if (_roots.TryGetValue(typeof(T), out var root))
                return root as T;
            return null;
        }

        /// <summary>按任意 Type 键获取自定义 UIRoot,未找到返回 null</summary>
        public UIRoot Get(Type key)
        {
            _roots.TryGetValue(key, out var root);
            return root;
        }

        /// <summary>注销自定义 UIRoot(按具体类型)</summary>
        public bool Unregister<T>() where T : UIRoot => _roots.Remove(typeof(T));

        /// <summary>注销自定义 UIRoot(按 Type 键)</summary>
        public bool Unregister(Type key) => _roots.Remove(key);

        /// <summary>所有已注册的自定义 UIRoot(只读快照,调试用)</summary>
        public IReadOnlyDictionary<Type, UIRoot> GetAll() => _roots;

        public override void Destroy()
        {
            _roots.Clear();
        }
    }
}
