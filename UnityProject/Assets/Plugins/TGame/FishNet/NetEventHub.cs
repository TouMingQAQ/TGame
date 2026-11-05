#if FISHNET
using System;
using System.Collections.Generic;
using FishNet.Connection;
using TGame.TCore.Runtime;

namespace GameNet.FishNet
{
    /// <summary>
    /// 网络作用域事件总线：把 TGame 的 EventModule 作用域分发与 FishNet 的生命周期/连接绑定起来。
    ///
    /// 解决的问题：
    ///   Host 模式下房主与客户端逻辑在同一进程，TGame 的全局事件会广播给所有玩家逻辑。
    ///   本类为每个玩家（NetworkConnection）维护独立作用域，事件只投递给对应玩家的订阅者。
    ///
    /// 作用域 key 约定（<see cref="GetScopeKey"/>）：
    ///   服务端视角：连接对象本身（NetworkConnection）
    ///   客户端视角：本地连接，所有远端玩家的“本地表现”共享同一作用域
    ///   无连接（单机）：固定字符串 Local
    /// </summary>
    public static class NetEventHub
    {
        /// <summary>
        /// 单机 / 无网络时的作用域 key。
        /// </summary>
        public const string LocalScope = "__local__";

        private static EventModule _eventModule;

        /// <summary>
        /// 连接 -> 观察该连接的所有 NetworkBehaviour（用于连接断开时批量清理）。
        /// </summary>
        private static readonly Dictionary<NetworkConnection, HashSet<UnityEngine.Object>> _scopeOwners = new();

        /// <summary>
        /// 事件总线。由 <see cref="TGEntityRegistry"/> 在初始化时注入（EventModule 是 Manager 下的模块）。
        /// </summary>
        public static EventModule Bus => _eventModule;

        /// <summary>
        /// 由 <see cref="TGEntityRegistry"/> 在初始化时注入 EventModule。
        /// </summary>
        public static void Initialize(EventModule eventModule)
        {
            _eventModule = eventModule;
        }

        /// <summary>
        /// 解析作用域 key。
        /// </summary>
        /// <param name="behaviour">发起方，可为空（例如纯服务器逻辑）</param>
        public static object GetScopeKey(TGameNetworkBridge behaviour = null)
        {
            if (behaviour == null)
                return LocalScope;

            // 服务端：按连接分桶，保证每个玩家的数据互不干扰
            if (behaviour.IsServerInitialized)
                return behaviour.Owner?.IsValid == true ? (object)behaviour.Owner : LocalScope;

            // 客户端：远端玩家在本地只有一个表现作用域
            return behaviour.LocalConnection?.IsValid == true ? (object)behaviour.LocalConnection : LocalScope;
        }

        /// <summary>
        /// 注册作用域事件。
        /// </summary>
        public static void Register<T>(TGameNetworkBridge owner, Action<T> action)
        {
            var bus = Bus;
            if (bus == null || action == null)
                return;

            var scope = GetScopeKey(owner);
            bus.Register(scope, action);
            TrackScopeOwner(owner, scope);
        }

        /// <summary>
        /// 注销作用域事件。
        /// </summary>
        public static void UnRegister<T>(TGameNetworkBridge owner, Action<T> action)
        {
            var bus = Bus;
            if (bus == null || action == null)
                return;

            bus.UnRegister(GetScopeKey(owner), action);
        }

        /// <summary>
        /// 广播作用域事件。
        /// </summary>
        public static void Call<T>(object scope, T value)
        {
            Bus?.Call(scope, value);
        }

        /// <summary>
        /// 广播给某个玩家（服务端调用；会自动改写为对应的连接作用域）。
        /// </summary>
        public static void CallTo<T>(NetworkConnection connection, T value)
        {
            Bus?.Call(connection?.IsValid == true ? (object)connection : LocalScope, value);
        }

        /// <summary>
        /// 广播给所有玩家（全局事件，订阅了全局的监听者会收到）。
        /// </summary>
        public static void CallAll<T>(T value)
        {
            Bus?.Call(value);
        }

        /// <summary>
        /// 清理某个连接的全部订阅（连接断开、玩家离开时调用）。
        /// </summary>
        public static void ClearConnection(NetworkConnection connection)
        {
            if (connection == null)
                return;

            Bus?.ClearScope(connection);
            _scopeOwners.Remove(connection);
        }

        private static void TrackScopeOwner(TGameNetworkBridge owner, object scope)
        {
            if (owner == null)
                return;
            if (scope is NetworkConnection connection)
            {
                if (!_scopeOwners.TryGetValue(connection, out var set))
                {
                    set = new HashSet<UnityEngine.Object>();
                    _scopeOwners[connection] = set;
                }
                set.Add(owner);
            }
        }

        /// <summary>
        /// 清空全部状态（退出 Play 模式或重开局时调用）。
        /// </summary>
        public static void Reset()
        {
            _eventModule = null;
            _scopeOwners.Clear();
        }
    }
}
#endif
