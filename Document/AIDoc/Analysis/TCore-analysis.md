# TCore 框架分析

## 概览

| 项目 | 值 |
|------|-----|
| 程序集 | `TGame` (.asmdef) |
| 命名空间 | `TGame.TCore.Runtime` |
| 外部依赖 | 3 个 GUID 引用 |
| 文件数 | 7 个 .cs + 1 个 .asmdef |

## 架构：三层体系

```
TGame (MonoBehaviour, 单例, DontDestroyOnLoad)
 └─ BaseManager (MonoBehaviour, 可多个)
     ├─ BaseSystem (纯 C# 对象, 0..N)
     │   └─ BaseModule (功能模块, 0..N)
     └─ BaseModule (功能模块, 0..N)
```

### 层级职责

| 层级 | 职责 | 关键操作 |
|------|------|----------|
| **TGame** | 根单例，Manager 注册表 | AddManager / GetManager |
| **BaseManager** | System + Module 管理，事件总线，Unity 生命周期驱动 | AddSystem / GetSystem / GetModule / Call |
| **BaseSystem** | 子系统逻辑，拥有自己的 Module 集合 | 同 Manager 的 System/Module API |
| **BaseModule** | 原子功能单元，可启用/禁用 | Init / Destroy / Tick |

### 生命周期驱动链

```
Manager.Update()      → System.Update()
Manager.FixedUpdate() → System.Tick(dt) → Module.Tick(dt)（若 Enable）
                      → Module.Tick(dt)（若 Enable）
Manager.LateUpdate()  → System.LateUpdate()
```

## 已实现模块

| 模块 | 文件 | 功能 |
|------|------|------|
| EventModule | EventModule.cs | 类型安全事件总线，Action\<T\> 委托 + Register/UnRegister/Call |
| TimerModule | TimerModule.cs | 命名定时器，ObjectPool 管理 TimerEvent 实例，支持注册/注销/覆盖 |
| ObjectPoolModule\<T\> | ObjectPoolModule.cs | 泛型抽象对象池，Queue 存储，子类实现 Create/OnGet/OnRelease |
| GameObjectPoolModule\<T\> | ObjectPoolModule.cs | MonoBehaviour 对象池，基于 prefab 实例化 + showRoot/hideRoot 层级管理 |

## 辅助类型

| 类型 | 说明 |
|------|------|
| TAction\<T\> | 无参泛型委托 `delegate void TAction<T>()` |
| TransformExtension | Transform 扩展方法：ClearChild、ClearChildImmediate、ExChangeSibling |

## 代码问题

1. **ObjectPoolModule\<T\>.Get() 无返回值** — 方法签名 `public void Get()` 无法返回池中对象，调用方拿不到实例
2. **GetModule 相比 GetSystem 行为不一致** — GetModule 不存在时自动创建，GetSystem 不存在时报错，容易误用
3. **Manager/System 的 Module 管理代码重复** — AddModule/RemoveModule/ClearModule/GetModule 逻辑几乎相同，可抽取为公共集合类
