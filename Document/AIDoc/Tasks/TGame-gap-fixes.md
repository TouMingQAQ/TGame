# TGame 缺口修复记录（Exam_Fight 接入适配）

> 来源：Exam_Fight（《做题区》）项目评审 `TGameSrc/TGame` 时发现的 10 项缺口。
> 分支：`master`，基线提交 `ba91872`（2026-09-16 修改SafeArea的实现）。
> 约定见工作区根目录 `AGENTS.md`：**一次修复 = 一次提交 + 一条记录**。

## 状态总表

| ID | 缺口 | 状态 | 提交 |
|---|---|---|---|
| G5 | `GameSystemManager.Awake` 重复调用 `OnInit` | ✅ 已修 | `4d905db` |
| G4 | `TimerModule` 静态池跨实例共享（串号 / 漏回收） | ✅ 已修 | `153e43a` |
| G3 | `TimerModule` 是一次性定时器，无 Repeat | ✅ 已修 | `957c3d4` |
| G7 | `GameBootstrapper` 全部 `#if UNITY_EDITOR`，打包无初始化入口 | ✅ 已修 | `f104441` |
| G2 | `Tick` 走 `FixedUpdate` 却传 `Time.deltaTime` | ✅ 已修 | `39e1535` |
| G6 | 无运行时场景加载（`SceneNavigator` 只有数据类） | ✅ 已修 | `152c1b4` |
| G1 | 事件总线无实例作用域 | ✅ 已修 | `ce84835` |
| G8 | 无 `NetworkBehaviour` 生命周期接入点 | ⏸ 待做（依赖 FishNet） | — |
| G9 | TUI Addressable 层 P0 缺陷 | 🟡 部分修复（退出 Play 销毁 NRE / Release 恰一次）；Unload 复位、并发去重仍待 Editor 实测 | `18f0bfd` |
| G10 | asmdef 靠 GUID 引用 | 📌 已知约束 | — |

---

## G5 — GameSystem 重复 OnInit

**问题**：`GameSystemManager.Awake` 用 `GetComponentsInChildren<GameSystem>()` 收集后无条件 `system.OnInit(this)`。
子类若在自身 `Awake` 里已经初始化过（注册事件、开定时器），会被二次 `OnInit` 覆盖。

**改动**（`GameSystem/GameSystemManager.cs`）：
- `GameSystem` 新增 `IsInitialized` 属性，`OnInit` 内置位。
- 收集时只有 `!IsInitialized` 才调用 `OnInit`，否则走 `SetManager` 只补挂引用。
- `GetComponentsInChildren<GameSystem>(true)` —— 包含未激活对象。
- `AddGameSystem` 增加 null 保护；`RemoveGameSystem` 移除时把 `Manager` 置 null，避免悬挂引用。

**影响面**：`GameSystem` 子类如果在 `Awake` 里自己调 `OnInit`，行为变成"只初始化一次"（符合预期）。
未在 `Awake` 初始化的子类行为不变。

**兼容**：`OnInit` 签名不变；新增 `internal SetManager` 只在本程序集可见。

---

## G4 — TimerModule 静态对象池

**问题**：`_timerEventPool` 是 `static`，所有 `TimerModule` 实例共享。两个 Manager 各挂一个 `TimerModule` 时，
A 模块 `Release` 的 `TimerEvent` 会被 B 模块 `Get` 到，造成定时器串号。
另外 `Release` 只归还实例但不重置 `CallBack`/`Timer`，复用时会带脏数据；已触发的槽位永远留在字典里（`Timer = -1` 不删），字典只增不减。

**改动**（`TCore/Runtime/Module/TimerModule.cs`）：
- 池改为实例级 `readonly ObjectPool<TimerEvent>`（`collectionCheck: true`，`maxSize: 64`）。
- `Create` / `Release` 由 `static` 改为实例方法；移除静态重载。
- 新增 `actionOnRelease: ResetTimerEvent`，归还时清空全部字段。
- `Tick` 触发后加入 `_removeCache / _releaseCache`，遍历结束统一删字典 + 归还池。
- 新增 `IsRunning(name)` / `GetRemaining(name)` 查询接口。

**影响面**：**破坏性变更** —— 调用方如果写过 `TimerModule.Create(...)` / `TimerModule.Release(...)` 这种静态调用，需改为 `timerModule.Create(...)`。
仓库内目前无此类调用（仅 Editor 侧示例），已全量 grep 确认。

**遗留**：`Tick` 仍然由宿主驱动，受 G2 影响；G3 之后循环定时器推荐用 `RegisterInterval`。

---

## G3 — TimerModule 循环定时器

**问题**：原实现是纯粹的一次性定时器，触发后 `Timer = -1` 且不再参与计算。
策划案里"每秒回复 20% 生命""违纪每秒扣 1 分""打火机按秒扣耐久"都需要循环计时，
不改框架就得每帧判断"触发 → 重新 Register"，既啰嗦又依赖名字去重逻辑。

**改动**（同上文件）：
- `TimerEvent` 新增 `RepeatCount`（0=一次性，>0=剩余次数，`-1`=无限循环）与 `TotalRepeatTriggered`。
- `TimerEvent.InfiniteRepeat` 常量，避免魔法数字。
- `Tick` 分支处理：无限循环按周期推进 `Timer`（防止长时间运行后浮点累加过大）；有限循环可在同一帧跨多个周期并自动回收。
- 新增 `RegisterOnce(name, time, action, cover)` 与 `RegisterInterval(name, interval, action, repeatCount, cover)`。

**用法**：
```csharp
var timer = GetModule<TimerModule>();
// 一次性
timer.RegisterOnce("respawn", 3f, () => Respawn());
// 每秒结算，无限
timer.RegisterInterval("examTick", 1f, OnExamSecond);
// 只在 10 次内结算
timer.RegisterInterval("bleed", 0.5f, OnBleedTick, repeatCount: 10);
```

---

## G7 — GameBootstrapper 打包失效

**问题**：整个类体被 `#if UNITY_EDITOR` 包裹，Player 构建里 `Awake`/`Start` 都是空的，
打包后没有任何 TGame 初始化入口，只能靠场景里预摆 `Game` 预制体。

**改动**（`TCore/Runtime/GameBootstrapper.cs`）：
- 去掉整类包裹，`Awake`/`Start` 在 Player 中同样执行。
- 目标场景按优先级解析：`SetTargetScene()` 运行时指定 > 序列化字段 `_targetScene` > Editor 下 `EditorPrefs`。
- 新增静态事件 `Bootstrapped`（场景加载完成后广播）。
- 新增 `ResetBootstrapState()`，支持回主菜单 / 重开局后再次引导。
- 新增 `_destroyOnBootstrapped` 开关，默认 `false` 保持旧行为。

**兼容**：`TargetSceneKey` 常量保留，ToolBox 的启动场景选择器不受影响。

---

## G2 — Tick 时间语义

**问题**：`BaseManager.FixedUpdate` 里调用 `module.Tick(Time.deltaTime)`。
固定帧驱动却传帧间时间，同一物理帧内若被多次调用会重复累加；且 `Time.deltaTime` 受 `timeScale` 影响，
暂停/慢动作时定时器与数值结算会一起漂移。

**改动**：
- `TGame.BaseClass.cs`：`FixedUpdate` 改传 `Time.fixedDeltaTime`；抽出 `TickModules(float deltaTime)`；
  遍历时复制 key 列表，允许模块在 `Tick` 内增删模块而不抛 `InvalidOperationException`。
- `Module/ModuleEntity.cs`：同样抽出 `TickModules(float deltaTime)`，保留 `unscaledDeltaTime` 的 UI 语义并写入注释。

**影响面**：所有挂在 `BaseManager` 下的 `BaseModule`，`Tick` 收到的 delta 从"帧时间"变成"固定步长"。
原本依赖 `Time.deltaTime` 语义的模块（如 Tween 推进）需要改用 `Time.unscaledDeltaTime` 或自己 `Update` 驱动。
仓库内目前只有 `TimerModule` 用 `Tick`，已同步验证。

---

## G6 — 运行时场景加载

**问题**：`SceneNavigator` 模块只有 `SceneEntry` 数据类 + Editor 面板，运行时没有任何加载 API。
`GameBootstrapper` 跳转后就没有下文，业务侧只能自己 `SceneManager.LoadScene`。

**改动**（新增 `SceneNavigator/Runtime/SceneNavigator.cs`）：
- 静态类，提供 `Load` / `LoadAdditive` / `Unload` / `LoadAsync`。
- `SetProfile(SceneNavigatorProfile)` 注册别名表，`ResolveScene(alias)` 解析为路径。
- 事件：`LoadingProgress` / `LoadCompleted` / `UnloadCompleted` / `LoadFailed`，状态：`IsLoading` / `CurrentScene`。
- 用 `Awaitable.NextFrameAsync` 驱动进度（Unity 6 API），进度从 `0.9` 归一化到 `0~1`。
- 加载进行中拒绝重复请求。

**注意**：这是本地场景加载，**不等于联机同步**。FishNet 接入后，联机场景切换仍需走 FishNet `SceneManager`，
本类只复用它的事件与别名解析。

---

## G1 — 事件总线无作用域

**问题**：`EventModule` 只能按事件类型 `Call<T>` 广播，无法定位到具体玩家/单位。
Exam_Fight 是 Host 模式（房主即服务器），8 个玩家的逻辑在同一进程内，
`PlayerDamaged`、`ScoreChanged`、`ViolationDetected` 这类事件会广播给**所有**玩家逻辑，结算必然错乱。

**改动**（`TCore/Runtime/Module/EventModule.cs`）：
- 新增 `_scopedEventMap: scope -> (Type -> Delegate)`。
- 新增 `Call<T>(scope, value)` / `Register<T>(scope, action)` / `UnRegister<T>(scope, action)`。
- 新增 `ClearScope(scope)`（实体销毁时批量解绑，防泄漏）/ `HasScope(scope)` / `ClearAll()`。
- `scope == null` 时退化为全局事件，避免静默丢事件。
- `Call` 改为快照 + 逐个 `try-catch`：监听者可在回调里增删订阅，单个监听者异常不影响其它监听者。
- `Register` 去重：同一 `target + method` 重复注册不再重复触发。
- 覆盖 `Destroy()`，模块销毁时清空全部订阅。

**用法**：
```csharp
var bus = GetModule<EventModule>();

// 全局（原有用法，不变）
bus.Register<GameStarted>(OnGameStarted);
bus.Call(new GameStarted());

// 作用域：只发给订阅了 playerId 的监听者
bus.Register<PlayerDamaged>(playerId, OnLocalPlayerDamaged);
bus.Call(playerId, new PlayerDamaged { Amount = 10 });

// 玩家销毁
bus.ClearScope(playerId);
```

**尚未处理的部分**：
- `BaseManager` 自己那套 `Call/Register/UnRegister`（protected）仍是全局语义，与 `EventModule` 两套并存。
  TUI 内部统一走 `UIManager.Call<T>`，本次未动，避免影响 UI 体系。
- 与 FishNet 的桥接（服务端事件 → `ObserversRpc` / `SyncVar` → 客户端作用域事件）属于游戏侧 `NetEventHub` 的职责，
  不放框架里，见工作区 `AGENTS.md` 的分层约定。

---

## G9（部分）— TUI 退出 Play 销毁 NRE

**实测证据**（2026-11-03T02:07:21Z 退出 Play，各 1 次 NRE）：
`UILoaderModule.get_Registry()`（`TUI/Runtime/Model/UILoaderModule.cs:43`）
→ `Destroy()`（:154）→ `ModuleEntity.ClearModule()`（:56）→ `UIRoot.OnDestroy()`（:124）；
以及 `ModuleEntity.OnDestroy()`（:100）。

**根因**：
1. 退出 Play 时 Unity 销毁顺序不定，`UIManager.OnDestroy` 可能先执行并把 `UIManager.Instance` 置 null；
   `UILoaderModule.Destroy` 仍通过 `UIManager.Instance.Registry` / `GetModule<AddressableModule<...>>()`
   解析依赖 → 空单例 NRE。
2. 该异常打断 `ModuleEntity.ClearModule` 的 `foreach`，`_moduleMap.Clear()` 未执行，map 仍满；
   随后 `ModuleEntity.OnDestroy` 再次 `ClearModule`，对同一批模块重复 `Destroy` → 第二条 NRE。
   属"依赖静态单例 + 清理非幂等"，**不是**靠 catch 吞异常能解决的问题。

**改动**（提交 `18f0bfd`，仅 2 个 TUI 文件，保留全部公开 API，未改 TCore / Addressables 包）：
- `UILoaderModule.cs`：依赖改为 `Bind(UIRegistryModule, AddressableModule<BaseUIPanel>)` 注入缓存，
  销毁阶段不再访问静态单例；新增 `_loadedAddresses` 在加载期记录 address，`Unload`/`Destroy`
  按缓存 address `Release`（引用计数恰一次）；`Destroy` 增加 `_destroyed` 幂等 + 快照遍历；
  加载返回后若宿主已销毁则归还刚取引用。`Bind` 内复位 `_destroyed`，不因复用永久失效。
- `UIRoot.cs`：`Initialize` 注入依赖；`OnDestroy` 增加 `_moduleEntity` 空判兜底（无异常时二次
  `ClearModule` 面对空表天然幂等）。

**验证**：`dotnet build TUI.csproj -nologo` → 0 error / 0 warning(CS)；真源 = EFProject 部署（逐文件 SHA256）。
**尚未验证**：Unity Editor 退出 Play 的实际 NRE 消除（需集成者实测）；G9 的 Unload 状态复位、
并发去重、句柄引用计数运行时语义仍未实测，**不宣称 G9 全部完成**。

**边界**：`AddressablesImpl.Dispose invalid operation handle` 归属 Boot 侧（`SceneRouter.Dispose` 直接
Release scene handle 与 `SceneUnloaded` 自动释放叠加，`m_SceneInstances` 残留），由 Boot 作者单独只读诊断，
不属于本 TUI 修复范围，本修复不保证消除该条。

---

## 待办

- **G8**：`NetworkBehaviour` 生命周期接入点 —— 需要先引入 FishNet，再写 `TGameNetworkBridge`
  （`OnStartNetwork` 注册到 TGame、`OnStopNetwork` 注销 + `ClearScope`）。
- **G9**：TUI Addressable 层 P0 缺陷 —— 退出 Play 销毁 NRE 与 Release 恰一次已修（`18f0bfd`）；
  余下 Unload 状态复位、并发去重、句柄引用计数仍需在 Unity Editor 实测，`dotnet build` 无法验证运行时行为。
- **G10**：asmdef GUID 约束 —— 已在 `AGENTS.md` 记录为新建模块的强制检查项。
