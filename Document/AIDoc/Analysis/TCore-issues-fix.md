# TCore 问题解决方案

## 问题 1：ObjectPoolModule\<T\>.Get() 无返回值

**现状** `ObjectPoolModule.cs:15-20`

```csharp
public void Get()
{
    if (!_poolQueue.TryDequeue(out var obj))
        obj = Create();
    OnGet(obj);
}
```

调用方无法拿到取出的对象，`Get()` 变成无意义的空操作。

**修复**

```csharp
public T Get()
{
    if (!_poolQueue.TryDequeue(out var obj))
        obj = Create();
    OnGet(obj);
    return obj;
}
```

**影响文件** 仅 `ObjectPoolModule.cs`，1 行改动。

---

## 问题 2：GetModule 与 GetSystem 行为不一致

**现状**

| 方法 | 不存在时 |
|------|----------|
| `GetModule<T>()` | 自动创建（调用 AddModule） |
| `GetSystem<T>()` | 报错，返回 default |

**分析** 这是个设计决策问题，不是 bug。两个方向都合理：

- **统一为自动创建**：调用方省心，但隐藏了"系统未初始化"的错误
- **统一为报错**：显式声明依赖，但调用方每次都要先判断

**建议方案：保留差异，明确语义**

Module 是轻量级功能单元，允许懒加载；System 是重量级子系统，必须显式初始化。差异是刻意的，**不修改代码，只补文档**：

- `GetSystem` 的 XML 注释已说明"不存在会报错"，保持不变
- `GetModule` 文档补充"不存在时自动创建"

如需更严格的模式，后续可增加 `TryGetSystem` / `TryGetModule` 方法。

---

## 问题 3：Manager 与 System 的 Module 管理代码重复

**现状** `BaseManager` 和 `BaseSystem` 各自实现相同的 4 个方法：

```
AddModule<T> / RemoveModule<T> / ClearModule / GetModule<T>
```

代码逻辑完全一致，仅 `_moduleMap` 字段定义不同。

**方案对比**

| 方案 | 改动量 | 复杂度 | 灵活性 |
|------|--------|--------|--------|
| A. 抽取 `ModuleCollection` 组合类 | 中 | 低 | 高 |
| B. 让 System 继承共同基类 | 大 | 中 | 低 |
| C. 不修改，接受重复 | 无 | 无 | — |

**推荐方案 A**：抽取为独立组合类

新增 `ModuleCollection.cs`:

```csharp
public class ModuleCollection
{
    private Dictionary<Type, BaseModule> _moduleMap = new();

    public T Add<T>() where T : BaseModule, new()
    {
        var type = typeof(T);
        if (_moduleMap.TryGetValue(type, out var value))
            return value as T;
        var module = new T();
        module.Init();
        _moduleMap[type] = module;
        return module;
    }

    public T Remove<T>() where T : BaseModule { ... }
    public void Clear() { ... }
    public T Get<T>() where T : BaseModule, new() { ... }
    public void Tick(float dt) { ... }
}
```

`BaseManager` 和 `BaseSystem` 改为持有 `ModuleCollection` 实例，原有 `AddModule/GetModule/...` 方法变为一行转发（保留 API 兼容）。
