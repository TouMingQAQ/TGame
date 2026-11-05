#if FISHNET
using FishNet.Connection;
using FishNet.Object;
using TGame.TCore.Runtime;
using UnityEngine;

namespace GameNet.FishNet
{
    /// <summary>
    /// TGame × FishNet 桥接组件（G8）。
    ///
    /// 职责：
    ///   1. 把网络对象的生命周期对齐到 TGame：OnStartNetwork 注册、OnStopNetwork 注销；
    ///   2. 提供稳定的实例 Id（服务端ObjectId / 客户端 ObjectId），供 TGame 作用域事件使用；
    ///   3. 玩家离开时清理该连接在 <see cref="NetEventHub"/> 上的所有订阅，避免泄漏。
    ///
    /// 使用：挂在带 NetworkObject 的预制体根节点上，业务脚本通过 GetComponent 访问。
    /// 注意：网络对象的初始化必须放在 OnStartNetwork（或 OnStartServer/OnStartClient），
    ///      不能放 Awake —— FishNet 对象在 Awake 时还没绑定网络身份。
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class TGameNetworkBridge : NetworkBehaviour
    {
        [Header("TGame 注册")]
        [Tooltip("勾选后在 OnStartNetwork 时把本对象注册进 TGame 的实体表，OnStopNetwork 时注销。")]
        [SerializeField]
        private bool _registerToTGame = true;

        [Tooltip("是否把本对象视为玩家（玩家对象离开时会清理连接上的全部事件订阅）。")]
        [SerializeField]
        private bool _isPlayer;

        /// <summary>
        /// TGame 侧实体 Id。服务端用 ObjectId，客户端用 ObjectId，保证两端各端内部唯一。
        /// </summary>
        public int EntityId { get; private set; } = -1;

        /// <summary>
        /// 是否已完成网络初始化。
        /// </summary>
        public bool IsNetworkReady { get; private set; }

        /// <summary>
        /// 对应的连接作用域（NetEventHub 使用的 key）。
        /// </summary>
        public object ScopeKey => NetEventHub.GetScopeKey(this);

        #region FishNet 生命周期

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            EntityId = ObjectId;
            IsNetworkReady = true;

            if (_registerToTGame)
            {
                var entityRegistry = GetEntityRegistry();
                entityRegistry?.Register(this);
            }

            // 业务初始化放这里，而不是 Awake
            OnNetworkReady();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            IsNetworkReady = false;

            if (_registerToTGame)
            {
                var entityRegistry = GetEntityRegistry();
                entityRegistry?.Unregister(this);
            }

            // 玩家对象销毁时清掉该连接上的全部订阅，防止 NetEventHub 里留悬挂委托
            if (_isPlayer && IsServerInitialized && Owner?.IsValid == true)
                NetEventHub.ClearConnection(Owner);

            OnNetworkStopped();

            EntityId = -1;
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            // 所有权变化后作用域 key 可能变化（例如从旁观视角变成自己操控），需要重新订阅
            OnNetworkOwnershipChanged();
        }

        #endregion

        #region 供业务重写的钩子

        /// <summary>
        /// 网络初始化完成（OnStartNetwork 之后）。业务初始化写在这里。
        /// </summary>
        protected virtual void OnNetworkReady() { }

        /// <summary>
        /// 网络停止（OnStopNetwork 之后）。业务清理写在这里。
        /// </summary>
        protected virtual void OnNetworkStopped() { }

        /// <summary>
        /// 所有权变化时触发。
        /// </summary>
        protected virtual void OnNetworkOwnershipChanged() { }

        #endregion

        #region TGame 帮助方法

        /// <summary>
        /// 本对象的本地作用域事件是否归属我（只有 Owner 或服务端无主对象为 true）。
        /// </summary>
        public bool IsLocalScopeOwner => IsController;

        /// <summary>
        /// 注册作用域事件（自动绑定到本对象的连接作用域）。
        /// </summary>
        public void Listen<T>(System.Action<T> action) => NetEventHub.Register(this, action);

        /// <summary>
        /// 注销作用域事件。
        /// </summary>
        public void Unlisten<T>(System.Action<T> action) => NetEventHub.UnRegister(this, action);

        /// <summary>
        /// 向本对象所在的作用域广播事件。
        /// </summary>
        public void Emit<T>(T value) => NetEventHub.Call(ScopeKey, value);

        private static TGEntityRegistry GetEntityRegistry()
        {
            var game = TGame.TCore.Runtime.Game.Instance;
            return game == null ? null : game.GetManager<TGEntityRegistry>();
        }

        #endregion
    }
}
#endif
