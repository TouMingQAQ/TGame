using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TGame.TCore.Runtime
{
    public abstract class ObjectPoolModule<T> : BaseModule
    {
        private Queue<T> _poolQueue = new();

        /// <summary>
        /// 获取
        /// </summary>
        public T Get()
        {
            if (!_poolQueue.TryDequeue(out var obj))
                obj = Create();
            OnGet(obj);
            return obj;
        }
        /// <summary>
        /// 回收
        /// </summary>
        /// <param name="obj"></param>
        public void Release(T obj)
        {
            OnRelease(obj);
            _poolQueue.Enqueue(obj);
        }
        protected virtual void OnGet(T obj) { } 
        protected virtual void OnRelease(T obj){ }

        protected abstract T Create();

    }

    public class GameObjectPoolModule<T> : ObjectPoolModule<T> where T : MonoBehaviour
    {
        protected T _prefab;
        protected Transform _showRoot;
        protected Transform _hideRoot;
        public GameObjectPoolModule(T prefab, Transform showRoot, Transform hideRoot)
        {
            _prefab = prefab;
            _showRoot = showRoot;
            _hideRoot = hideRoot;
            _hideRoot.gameObject.SetActive(false);
        }
        
        protected override T Create()
        {
            var obj = Object.Instantiate(_prefab, _showRoot);
            return obj;
        }

        protected override void OnRelease(T obj)
        {
            obj.transform.SetParent(_hideRoot);
        }

        protected override void OnGet(T obj)
        {
            obj.transform.SetParent(_showRoot);
        }
    }
}