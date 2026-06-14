# TCore 框架分析

> 最近更新：2026-06-14，基于 master 分支 HEAD（1eb5fca）。
> 范围：`Assets/Plugins/TGame/TCore/Runtime/`。

## 概览

| 项目 | 值 |
| --- | --- |
| 程序集 | `TGame`（`.asmdef`，GUID: `TGame.asmdef`） |
| 命名空间 | `TGame.TCore.Runtime` |
| 文件数 | 7 个 .cs（Game / TGame.BaseClass / TGame.Extension / TGame.Interface / GameBootstrapper + 3 个 Module） |
| 外部依赖 | 0 — 纯 Unity API，不引任何第三方库 |

## 架构：两层体系

```text
TGame (MonoBehaviour, 单例, DontDestroyOnLoad)
 └─ BaseManager (MonoBehaviour, 可多个)
     └─ BaseModule (纯 C# 对象, 0..N)
```

注意：早期文档描述的"Game → BaseManager → BaseSystem → BaseModule"四层中，**`BaseSystem` 已不存在**。当前代码是 `Game → BaseManager → BaseModule` 三层。`BaseManager` 内部直接持有 `BaseModule` 字典。

### 层级职责

| 层级 | 职责 | 关键操作 |
| --- | --- | --- |
| **`Game`** | 根单例，Manager 注册表。`DefaultExecutionOrder(-9000)` 确保最早 Awake | `AddManager<T> / GetManager<T> / LogInfo` |
| **`BaseManager`** | Module 容器 + 事件总线 + Unity 生命周期驱动。`DefaultExecutionOrder(-8000)` | `AddModule<T> / GetModule<T> / RemoveModule / ClearModule / Call<T> / Register<T> / UnRegister<T>` |
| **`BaseModule`** | 原子功能单元，Enable/Disable 控制 Tick | `Init / Destroy / Tick(dt) / Enable` |

### 生命周期驱动链

```csharp
BaseManager.FixedUpdate() {
    foreach (var module in _moduleMap.Values)
        if (module.Enable) module.Tick(Time.deltaTime);
}
```

**只走 `FixedUpdate`**，没有 `Update / LateUpdate` 链路。`Module.Tick` 的入参是 `Time.deltaTime`（不是 `Time.fixedDeltaTime`），**与文档"按物理帧驱动"不符**——按代码现状是"每物理帧把 `Time.deltaTime` 传给 Module"，业务方拿到的是帧间时间。

事件总线通过 `BaseManager.Call<T>(T value)` 广播，`Register<T>(Action<T>)` / `UnRegister<T>(Action<T>)` 维护委托多播。**与 Module 内部 `EventModule` 是两套独立体系**——Module 的事件字典不与 Manager 的事件字典互通。

## 已实现模块

`Assets/Plugins/TGame/TCore/Runtime/Module/` 三个文件，**4 个具体类**：

| 模块 | 文件 | 功能 |
| --- | --- | --- |
| `EventModule` | `EventModule.cs` | 类型安全事件总线，`Dictionary<Type, Delegate> _eventMap`，`Call<T> / Register<T> / UnRegister<T>`，与 `BaseManager` 事件 API 完全独立 |
| `TimerModule` | `TimerModule.cs` | 命名定时器，`Dictionary<string, TimerEvent>`；静态 `ObjectPool<TimerEvent>` 复用实例；`Tick(dt)` 推进计时；支持 `cover` 覆盖和 `-1`（已触发）槽位复用 |
| `ObjectPoolModule<T>` | `ObjectPoolModule.cs` | 泛型抽象池，`Queue<T>` 存储；`Get()` 返回实例（**已修复**早期"无返回值"问题）；`Create / OnGet / OnRelease` 三个虚方法 |
| `GameObjectPoolModule<T>` | `ObjectPoolModule.cs` | 继承 `ObjectPoolModule<T>`，约束 `T : MonoBehaviour`；构造时传入 `prefab / showRoot / hideRoot`；`OnGet` 挂 `_showRoot`，`OnRelease` 挂 `_hideRoot`，自动 `SetActive(false)` 隐藏根 |

### 关键设计点

- **`ObjectPoolModule<T>` 是抽象类**，`Create()` 必须由子类实现。`Get()` 返回 `T`（不是 `void`），调用方拿到的是新创建或复用对象。
- **`TimerModule` 用 Unity 自带 `UnityEngine.Pool.ObjectPool<TimerEvent>`**（不是自家泛型池），静态共享一个池，跨实例复用。`Register(timerEvent, cover=false)` 走 `TryAdd`，同名重复注册会 `Release(newEvent) + LogWarning`，除非 `cover=true` 或旧槽位 `Timer < 0`（已触发过）才覆盖。
- **`EventModule` 与 `BaseManager` 的事件总线功能重复**——前者挂在 Manager 下，后者直接由 Manager 暴露。两套 API 形同（`Call<T> / Register<T> / UnRegister<T>`），但字典隔离。

## 辅助类型

| 类型 | 位置 | 说明 |
| --- | --- | --- |
| `TAction<T>` | `TGame.Interface.cs` | 无参泛型委托 `delegate void TAction<T>()`，项目内替代 `Action` 风格的别名（实际很少使用） |
| `TransformExtension` | `TGame.Extension.cs` | `ClearChild`（Destroy 子物体）/ `ClearChildImmediate`（DestroyImmediate）/ `ExChangeSibling`（同父节点交换下标） |

## 引导与启动

`GameBootstrapper` 挂在初始场景（典型 `Start.unity`）：

- `DefaultExecutionOrder(-100)`，在 `Game` 之后但早于业务 Manager
- `Awake` 校验 `Game.Instance != null`
- `Start`（`#if UNITY_EDITOR` 包裹）从 `EditorPrefs["TGame_InitBoot_TargetScene"]` 读目标场景路径，**有就加载并 DeleteKey**，无就 LogWarning 跳过
- 静态标志 `_hasBootstrapped` 防止目标场景内嵌的同名 Bootstrapper 触发循环加载
- **生产环境此脚本无效**（`#if UNITY_EDITOR` 包裹所有加载逻辑），仅供 Editor 场景间跳转

## 模块间依赖与使用方式

### 业务方使用模块的标准模式

```csharp
// 在自定义 Manager 的 Awake 末尾:
var module = GetModule<EventModule>();
module.Register<MyEvent>(OnMyEvent);

// Tick 触发:
module.Tick(Time.deltaTime);   // Manager.FixedUpdate 自动驱动
```

### 跨 Manager 通信

```csharp
// Manager A 发事件:
this.Call(new MyEvent { ... });

// Manager B 收事件:
protected override void Awake() {
    base.Awake();
    Register<MyEvent>(OnMyEvent);   // 注意:Register 是 protected
}
```

**注意**：`Register / UnRegister` 在 `BaseManager` 上是 `protected`，业务方需要在自己的 Manager 里包一层 `public` 转发，或直接订阅 Module 内部 `EventModule`。

## 已知设计权衡

1. **两层 vs 三层体系**：`BaseSystem` 已删除。StackPanelModel / PopupModule 这类"逻辑模块"直接挂在 `UIManager : BaseManager` 下，与 `EventModule` 平级。命名上仍叫 "Model" / "Module" 但实质都是 `BaseModule`。
2. **Module 事件与 Manager 事件并存**：`EventModule` 和 `BaseManager.Call<T>` 都提供"类型安全事件"语义，使用时需明确选哪套。**TUI 体系内部统一走 `BaseManager.Call<T>`**（即 `UIManager.Call<T>`），不用 `EventModule`。
3. **`Tick` 频率**：受 `FixedUpdate` 驱动，调用 `Time.deltaTime`（不是 `Time.fixedDeltaTime`）。高频/帧率敏感逻辑建议业务方自己 `Update` 驱动。
4. **`ObjectPoolModule<T>.Create()` 抽象**：泛型约束 `new()` 故意不写，子类用构造参数（如 `prefab`）更灵活。

## 相关文档

- [TCore 问题与改进方案](TCore-issues-fix.md)
- [TUI 栈式 Panel 分析（已重构）](../Analysis/TUI-StackPanelModel.md)
- [TUI 浮窗子系统分析（已重构）](../Analysis/TUI-PopupModule.md)
