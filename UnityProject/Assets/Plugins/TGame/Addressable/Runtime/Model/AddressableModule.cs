using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TGame.TCore.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace TGame.Addressable
{
    public struct AssetHandle<T> where T : Object
    {
        public string Key;
        public T Prefab;
        public AsyncOperationHandle OpHandle;
        public int RefCount;
    }

    public sealed class AddressableModule<T> : BaseModule where T : Object
    {
        private readonly Dictionary<string, AssetHandle<T>> _handles = new();

        #region Preload

        /// <summary>
        /// 预加载资源 (AssetReference)
        /// </summary>
        /// <param name="reference">Addressable 资产引用</param>
        public async UniTask<AssetHandle<T>> PreLoadAsync(AssetReference reference)
        {
            if (reference == null)
                return default;

            var key = reference.RuntimeKey.ToString();
            if (_handles.TryGetValue(key, out var existingHandle))
            {
                existingHandle.RefCount++;
                _handles[key] = existingHandle;
                return existingHandle;
            }

            if (typeof(MonoBehaviour).IsAssignableFrom(typeof(T)))
            {
                var loadHandle = reference.LoadAssetAsync<GameObject>();
                if (!loadHandle.IsValid())
                    return default;

                var result = await loadHandle;
                if (result != null && result.TryGetComponent<T>(out var prefab))
                {
                    return OnLoad(key, prefab, loadHandle);
                }

                if (loadHandle.IsValid())
                    Addressables.Release(loadHandle);
                return default;
            }
            else
            {
                var loadHandle = reference.LoadAssetAsync<T>();
                if (!loadHandle.IsValid())
                    return default;

                var result = await loadHandle;
                if (result != null)
                {
                    return OnLoad(key, result, loadHandle);
                }

                if (loadHandle.IsValid())
                    Addressables.Release(loadHandle);
                return default;
            }
        }

        #endregion

        #region Load

        private AssetHandle<T> OnLoad(string key, T prefab, AsyncOperationHandle opHandle)
        {
            if (_handles.TryGetValue(key, out var existing))
            {
                existing.RefCount++;
                _handles[key] = existing;
                return existing;
            }

            var handle = new AssetHandle<T>
            {
                Prefab = prefab,
                Key = key,
                OpHandle = opHandle,
                RefCount = 1
            };
            _handles[key] = handle;
            return handle;
        }

        public async void LoadByKey(string key, Action<T> onLoaded)
        {
            var prefab = await LoadByKeyAsync(key);
            onLoaded?.Invoke(prefab);
        }

        public async UniTask<T> LoadByKeyAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            if (_handles.TryGetValue(key, out var handle))
            {
                handle.RefCount++;
                _handles[key] = handle;
                return handle.Prefab;
            }

            if (typeof(MonoBehaviour).IsAssignableFrom(typeof(T)))
            {
                var loadHandle = Addressables.LoadAssetAsync<GameObject>(key);
                if (!loadHandle.IsValid())
                    return null;

                var go = await loadHandle;
                if (go != null && go.TryGetComponent<T>(out var comp))
                {
                    var newHandle = OnLoad(key, comp, loadHandle);
                    return newHandle.Prefab;
                }

                if (loadHandle.IsValid())
                    Addressables.Release(loadHandle);
                return null;
            }
            else
            {
                var loadHandle = Addressables.LoadAssetAsync<T>(key);
                if (!loadHandle.IsValid())
                    return null;

                var res = await loadHandle;
                if (res != null)
                {
                    var newHandle = OnLoad(key, res, loadHandle);
                    return newHandle.Prefab;
                }

                if (loadHandle.IsValid())
                    Addressables.Release(loadHandle);
                return null;
            }
        }

        #endregion

        #region Release

        public void Release(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (_handles.TryGetValue(key, out var handle))
            {
                handle.RefCount--;
                if (handle.RefCount <= 0)
                {
                    if (handle.OpHandle.IsValid())
                    {
                        Addressables.Release(handle.OpHandle);
                    }
                    _handles.Remove(key);
                }
                else
                {
                    _handles[key] = handle;
                }
            }
        }

        public void ReleaseAll()
        {
            foreach (var handle in _handles.Values)
            {
                if (handle.OpHandle.IsValid())
                {
                    Addressables.Release(handle.OpHandle);
                }
            }
            _handles.Clear();
        }

        public override void Destroy()
        {
            ReleaseAll();
        }

        #endregion
    }
}
