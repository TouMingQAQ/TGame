# TCore 问题与改进方案

> 最近更新：2026-06-14，基于 master 分支 HEAD（1eb5fca）。
>
> 早期文档（写于 `TGame.cs` 早期阶段）的 3 个问题中，**问题 1 已修复**，问题 2/3 仍按"决策记录"形式保留供后续参考。

---

## 问题 1：`ObjectPoolModule<T>.Get()` 无返回值（**已修复**）

### 历史问题

早期 `ObjectPoolModule.cs` 的 `Get()` 签名是 `public void Get()`，调用方无法拿到取出的对象，等于无意义的空操作。

### 现状（已修复）

`ObjectPoolModule.cs:15-21`：

```csharp
public T Get()
{
    if (!_poolQueue.TryDequeue(out var obj))
        obj = Create();
    OnGet(obj);
    return obj;
}
```

返回类型已改为 `T`，业务方可以拿到实例。**问题 1 关闭，无需进一步处理。**

### 关联变更

- `GameObjectPoolModule<T>` 继承后无重写 `Get`，自动复用基类。
- 当前 TUI 体系没有直接使用 `ObjectPoolModule<T>`，主要是 `TimerModule` 内嵌的 `UnityEngine.Pool.ObjectPool<TimerEvent>` 在用 Unity 自带池。

---

## 问题 2：`GetModule` vs `GetSystem` 行为不一致（**保留差异**）

### 现状

`BaseManager.GetModule<T>()`（`TGame.BaseClass.cs:48-54`）：

```csharp
public T GetModule<T>() where T : BaseModule, new()
{
    var type = typeof(T);
    if (_moduleMap.TryGetValue(type, out var value))
        return value as T;
    return AddModule<T>();   // 不存在 → 自动 AddModule
}
```

- **`GetModule<T>`** — 不存在时**自动创建**（`AddModule`）
- **`BaseManager.Call<T>` 内部**对应"System"语义的"不存在时 LogError"

### 决策：保留差异，明确语义

Module 是轻量级功能单元，允许懒加载；System 是重量级子系统，必须显式初始化。差异是刻意的。

**当前实际**：`BaseSystem` 类已删除，原"GetSystem"行为不再存在。`UIManager` 等所有 Manager 的 Module API 都走"懒加载自动创建"语义，业务方无需显式 `Init` 即可 `GetModule<T>()` 拿到实例。

**对调用方的影响**：

| 模式 | 行为 | 是否需要 `Init` |
| --- | --- | --- |
| 首次 `GetModule<T>()` | 自动 `AddModule`（含 `Init()`） | 否 |
| 后续 `GetModule<T>()` | 命中缓存 | 否 |
| 主动 `RemoveModule<T>()` | 调 `Destroy()` + 移除 | 不需要单独 `Init` |

**结论**：当前实现已统一为"懒加载自动 Init"，原"GetSystem 报错 / GetModule 自动创建"分歧不再存在。**问题 2 已过时，本节作为决策记录保留。**

---

## 问题 3：Manager 与 System 的 Module 管理代码重复（**不修复**）

### 历史现状

早期版本 `BaseManager` 和 `BaseSystem` 各自实现相同的 4 个方法：

- `AddModule<T> / RemoveModule<T> / ClearModule / GetModule<T>`

代码逻辑几乎完全一致，仅 `_moduleMap` 字段定义不同。

`BaseSystem` 已删除，问题自然消失。`BaseManager` 内部统一管理 `_moduleMap`，无重复代码。

### 方案对比（保留作历史记录）

| 方案 | 改动量 | 复杂度 | 灵活性 | 状态 |
| --- | --- | --- | --- | --- |
| A. 抽取 `ModuleCollection` 组合类 | 中 | 低 | 高 | 不需要 |
| B. 让 System 继承共同基类 | 大 | 中 | 低 | 已被架构演进取代 |
| C. 不修改，接受重复 | 无 | 无 | — | 不再适用 |

**结论**：随着 `BaseSystem` 被删除，"重复代码"问题已不存在。**问题 3 关闭。**

---

## 新增观察（基于 2026-06 HEAD 复检）

复检最新代码时发现以下 4 个值得后续关注的设计点（非紧急，记入决策备查）：

### 观察 1：EventModule 与 BaseManager.Call 重复

两套事件 API 共存：

| API | 所在类 | 字典 | 公开度 |
| --- | --- | --- | --- |
| `Call<T> / Register<T> / UnRegister<T>` | `BaseManager` | `BaseManager._eventMap` | `Call` 公开，`Register/UnRegister` protected |
| `Call<T> / Register<T> / UnRegister<T>` | `EventModule` | `EventModule._eventMap` | 全部公开 |

形态完全一样但字典隔离。`TUI` 体系内部统一走 `UIManager.Call<T>`，从不挂 `EventModule`，建议：

- **方案 A（推荐）**：在文档/代码注释明确"`TUI`/Manager 内部走 Manager 事件总线，第三方子系统用 EventModule"，保留两套
- **方案 B**：合并为一套（删 `EventModule` 或在 `BaseManager` 委托给 `EventModule`），影响面大

### 观察 2：Tick 走 FixedUpdate + Time.deltaTime

```csharp
// BaseManager.FixedUpdate:
if (module.Enable) module.Tick(Time.deltaTime);
```

物理帧驱动但传帧间时间，**与"按物理频率"的直觉不符**。对帧率敏感模块（如 Tween 推进、UI 动画）会有累积误差。建议：

- 高频/帧率敏感逻辑自行 `Update` 驱动，**不要**挂在 `BaseManager.GetModule<T>()` 的 Tick 上
- 或在 `BaseManager` 暴露一个可选的 `Update` 钩子

### 观察 3：ObjectPoolModule 泛型约束 `new()` 故意不写

```csharp
public abstract class ObjectPoolModule<T> : BaseModule
{
    protected abstract T Create();   // 子类自管构造
}
```

子类的 `Create()` 可以引用 `prefab` 等构造参数。如果未来出现"必须 `new()`"的误用情况，可考虑在 `ObjectPoolModule<T>` 改为泛型约束 `where T : new()` 强制。

### 观察 4：TimerModule 静态池跨实例共享

```csharp
private static ObjectPool<TimerEvent> _timerEventPool = new ObjectPool<TimerEvent>(...);
```

**所有 `TimerModule` 实例共享同一个 `TimerEvent` 池**。如果出现"两个 Manager 都挂了 `TimerModule`"的情况，回收的 TimerEvent 会被另一个 Manager 取走，**有竞态风险**。现状是 TCore 内仅 `GameBootstrapper` 旁路使用，业务 Manager 暂未挂 `TimerModule`，暂未触发。后续若推广到多 Manager，需改为实例级池。

---

## 决策摘要

| 问题 | 状态 | 处理 |
| --- | --- | --- |
| 1. `Get()` 无返回值 | **已修复** | 关闭 |
| 2. `GetModule` vs `GetSystem` 不一致 | **已过时**（架构演进统一为懒加载） | 关闭 |
| 3. Manager/System 重复代码 | **已过时**（BaseSystem 删除） | 关闭 |
| 观察 1：双事件总线 | 待决策 | 文档化分歧，暂不合并 |
| 观察 2：Tick 频率语义 | 待决策 | 业务方自行 Update 驱动 |
| 观察 3：`new()` 约束 | 待决策 | 暂不动 |
| 观察 4：TimerModule 静态池 | **潜在 bug** | 多 Manager 推广前需修 |
