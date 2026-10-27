using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TGame.SceneNavigator
{
    /// <summary>
    /// 运行时场景导航：统一提供场景加载 / 卸载 / 预加载入口，并以事件形式对外广播加载进度。
    ///
    /// 说明：这是 Unity 本地场景加载的薄封装，不等价于联机同步。
    /// 联机场景切换（FishNet SceneManager）应由网络层驱动，再由网络层调用本类或直接 LoadSceneAsync，
    /// 但事件订阅与别名解析可以复用这里。
    /// </summary>
    public static class SceneNavigator
    {
        private static readonly Dictionary<string, string> _aliasToPath = new();

        private static SceneNavigatorProfile _profile;

        /// <summary>
        /// 场景加载中（参数 0~1）。
        /// </summary>
        public static event Action<float> LoadingProgress;

        /// <summary>
        /// 场景加载完成（参数 场景路径/名称）。
        /// </summary>
        public static event Action<string> LoadCompleted;

        /// <summary>
        /// 场景卸载完成（参数 场景路径/名称）。
        /// </summary>
        public static event Action<string> UnloadCompleted;

        /// <summary>
        /// 场景加载失败（参数 场景路径/名称、错误信息）。
        /// </summary>
        public static event Action<string, string> LoadFailed;

        /// <summary>
        /// 是否正在加载（用于挡输入 / 显示 LoadingPanel）。
        /// </summary>
        public static bool IsLoading { get; private set; }

        /// <summary>
        /// 当前场景标识（路径或名称）。
        /// </summary>
        public static string CurrentScene { get; private set; }

        /// <summary>
        /// 注册场景别名表（通常是 SceneNavigatorProfile）。
        /// </summary>
        public static void SetProfile(SceneNavigatorProfile profile)
        {
            _profile = profile;
            ReloadAliasMap();
        }

        /// <summary>
        /// 重新解析 Profile 里的别名。
        /// </summary>
        public static void ReloadAliasMap()
        {
            _aliasToPath.Clear();
            if (_profile == null || _profile.scenes == null)
                return;

            foreach (var entry in _profile.scenes)
            {
                if (entry == null || string.IsNullOrEmpty(entry.scenePath))
                    continue;
                var alias = string.IsNullOrEmpty(entry.alias) ? entry.scenePath : entry.alias;
                _aliasToPath[alias] = entry.scenePath;
            }
        }

        /// <summary>
        /// 把别名或路径统一解析为可用于 SceneManager.LoadSceneAsync 的字符串。
        /// </summary>
        public static string ResolveScene(string sceneKey)
        {
            if (string.IsNullOrEmpty(sceneKey))
                return null;
            if (_aliasToPath.TryGetValue(sceneKey, out var path))
                return path;
            return sceneKey;
        }

        /// <summary>
        /// 以 Single 模式加载场景（切换到目标场景，卸载其它场景）。
        /// </summary>
        /// <param name="sceneKey">别名或场景路径/名称</param>
        /// <param name="onCompleted">加载完成回调</param>
        public static void Load(string sceneKey, Action onCompleted = null)
        {
            LoadAsync(sceneKey, mode: LoadSceneMode.Single, onCompleted: onCompleted);
        }

        /// <summary>
        /// 以 Additive 模式叠加加载场景（UI 层、常驻模块场景）。
        /// </summary>
        public static void LoadAdditive(string sceneKey, Action onCompleted = null)
        {
            LoadAsync(sceneKey, mode: LoadSceneMode.Additive, onCompleted: onCompleted);
        }

        /// <summary>
        /// 卸载场景（仅对 Additive 加载的场景有效）。
        /// </summary>
        public static void Unload(string sceneKey, Action onCompleted = null)
        {
            var scene = ResolveScene(sceneKey);
            if (string.IsNullOrEmpty(scene))
                return;

            var op = SceneManager.UnloadSceneAsync(scene);
            if (op == null)
            {
                LoadFailed?.Invoke(scene, "UnloadSceneAsync returned null");
                return;
            }
            if (onCompleted != null)
                op.completed += _ => onCompleted();
            UnloadCompleted?.Invoke(scene);
        }

        /// <summary>
        /// 异步加载场景。已在进行中时会被拒绝（避免重复加载导致状态错乱）。
        /// </summary>
        public static void LoadAsync(string sceneKey, LoadSceneMode mode = LoadSceneMode.Single,
            Action onCompleted = null)
        {
            var scene = ResolveScene(sceneKey);
            if (string.IsNullOrEmpty(scene))
            {
                LoadFailed?.Invoke(sceneKey, "scene key is null or empty");
                return;
            }
            if (IsLoading)
            {
                LoadFailed?.Invoke(scene, "another scene load is in progress");
                return;
            }

            LoadCoreAsync(scene, mode, onCompleted);
        }

        private static async void LoadCoreAsync(string scene, LoadSceneMode mode, Action onCompleted)
        {
            IsLoading = true;
            LoadingProgress?.Invoke(0f);

            AsyncOperation op;
            try
            {
                op = SceneManager.LoadSceneAsync(scene, mode);
            }
            catch (Exception e)
            {
                IsLoading = false;
                LoadFailed?.Invoke(scene, e.Message);
                Debug.LogError($"[SceneNavigator] 加载场景失败: {scene}\n{e}");
                return;
            }

            if (op == null)
            {
                IsLoading = false;
                LoadFailed?.Invoke(scene, "LoadSceneAsync returned null, 请检查场景是否在 Build Settings 中");
                return;
            }

            while (!op.isDone)
            {
                // allowSceneActivation 前的进度上限是 0.9，这里映射到 0~1
                LoadingProgress?.Invoke(Mathf.Clamp01(op.progress / 0.9f));
                await Awaitable.NextFrameAsync();
            }

            IsLoading = false;
            CurrentScene = scene;
            LoadingProgress?.Invoke(1f);
            LoadCompleted?.Invoke(scene);
            onCompleted?.Invoke();
        }
    }
}
