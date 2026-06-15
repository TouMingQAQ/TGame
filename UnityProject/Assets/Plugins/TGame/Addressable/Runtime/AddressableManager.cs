using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TGame.TCore.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TGame.Addressable
{
    /// <summary>
    /// Addressables 资源管理入口。
    /// 薄壳:所有方法转发到 AddressableModel,本类仅负责 Awake→Addressables.InitializeAsync + Start→AddManager + 注入。
    ///
    /// 状态:无(所有状态由 AddressableModel 维护)
    /// 依赖:
    ///   - AddressableModel:句柄池 + 引用计数 + 预热 + 取消
    ///   - AddressableManager.Call:广播 AddressableLoadCompletedEvent / AddressablePreloadCompletedEvent
    ///
    /// 调用链:
    ///   业务方 LoadAsync&lt;T&gt;(key) → 本类 LoadAsync(转发) → AddressableModel.LoadAsync
    ///   业务方 PreloadByLabelAsync&lt;T&gt;(label) → 本类 PreloadByLabelAsync(转发) → AddressableModel.PreloadByLabelAsync
    ///   业务方 Release&lt;T&gt;(key) → 本类 Release(转发) → AddressableModel.Release
    ///
    /// 关键不变量:
    ///   - 任何其他 Manager 都可通过 Game.Instance.GetManager&lt;AddressableManager&gt;() 访问
    ///   - 静态 Instance 仅在 Game.Instance 非空时返回,业务方使用前应做 null check
    ///   - OnDestroy 时 ClearModule → AddressableModel.Destroy → ReleaseAll + CancelAllLoading
    /// </summary>
    [DefaultExecutionOrder(-7980)]
    public sealed class AddressableManager : BaseManager
    {
        private void Awake()
        {
            // 触发 Addressables 初始化(异步、自动释放 handle,无需 await)
            // 后续 LoadAsync 会自动等待 InitializeAsync 完成
            var initHandle = Addressables.InitializeAsync(false);
            initHandle.ReleaseHandleOnCompletion();
        }

        private void Start()
        {
            game = Game.Instance;
            if (game == null)
            {
                Debug.LogError("[AddressableManager] Game.Instance is null, ensure Game is in scene");
                return;
            }
            game.AddManager(this);

            // 注入自身引用,供 Module 内部访问 + 事件广播
            GetModule<AddressableModel>().SetManager(this);
        }

        private void OnDestroy()
        {
            ClearModule();
        }

        // ===== 转发 API(与 Model 同名同参) =====

        /// <summary>按 Addressables address 加载资源</summary>
        public UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default) where T : UnityEngine.Object
            => GetModule<AddressableModel>().LoadAsync<T>(key, ct);

        /// <summary>按任意 object key 加载(IResourceLocation / object-label 等)</summary>
        public UniTask<T> LoadAsync<T>(object key, CancellationToken ct = default) where T : UnityEngine.Object
            => GetModule<AddressableModel>().LoadAsync<T>(key, ct);

        /// <summary>释放引用计数。RefCount 归零时 Addressables.Release</summary>
        public void Release<T>(string key) where T : UnityEngine.Object
            => GetModule<AddressableModel>().Release<T>(key);

        /// <summary>按 Addressables label 批量预热</summary>
        public UniTask PreloadByLabelAsync<T>(string label,
            IProgress<float> progress = null, CancellationToken ct = default) where T : UnityEngine.Object
            => GetModule<AddressableModel>().PreloadByLabelAsync<T>(label, progress, ct);

        /// <summary>按一组 Addressables address 批量预热</summary>
        public UniTask PreloadByKeysAsync<T>(IEnumerable<string> keys,
            IProgress<float> progress = null, CancellationToken ct = default) where T : UnityEngine.Object
            => GetModule<AddressableModel>().PreloadByKeysAsync<T>(keys, progress, ct);

        /// <summary>资源是否已加载</summary>
        public bool IsLoaded<T>(string key) where T : UnityEngine.Object
            => GetModule<AddressableModel>().IsLoaded<T>(key);

        /// <summary>句柄池中指定 key 的引用计数</summary>
        public int GetRefCount<T>(string key) where T : UnityEngine.Object
            => GetModule<AddressableModel>().GetRefCount<T>(key);

        /// <summary>当前句柄池条目数(调试用)</summary>
        public int HandleCount => GetModule<AddressableModel>().HandleCount;

        /// <summary>当前进行中加载数(调试用)</summary>
        public int LoadingCount => GetModule<AddressableModel>().LoadingCount;

        /// <summary>取消所有进行中加载(场景切换时使用)</summary>
        public void CancelAllLoading()
            => GetModule<AddressableModel>().CancelAllLoading();

        // ---- 快捷静态入口 ----

        public static AddressableManager Instance =>
            Game.Instance != null ? Game.Instance.GetManager<AddressableManager>() : null;
    }
}
