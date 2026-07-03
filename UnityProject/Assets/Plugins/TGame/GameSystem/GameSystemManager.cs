using System;
using System.Collections.Generic;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.GameSystem
{
    public abstract class GameSystem : MonoBehaviour
    {
        protected GameSystemManager Manager { get; set; }
        public virtual void OnInit(GameSystemManager manager)
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
            var systems = GetComponentsInChildren<GameSystem>();
            foreach (var system in systems)
            {
                var type = system.GetType();
                if (_gameSystems.TryGetValue(type, out GameSystem gameSystem))
                {
                    Debug.LogError($"[<color=66ccff>GameSystem</color>] Have same gameSystem ===>{type}");
                    continue;
                }
                system.OnInit(this);
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

        public void AddGameSystem<T>(T gameSystem) where T : GameSystem
        {
            var type = typeof(T);
            if (_gameSystems.TryGetValue(type, out var system))
            {
                Debug.LogError($"[<color=66ccff>GameSystem</color>] Have same gameSystem ===>{type}");
                return;
            }
            gameSystem.OnInit(this);
            _gameSystems[type] = gameSystem;
        }

        public void RemoveGameSystem<T>() where T : GameSystem
        {
            var type = typeof(T);
            _gameSystems.Remove(type);
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