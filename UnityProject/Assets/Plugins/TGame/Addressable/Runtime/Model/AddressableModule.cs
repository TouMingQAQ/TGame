using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TGame.TCore.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace TGame.Addressable
{
    public struct AssetHandle<T>  where T : Object
    {
        public string Key;
        public T Prefab;
        public void Release()
        {
            if(Prefab != null)
                Object.Destroy(Prefab);
        }
    }
    public sealed class AddressableModule<T> : BaseModule where T : Object
    {
        private Dictionary<string, AssetHandle<T>> handles = new Dictionary<string, AssetHandle<T>>();

        #region Preload

        public async void PreLoadAsync(IEnumerable<string> labels)
        {
            //Todo::通过扫描labels来预加载，记得加载目标类型必须为T
        }

        public async void PreLoadAsync(string addressableKey)
        {
            //Todo::通过扫描addressableKey来预加载，记得加载目标类型必须为T
        }
        
        /// <summary>
        /// 预加载资源
        /// </summary>
        /// <param name="reference"></param>
        public async UniTask<AssetHandle<T>> PreLoadAsync(AssetReference reference)
        {
            var key = reference.RuntimeKey.ToString();
            if(handles.TryGetValue(key, out var handle))
                return handle;
            if (typeof(MonoBehaviour).IsAssignableFrom(typeof(T)))
            {
                var loadHandle = reference.LoadAssetAsync<GameObject>();
                if(!loadHandle.IsValid())
                    return default;
                await loadHandle;
                var result =  await loadHandle;
                if (result.TryGetComponent<T>(out var prefab))
                {
                    handle = OnLoad(key, prefab);
                }
                else
                {
                    loadHandle.Release();
                    return default;
                }
                loadHandle.Release();
            }
            else
            {
                var loadHandle = reference.LoadAssetAsync<T>();
                if(!loadHandle.IsValid())
                    return default;
                await loadHandle;
                handle = OnLoad(key,loadHandle.Result);
                loadHandle.Release();
            }
            return handle;
           
        }
        
        #endregion

        #region Load

        AssetHandle<T> OnLoad(string key,T prefab)
        {
            var handle = new AssetHandle<T>
            {
                Prefab = prefab,
                Key = key
            };
            handles[key] = handle;
            return handle;
        }
        public async void LoadByKey(string key,Action<T> onLoaded)
        {
            if(handles.TryGetValue(key, out var handle))
                onLoaded?.Invoke(handle.Prefab);
            var loadHandle = Addressables.LoadAssetAsync<T>(key);
            await loadHandle;
            OnLoad(key,loadHandle.Result);
            loadHandle.Release();
            onLoaded?.Invoke(handle.Prefab);
        }

        public async UniTask<T> LoadByKeyAsync(string key)
        {
            if(handles.TryGetValue(key, out var handle))
                return handle.Prefab;
            var loadHandle = Addressables.LoadAssetAsync<T>(key);
            await loadHandle;
            OnLoad(key,loadHandle.Result);
            loadHandle.Release();
            return handle.Prefab;
        }
        #endregion
        #region Release

        public void ReleaseAll()
        {
            foreach (var handle in handles.Values)
            {
                handle.Release();
            }
            handles.Clear();
        }

        #endregion
   
    }
}
