// CustomToolbarRegistry.cs
// 扫描 AppDomain 里所有 ICustomToolbarItem 实现。
// 注意:Editor 启动时第一次扫描要等所有用户程序集加载完,这里用 EditorApplication.delayCall 保护。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TGame.EditorToolBar
{
    public static class CustomToolbarRegistry
    {
        private static readonly List<ICustomToolbarItem> _items = new();

        public static IReadOnlyList<ICustomToolbarItem> Items => _items;

        /// <summary>
        /// 域重载后由 Host 触发。延迟一帧,确保所有用户程序集已加载。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Bootstrap()
        {
            EditorApplication.delayCall += Scan;
        }

        public static void Scan()
        {
            _items.Clear();
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                    catch { continue; }

                    foreach (var type in types)
                    {
                        if (type == null) continue;
                        if (type.IsAbstract || type.IsInterface) continue;
                        if (!typeof(ICustomToolbarItem).IsAssignableFrom(type)) continue;

                        // 必须有无参构造函数(对静态类排除)
                        if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                        try
                        {
                            var instance = (ICustomToolbarItem)Activator.CreateInstance(type);
                            _items.Add(instance);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[EditorToolBar] Failed to instantiate {type.FullName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EditorToolBar] Registry.Scan failed: {ex}");
            }
        }

        public static ICustomToolbarItem FindById(string id)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Id == id) return _items[i];
            return null;
        }
    }
}
