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
        }

        private void OnDestroy()
        {
            ClearModule();
        }

 
    }
}
