# FishNet 桥接层（TGameNetworkBridge / NetEventHub / TGEntityRegistry）

> 对应缺口 **G8**：TGame 没有 `NetworkBehaviour` 生命周期接入点。
> 位置：`Assets/Plugins/TGame/FishNet/`（TGame 源码真源内）
> 依赖：FishNet 4.x（测试于 4.7.4）、TGame.TCore

## 为什么需要它

矛盾点：FishNet 的对象由服务端 `Spawn` 出来，`Awake` 时还没有网络身份；
TGame 的 Manager 是全局单例，感知不到「哪个对象属于哪个玩家」。
Host 模式下 8 个玩家逻辑跑在同一进程，直接 `EventModule.Call` 会广播给所有人。

## 三个类

| 类 | 类型 | 职责 |
|---|---|---|
| `TGameNetworkBridge` | `NetworkBehaviour` | 挂到网络对象根节点，把生命周期对齐到 TGame；提供 `EntityId`、作用域事件快捷方法 |
| `NetEventHub` | 静态类 | 连接级事件作用域：`Register/UnRegister/Call/CallTo/ClearConnection` |
| `TGEntityRegistry` | `BaseManager` | TGame 侧实体表 `EntityId -> Bridge`；持有 `EventModule` 并注入给 `NetEventHub` |

## 作用域 key 约定

`NetEventHub.GetScopeKey(bridge)`：

| 视角 | 条件 | key |
|---|---|---|
| 服务端 | `IsServerInitialized` 且有 owner | `NetworkConnection`（按连接分桶） |
| 服务端 | 无 owner（场景物件） | `NetEventHub.LocalScope`（`"__local__"`） |
| 客户端 | 有本地连接 | `NetworkConnection`（本地连接，所有远端玩家共享表现作用域） |
| 单机 | 无网络 | `NetEventHub.LocalScope` |

这样服务端每个玩家的生命/分数/违纪事件互不串扰，客户端则统一走本地表现总线。

## 使用步骤

### 1. 场景装配

在 TGame 的 `Game` 预制体下挂 `TGEntityRegistry`（它是 `BaseManager`，会自动注册到 `Game`）。
`Game` 本身需要挂 `NetworkManager`（或独立 GameObject）。

### 2. 玩家预制体

```
Player (NetworkObject)
 ├─ TGameNetworkBridge   (勾 Is Player)
 ├─ Character            (ECM2)
 ├─ CharacterMovement
 └─ Animator
```

### 3. 业务脚本写法

```csharp
public class PlayerHealth : MonoBehaviour
{
    private TGameNetworkBridge _bridge;

    private void Awake() => _bridge = GetComponent<TGameNetworkBridge>();

    private void OnEnable()  => _bridge.Listen<PlayerDamaged>(OnDamaged);
    private void OnDisable() => _bridge.Unlisten<PlayerDamaged>(OnDamaged);

    private void OnDamaged(PlayerDamaged e)
    {
        // 只有订阅到的那一份会进来（服务端按连接隔离）
    }
}
```

初始化逻辑写在 `TGameNetworkBridge` 的子类 `OnNetworkReady()` 里，**不要写 Awake**。

## 注意

- `TGEntityRegistry` 的 `Game.Instance` 不能再写成裸 `Game.Instance` —— 命名空间是 `GameNet.FishNet`，需要 `TGame.TCore.Runtime.Game.Instance` 或 `using Game = TGame.TCore.Runtime.Game;`。
- `NetEventHub` 的 `EventModule` 由 `TGEntityRegistry` 注入；如果业务层先于注册表发事件，`Bus` 为 null 并静默跳过（设计如此，避免初始化顺序踩坑）。
- 玩家对象 `OnStopNetwork` 时会自动 `ClearConnection(Owner)`，不用手动清。
