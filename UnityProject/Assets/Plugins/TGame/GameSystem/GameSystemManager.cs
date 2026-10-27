using System;
using System.Collections.Generic;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.GameSystem
{
    public abstract class GameSystem : MonoBehaviour
    {
        protected GameSystemManager Manager { get; set; }

        /// <summary>
        /// 是否已经执行过 OnInit。
        /// 用于避免 GameSystemManager 在 Awake 自动收集时重复调用 OnInit，
        /// 覆盖 GameSystem 自己在 Awake 中已完成的初始化。
        /// </summary>
        public bool IsInitialized { get; private set; }

        public virtual void OnInit(GameSystemManager manager)
        {
            this.Manager = manager;
            IsInitialized = true;
        }

        /// <summary>
        /// 由 GameSystemManager 调用，仅用于补挂 / 解除 Manager 引用，不会重复触发 OnInit。
        /// </summary>
        internal void SetManager(GameSystemManager manager)
        {
            this.Manager = manager;
        }
    }
    public sealed class GameSystemManager : BaseManager
    {
        private Dictionary<Type,GameSystem> _gameSystems = new Dictionary<Type, GameSystem>();

        private void Awake()
        {
            //自动收集自己子物体中的组件
            _gameSystems.Clear();
            var systems = GetComponentsInChildren<GameSystem>(true);
            foreach (var system in systems)
            {
                var type = system.GetType();
                if (_gameSystems.TryGetValue(type, out GameSystem gameSystem))
                {
                    Debug.LogError($"[<color=66ccff>GameSystem</color>] Have same gameSystem ===>{type}");
                    continue;
                }

                // 只登记未初始化过的实例：子类若在自身 Awake 里已经完成初始化，
                // 这里不再二次 OnInit，避免覆盖其状态。
                if (!system.IsInitialized)
                    system.OnInit(this);
                else
                    system.SetManager(this);

                _gameSystems.Add(type, system);
            }
        }

        private void Start()
        {
            game = Game.Instance;
            if (game == null)
            {
                Debug.LogError("[GameSystemManager] Game.Instance is null, ensure Game is in scene");
                return;
            }
            game.AddManager(this);
        }

        /// <summary>
        /// 注册 GameSystem。已 OnInit 过的实例只补挂 Manager，不会重复初始化。
        /// </summary>
        public void AddGameSystem<T>(T gameSystem) where T : GameSystem
        {
            if (gameSystem == null)
            {
                Debug.LogError($"[<color=66ccff>GameSystem</color>] AddGameSystem with null instance ===>{typeof(T)}");
                return;
            }
            var type = typeof(T);
            if (_gameSystems.TryGetValue(type, out var system))
            {
                Debug.LogError($"[<color=66ccff>GameSystem</color>] Have same gameSystem ===>{type}");
                return;
            }
            if (gameSystem.IsInitialized)
                gameSystem.SetManager(this);
            else
                gameSystem.OnInit(this);
            _gameSystems[type] = gameSystem;
        }

        /// <summary>
        /// 移除 GameSystem，并解除其与当前 Manager 的绑定。
        /// </summary>
        public void RemoveGameSystem<T>() where T : GameSystem
        {
            var type = typeof(T);
            if (_gameSystems.Remove(type, out var system) && system != null)
                system.SetManager(null);
        }

        public T GetGameSystem<T>() where T : GameSystem
        {
            var type = typeof(T);
            if(_gameSystems.TryGetValue(type, out var system) && system is T targetSystem)
                return targetSystem;
            Debug.LogError($"[<color=66ccff>GameSystem</color>] Have no gameSystem ===>{type}");
            return null;
        }
    }
}