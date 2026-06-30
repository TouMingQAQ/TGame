using System;
using System.Collections.Generic;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// UIRoot 管理 Module。挂在 UIManager 下,管理所有启用中的 UIRoot 实例(按运行时 Type 键索引)。
    ///
    /// 使用方式:
    ///   1. 自定义 UIRoot 子类 OnEnable 时自动按运行时类型注册
    ///   2. OnDisable 时自动注销自身
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
            if (root == null) return;
            Register(root.GetType(), root);
        }

        /// <summary>
        /// 以任意 Type 为键注册 UIRoot。
        /// </summary>
        public void Register(Type key, UIRoot root)
        {
            if (key == null || root == null) return;
            if (_roots.TryGetValue(key, out var existing))
            {
                if (ReferenceEquals(existing, root)) return;
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

        /// <summary>仅当 key 当前指向指定 root 时注销,避免禁用旧实例误删新实例。</summary>
        public bool Unregister(Type key, UIRoot root)
        {
            if (key == null || root == null) return false;
            if (!_roots.TryGetValue(key, out var existing) || !ReferenceEquals(existing, root))
                return false;
            return _roots.Remove(key);
        }

        /// <summary>所有已注册的自定义 UIRoot(只读快照,调试用)</summary>
        public IReadOnlyDictionary<Type, UIRoot> GetAll() => _roots;

        public override void Destroy()
        {
            _roots.Clear();
        }
    }
}
