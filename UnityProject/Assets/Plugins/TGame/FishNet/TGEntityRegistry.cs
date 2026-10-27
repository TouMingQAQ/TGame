using System.Collections.Generic;
using TGame.TCore.Runtime;
using UnityEngine;

namespace GameNet.FishNet
{
    /// <summary>
    /// TGame 侧的网络实体注册表。
    /// 继承 BaseManager，挂到 TGame 的 Game 预制体下，随 Game 一起 DontDestroyOnLoad。
    ///
    /// 职责：
    ///   1. 维护 EntityId -> TGameNetworkBridge 的映射，供任意系统按 Id 查实体；
    ///   2. 持有 EventModule，供 NetEventHub 用于作用域事件分发；
    ///   3. 连接断开 / 对象销毁时统一注销，避免悬挂引用。
    /// </summary>
    public class TGEntityRegistry : BaseManager
    {
        private readonly Dictionary<int, TGameNetworkBridge> _entities = new();
        private readonly List<int> _cleanupCache = new();

        private EventModule _eventModule;

        /// <summary>
        /// 当前实体数量（调试用）。
        /// </summary>
        public int Count => _entities.Count;

        private void Awake()
        {
            _eventModule = GetModule<EventModule>();
            NetEventHub.Initialize(_eventModule);
        }

        private void Start()
        {
            // BaseManager 的 game 字段在 Start 里注入，和 GameSystemManager 保持一致
            game = Game.Instance;
            if (game == null)
            {
                Debug.LogError("[TGEntityRegistry] Game.Instance is null, ensure Game is in scene");
                return;
            }
            game.AddManager(this);

            _eventModule = GetModule<EventModule>();
            NetEventHub.Initialize(_eventModule);
            Debug.Log("<color=#66ccff>[TGEntityRegistry]</color> ready");
        }

        /// <summary>
        /// 注册实体。
        /// </summary>
        public void Register(TGameNetworkBridge entity)
        {
            if (entity == null || entity.EntityId < 0)
                return;

            if (_entities.TryGetValue(entity.EntityId, out var exist) && exist != null && exist != entity)
            {
                Debug.LogWarning($"[TGEntityRegistry] EntityId {entity.EntityId} 已存在，覆盖注册");
            }
            _entities[entity.EntityId] = entity;
        }

        /// <summary>
        /// 注销实体。
        /// </summary>
        public void Unregister(TGameNetworkBridge entity)
        {
            if (entity == null || entity.EntityId < 0)
                return;
            if (_entities.TryGetValue(entity.EntityId, out var exist) && exist == entity)
                _entities.Remove(entity.EntityId);
        }

        /// <summary>
        /// 按 Id 取实体。
        /// </summary>
        public TGameNetworkBridge Get(int entityId)
        {
            return _entities.TryGetValue(entityId, out var entity) ? entity : null;
        }

        /// <summary>
        /// 按 Id 取指定类型的实体组件。
        /// </summary>
        public T Get<T>(int entityId) where T : class
        {
            var entity = Get(entityId);
            return entity == null ? null : entity.GetComponent<T>();
        }

        /// <summary>
        /// 遍历所有实体（清理时使用，避免直接暴露字典）。
        /// </summary>
        public void GetAll(List<TGameNetworkBridge> results)
        {
            if (results == null)
                return;
            results.Clear();
            foreach (var pair in _entities)
                results.Add(pair.Value);
        }

        /// <summary>
        /// 清理已被销毁的实体引用。
        /// </summary>
        public void CleanupDestroyed()
        {
            _cleanupCache.Clear();
            foreach (var pair in _entities)
            {
                if (pair.Value == null)
                    _cleanupCache.Add(pair.Key);
            }
            for (int i = 0; i < _cleanupCache.Count; i++)
                _entities.Remove(_cleanupCache[i]);
            _cleanupCache.Clear();
        }

        private void OnDestroy()
        {
            _entities.Clear();
            NetEventHub.Reset();
            ClearModule();
        }
    }
}
