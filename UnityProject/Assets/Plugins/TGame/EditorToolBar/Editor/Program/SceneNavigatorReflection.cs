// SceneNavigatorReflection.cs
// 共享反射工具:跨过 asmdef 引用隔离,反射访问 SceneNavigatorProfile / SceneEntry。
// 被 QuickLaunchToolbarItem 和 SceneDropdownToolbarItem 共用。
//
// 约束:TGame.EditorToolBar.Editor.asmdef 的 references: [] 为空,
// 拿不到 SceneNavigator.Runtime 程序集中的类型,只能反射。

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TGame.EditorToolBar.BuiltIn
{
    internal static class SceneNavigatorReflection
    {
        private const string ProfileTypeName = "TGame.SceneNavigator.SceneNavigatorProfile";
        private const string EntryTypeName = "TGame.SceneNavigator.SceneEntry";
        private const string ResourcePath = "SceneNavigatorProfile";
        // 与 SceneNavigator/Editor/SceneNavigatorBox.cs:524 硬编码路径保持一致
        public const string ProfileAssetPath =
            "Assets/Plugins/TGame/SceneNavigator/Resources/SceneNavigatorProfile.asset";

        private static Type _profileType;
        private static Type _entryType;
        private static FieldInfo _scenesField;
        private static FieldInfo _pathField;
        private static FieldInfo _aliasField;
        private static bool _initialized;

        public static Type ProfileType => _profileType;
        public static Type EntryType => _entryType;

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true; // 失败也只跑一次
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (_profileType == null)
                    _profileType = asm.GetType(ProfileTypeName, false);
                if (_entryType == null)
                    _entryType = asm.GetType(EntryTypeName, false);
            }
            if (_profileType != null)
                _scenesField = _profileType.GetField("scenes", BindingFlags.Public | BindingFlags.Instance);
            if (_entryType != null)
            {
                _pathField = _entryType.GetField("scenePath", BindingFlags.Public | BindingFlags.Instance);
                _aliasField = _entryType.GetField("alias", BindingFlags.Public | BindingFlags.Instance);
            }
        }

        public static object LoadProfile()
        {
            EnsureInitialized();
            if (_profileType == null) return null;
            // Resources.Load<T>(string) — 通过反射拿泛型方法
            var genericLoad = typeof(Resources).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Load"
                                     && m.IsGenericMethodDefinition
                                     && m.GetParameters().Length == 1
                                     && m.GetParameters()[0].ParameterType == typeof(string));
            if (genericLoad == null) return null;
            return genericLoad.MakeGenericMethod(_profileType).Invoke(null, new object[] { ResourcePath });
        }

        public static SceneRow[] ReadScenes(object profile)
        {
            EnsureInitialized();
            if (profile == null || _scenesField == null) return Array.Empty<SceneRow>();
            var list = _scenesField.GetValue(profile) as IList;
            if (list == null) return Array.Empty<SceneRow>();
            var result = new SceneRow[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry == null) { result[i] = new SceneRow { ScenePath = "", Alias = "" }; continue; }
                var path = _pathField?.GetValue(entry) as string ?? "";
                var alias = _aliasField?.GetValue(entry) as string ?? "";
                result[i] = new SceneRow { ScenePath = path, Alias = alias };
            }
            return result;
        }

        public struct SceneRow
        {
            public string ScenePath;
            public string Alias;
            public string DisplayName =>
                !string.IsNullOrEmpty(Alias) ? Alias : System.IO.Path.GetFileNameWithoutExtension(ScenePath);
        }
    }
}
