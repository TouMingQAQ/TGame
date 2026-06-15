using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TGame.TCore.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Debug = UnityEngine.Debug;

namespace TGame.Addressable
{
    /// <summary>
    /// Addressables 资源加载/释放 Model。
    /// 挂在 AddressableManager 下,封装句柄池(引用计数),对外提供按 address 单资源加载和按 label/keys 批量预热。
    ///
    /// 职责:
    ///   - LoadAsync&lt;T&gt;:命中句柄池 RefCount++ 并立刻返回,未命中则 Addressables.LoadAssetAsync 写入池
    ///   - Release&lt;T&gt;:RefCount 归零时 Addressables.Release(handle) 并从池移除
    ///   - PreloadByLabelAsync&lt;T&gt; / PreloadByKeysAsync&lt;T&gt;:解析 IResourceLocation 后并行 LoadInternalAsync
    ///   - 广播 AddressableLoadCompletedEvent / AddressablePreloadProgressEvent / AddressablePreloadCompletedEvent
    ///
    /// 状态:
    ///   - _handles:Dictionary&lt;AddressableKey, HandleEntry&gt; — 句柄池
    ///   - _loadingMap:Dictionary&lt;AddressableKey, CancellationTokenSource&gt; — 进行中加载,Destroy 时 Cancel 全体
    ///   - _globalCts:基类 Destroy 时触发,联动外部 ct 让所有 in-flight 任务被取消
    ///
    /// 依赖:
    ///   - AddressableManager:SetManager(AddressableManager) 注入,事件广播走 _mgr.Call
    ///   - 其他 BaseManager(如 UIManager):SetManager(BaseManager) 注入广播目标
    ///   - UnityEngine.AddressableAssets.Addressables:加载/释放入口
    ///   - Cysharp.Threading.Tasks:ToUniTask(cancellationToken)
    ///
    /// 关键不变量:
    ///   - Handle 存为 non-generic AsyncOperationHandle,取结果时 entry.Handle.Result as T
    ///   - RefCount &gt; 0 时 Handle.IsValid() 应为 true;RefCount 归零立即 Release
    ///   - 预热逐资源独立 Handle(非 batch),与单资源加载共用句柄池,Release 时路径一致
    ///   - 取消时通过 linkedCts 联动,await 抛 OperationCanceledException,finally 清理 _loadingMap
    ///   - 并发同一 key 的 LoadAsync 通过 _loadingMap.TryGetValue 查重:后到者在 CTS 上等候并重试句柄池
    /// </summary>
    public sealed class AddressableModel : BaseModule
    {
        // 句柄池:key=(Type,string) → handle(存储为 non-generic 的 AsyncOperationHandle)
        // 不存 Handle.Result 强引用,避免阻止 GC 释放资源
        private readonly Dictionary<AddressableKey, HandleEntry> _handles = new();

        // 进行中加载:同一 key 的并发请求共用一个 CancellationTokenSource
        private readonly Dictionary<AddressableKey, CancellationTokenSource> _loadingMap = new();

        // 全局取消源:Destroy 时 Cancel,联动所有 in-flight 任务
        private CancellationTokenSource _globalCts;

        private AddressableManager _mgr;
        private BaseManager _broadcastTarget;

        /// <summary>由 AddressableManager.Start() 注入自身引用,之后才能广播事件</summary>
        public void SetManager(AddressableManager mgr) => _mgr = mgr;

        /// <summary>
        /// 注入事件广播目标 BaseManager。
        /// 当 AddressableModel 被挂载到非 AddressableManager 的 BaseManager 下时(例如 UIManager),
        /// 通过此方法指定事件广播出口。UIManager 等其他 Manager 不需要 _mgr 引用。
        /// </summary>
        public void SetManager(BaseManager broadcastTarget) => _broadcastTarget = broadcastTarget;

        public override bool Enable { get; set; } = true;

        /// <summary>
        /// 事件广播出口:优先用 _mgr(AddressableManager),否则用 _broadcastTarget(挂载到其他 BaseManager 时)。
        /// </summary>
        private BaseManager BroadcastTarget => _mgr ?? (BaseManager)_broadcastTarget;

        /// <summary>当前句柄池条目数(调试用)</summary>
        public int HandleCount => _handles.Count;

        /// <summary>当前进行中加载数(调试用)</summary>
        public int LoadingCount => _loadingMap.Count;

        // ===== 单资源加载 =====

        public async UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[AddressableModel] LoadAsync key is null or empty");
                return default;
            }
            var addrKey = new AddressableKey(typeof(T), key);
            return await LoadInternalAsync<T>(addrKey, key, ct);
        }

        public async UniTask<T> LoadAsync<T>(object key, CancellationToken ct = default) where T : UnityEngine.Object
        {
            if (key == null)
            {
                Debug.LogError("[AddressableModel] LoadAsync key is null");
                return default;
            }
            var addrKey = new AddressableKey(typeof(T), key.ToString());
            return await LoadInternalAsync<T>(addrKey, key, ct);
        }

        // ===== 释放 =====

        public void Release<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key)) return;
            ReleaseInternal(new AddressableKey(typeof(T), key));
        }

        public void Release(AddressableKey key) => ReleaseInternal(key);

        public void ReleaseAll()
        {
            foreach (var entry in _handles.Values)
            {
                if (entry.Handle.IsValid())
                {
                    try { Addressables.Release(entry.Handle); }
                    catch (Exception e) { Debug.LogWarning($"[AddressableModel] Release handle failed: {e.Message}"); }
                }
            }
            _handles.Clear();
        }

        // ===== 批量预热 =====

        public async UniTask PreloadByLabelAsync<T>(string label,
            IProgress<float> progress = null,
            CancellationToken ct = default) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(label))
            {
                Debug.LogError("[AddressableModel] PreloadByLabelAsync label is null or empty");
                return;
            }
            await PreloadInternalAsync<T>(new object[] { label }, label, Addressables.MergeMode.Union, progress, ct);
        }

        public async UniTask PreloadByKeysAsync<T>(IEnumerable<string> keys,
            IProgress<float> progress = null,
            CancellationToken ct = default) where T : UnityEngine.Object
        {
            if (keys == null)
            {
                Debug.LogError("[AddressableModel] PreloadByKeysAsync keys is null");
                return;
            }
            var keyArr = new List<object>();
            foreach (var k in keys)
            {
                if (!string.IsNullOrEmpty(k)) keyArr.Add(k);
            }
            if (keyArr.Count == 0)
            {
                Debug.LogWarning("[AddressableModel] PreloadByKeysAsync keys is empty after filter");
                return;
            }
            await PreloadInternalAsync<T>(keyArr, $"keys[{keyArr.Count}]", Addressables.MergeMode.Union, progress, ct);
        }

        // ===== 查询 =====

        public bool IsLoaded<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key)) return false;
            var addrKey = new AddressableKey(typeof(T), key);
            return _handles.TryGetValue(addrKey, out var entry) && entry.Handle.IsValid() && entry.RefCount > 0;
        }

        public int GetRefCount<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key)) return 0;
            var addrKey = new AddressableKey(typeof(T), key);
            return _handles.TryGetValue(addrKey, out var entry) ? entry.RefCount : 0;
        }

        // ===== 取消 =====

        public void CancelAllLoading()
        {
            if (_globalCts != null)
            {
                try { _globalCts.Cancel(); } catch { /* already disposed */ }
                _globalCts.Dispose();
                _globalCts = null;
            }
            // 仅 Cancel(不 Dispose):每个 in-flight LoadInternalAsync 的 using 声明自己 Dispose 各自的 linkedCts
            foreach (var cts in _loadingMap.Values)
            {
                try { cts.Cancel(); } catch { /* ignore */ }
            }
            _loadingMap.Clear();
        }

        // ===== 生命周期 =====

        public override void Destroy()
        {
            CancelAllLoading();
            ReleaseAll();
        }

        // ===== 内部 =====

        private struct HandleEntry
        {
            public AsyncOperationHandle Handle;
            public int RefCount;
        }

        /// <summary>核心加载流程。
        /// 并发防重入:若 _loadingMap 已有该 key 的进行中加载,后到者复用已有 CTS 等待同一任务。</summary>
        private async UniTask<T> LoadInternalAsync<T>(AddressableKey addrKey, object rawKey,
                                                       CancellationToken ct) where T : UnityEngine.Object
        {
            // 1. 命中句柄池 → RefCount++,直接返回
            if (_handles.TryGetValue(addrKey, out var entry))
            {
                if (entry.Handle.IsValid())
                {
                    entry.RefCount++;
                    _handles[addrKey] = entry;
                    BroadcastTarget?.Call(new AddressableLoadCompletedEvent(addrKey.Key, addrKey.AssetType, true, 0));
                    return entry.Handle.Result as T;
                }
                _handles.Remove(addrKey);
            }

            // 2. 并发防重入:若已有该 key 的进行中加载,等待其 CTS 取消或完成后重试句柄池
            if (_loadingMap.TryGetValue(addrKey, out var existingCts))
            {
                try { await existingCts.Token.WaitUntilCanceled(); } catch { /* cancelled */ }
                // 重试句柄池查重
                if (_handles.TryGetValue(addrKey, out var joinEntry) && joinEntry.Handle.IsValid())
                {
                    joinEntry.RefCount++;
                    _handles[addrKey] = joinEntry;
                    BroadcastTarget?.Call(new AddressableLoadCompletedEvent(addrKey.Key, addrKey.AssetType, true, 0));
                    return joinEntry.Handle.Result as T;
                }
                // 原加载取消/失败后句柄未写入,回退到新加载
            }

            // 3. Merged CancellationToken:外部 ct + _globalCts
            _globalCts ??= new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, ct);
            var mergedToken = linkedCts.Token;
            _loadingMap[addrKey] = linkedCts;

            var startTime = Time.realtimeSinceStartupAsDouble;
            T result = default;
            AsyncOperationHandle<T> handle = default;
            try
            {
                handle = Addressables.LoadAssetAsync<T>(rawKey);
                mergedToken.ThrowIfCancellationRequested();
                await handle.ToUniTask(cancellationToken: mergedToken);

                result = handle.Result;
                if (result != null)
                {
                    _handles[addrKey] = new HandleEntry { Handle = handle, RefCount = 1 };
                }
                else
                {
                    Addressables.Release(handle);
                }
            }
            catch (OperationCanceledException)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                var elapsed = (float)((Time.realtimeSinceStartupAsDouble - startTime) * 1000);
                BroadcastTarget?.Call(new AddressableLoadCompletedEvent(addrKey.Key, addrKey.AssetType, false, elapsed));
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressableModel] LoadAsync<{typeof(T).Name}>({addrKey.Key}) failed: {ex.Message}");
                if (handle.IsValid())
                    Addressables.Release(handle);
                var elapsed = (float)((Time.realtimeSinceStartupAsDouble - startTime) * 1000);
                BroadcastTarget?.Call(new AddressableLoadCompletedEvent(addrKey.Key, addrKey.AssetType, false, elapsed));
                return default;
            }
            finally
            {
                _loadingMap.Remove(addrKey);
            }

            var totalMs = (float)((Time.realtimeSinceStartupAsDouble - startTime) * 1000);
            BroadcastTarget?.Call(new AddressableLoadCompletedEvent(addrKey.Key, addrKey.AssetType, result != null, totalMs));
            return result;
        }

        private void ReleaseInternal(AddressableKey key)
        {
            if (!_handles.TryGetValue(key, out var entry)) return;
            entry.RefCount--;
            if (entry.RefCount > 0)
            {
                _handles[key] = entry;
                return;
            }
            if (entry.Handle.IsValid())
            {
                try { Addressables.Release(entry.Handle); }
                catch (Exception e) { Debug.LogWarning($"[AddressableModel] Release handle failed: {e.Message}"); }
            }
            _handles.Remove(key);
            BroadcastTarget?.Call(new AddressableReleasedEvent(key.Key, key.AssetType));
        }

        /// <summary>批量预热核心:解析 locations → 并行 LoadInternalAsync → 进度广播。
        /// 单轮迭代过滤已加载 key + building tasks,再用 UniTask.WhenAll 并行执行。</summary>
        private async UniTask PreloadInternalAsync<T>(IList<object> keys, string context,
                                                       Addressables.MergeMode mergeMode,
                                                       IProgress<float> progress,
                                                       CancellationToken ct) where T : UnityEngine.Object
        {
            var startTime = Time.realtimeSinceStartupAsDouble;

            // Step 1: 解析 locations
            var locHandle = Addressables.LoadResourceLocationsAsync(keys, mergeMode, typeof(T));
            await locHandle.ToUniTask(cancellationToken: ct);

            if (locHandle.Result == null || locHandle.Result.Count == 0)
            {
                Addressables.Release(locHandle);
                BroadcastTarget?.Call(new AddressablePreloadCompletedEvent(context, 0, 0));
                return;
            }

            var locations = locHandle.Result;

            // Step 2: 单轮遍历过滤 + 启动任务
            var tasks = new List<UniTask>(locations.Count);
            foreach (var loc in locations)
            {
                if (loc == null) continue;
                var innerKey = new AddressableKey(typeof(T), loc.PrimaryKey);
                if (_handles.TryGetValue(innerKey, out var h) && h.Handle.IsValid() && h.RefCount > 0)
                    continue;
                tasks.Add(LoadInternalAsync<T>(innerKey, loc, ct).AsUniTask());
            }

            int todoCount = tasks.Count;
            if (todoCount == 0)
            {
                Addressables.Release(locHandle);
                BroadcastTarget?.Call(new AddressablePreloadCompletedEvent(context, 0, 0));
                return;
            }

            // Step 3: 并行加载。UniTask.WhenAll 只接受 UniTask(无值),内部结果已写入 _handles
            try
            {
                await UniTask.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                var elapsed = (float)((Time.realtimeSinceStartupAsDouble - startTime) * 1000);
                BroadcastTarget?.Call(new AddressablePreloadCompletedEvent(context, todoCount, elapsed));
                Addressables.Release(locHandle);
                throw;
            }

            Addressables.Release(locHandle);
            var totalMs = (float)((Time.realtimeSinceStartupAsDouble - startTime) * 1000);
            BroadcastTarget?.Call(new AddressablePreloadCompletedEvent(context, todoCount, totalMs));
        }
    }
}
