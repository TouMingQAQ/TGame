# StackPanelModel 重构分析

> 最近更新：2026-06-14，基于 master 分支 HEAD（1eb5fca）。
> 重构时间线：1de65ee（`feat(TUI): 抽出 StackPanelModel`）→ ea90105（`refactor(TUI): UIManager 拆分为 4 个 BaseModule`）→ 595240f（`fix(TUI): 修复 Popup 坐标与动画重入`）。
> 本文档分析 `StackPanelModel` 本体，UIManager 拆分与 Popup 修复分别见 [TUI-PopupModule.md](TUI-PopupModule.md) 和对应 commit。

## 概览

| 项目 | 值 |
| --- | --- |
| 模块 | `TGame.TUI.StackPanelModel` |
| 父级 | `UIManager`（作为 `BaseModule` 挂在它下面） |
| 文件 | `Assets/Plugins/TGame/TUI/Runtime/Model/StackPanelModel.cs` + `StackPanelEntry.cs` |
| 命名空间 | `TGame.TUI` |
| 提交 | 1de65ee + ea90105 |

## 重构前的问题

`UIManager` 早期同时承担两类职责：

1. **加载/单实例显隐注册表** —— `_loadedPanels` 字典 + `LoadPanel/ShowPanel/HidePanel/UnloadPanel`
2. **栈式 Panel 管理** —— `_stack` 列表 + `PushPanel/PopPanel/BackTo/PopToRoot` 等 11 个方法

职责耦合导致：

- **关系缺失**：`PushPanel` 不接收 parent，新面板叠加在"当前栈顶"上，无法表达"在 A 上面打开 B"以外的语义。
- **层级守门缺失**：若 A 实际注册在 Layer2、要打开的 B 注册在 Layer1，旧代码照常打开，导致 sibling 顺序与 Layer 顺序矛盾。
- **外部关闭无兜底**：业务方直接调 `uimgr.HidePanel<StackSubPanel>()` 时，栈顶条目残留，需要业务方自己管理。
- **模块结构不一致**：`BaseUIPanel.OnPushed/OnPopped` 钩子和 `PanelPushedEvent/PanelPoppedEvent` 事件都"沾"在 UIManager 上，不符合"Model 自治"的设计。

## 重构后架构

```text
UIManager : BaseManager
 ├─ UIRegistryModule         面板 prefab + layer 注册表
 ├─ UILayerRootModule        6 个 layer 根 Transform 字典
 ├─ UILoaderModule           Panel 实例化 + 缓存 + RectTransform 填充 + Init + Unload
 ├─ UIVisibilityModule       Show/Hide + SetAsLastSibling + 事件广播
 ├─ StackPanelModel          栈式 Panel 管理（Push/Pop/层级守门）
 └─ PopupModule              浮窗子系统（独立 Tooltip Layer）
        ↓
UIManager 仅做"加载/单实例显隐"职责的薄壳转发,栈逻辑完全由 Model 自治
```

`UIManager` 退回到"加载/单实例显隐"职责,栈逻辑完全由 `StackPanelModel` 自治。**`UIManager.ShowPanelStack<T>()` 转发到 `StackPanelModel.Open<T>()`**（`UIManager.cs:100-101`）。

## 关键设计

### 1. Model 形态选择

`StackPanelModel : BaseModule` —— 沿用 TCore 现有 `BaseModule` 体系（挂在 `BaseManager` 下、`FixedUpdate` 驱动 Tick），不另起 `BaseModel` 抽象类。

代价：`BaseModule.Tick(float deltaTime)` 不再需要（栈是被动事件驱动），保留默认空实现。

### 2. 外部关闭检测 — 订阅 `BaseUIPanel.Hidden` 事件

`BaseUIPanel` 新增 `event Action<BaseUIPanel> Hidden`，在 `OnHideComplete` 末尾广播（`BaseUIPanel.cs:42, 199`）。

Model 订阅此事件：

- 顶层被外部关闭 → `Hidden` 触发 → Model 移除栈项 → `RestoreStackTop` 自动 Show 上一层
- 顶层被 Model 自身 `Back()` 关闭 → 用 `_suppressHiddenHandler` 标志位抑制（`try/finally` 包裹），避免双重恢复

### 3. 层级守门 — Panel 序列化 `Layer` 字段

`BaseUIPanel` 加 `[SerializeField] UILayer _layer` + `internal void SetLayer(UILayer)` 写入接口。

`UIManager.LoadPanel` → `UILoaderModule.Load` 在 `Instantiate` 后、`Init` 前写入 `panel.SetLayer(config.layer)`，保证运行时值与注册配置一致。

Model.Open 校验：`(int)newPanel.Layer >= (int)topPanel.Layer`，不满足 `LogError + return null`，栈不变。空栈放行（无"当前顶层"可比）。

### 4. Destroy 兜底

外部 `Destroy(panel.gameObject)` 不会触发 `Hidden` 事件（没走 Hide 动画）。Model 在 `RestoreStackTop` 巡检时发现 `Instance == null` 或 `gameObject == null` → `LogWarning` + 移除该条目并继续向上找。

### 5. 订阅管理

Model 维护 `Dictionary<BaseUIPanel, Action<BaseUIPanel>> _subs`，记录每条订阅以便 `Destroy` 时反注册，避免内存泄漏。

`UIManager.OnDestroy` 链路：遍历 `UILoaderModule.GetAllLoaded()` 销毁 GO + `ClearModule()`（逐个调 `module.Destroy()`），Model 在自己的 `Destroy` 里反注册所有订阅 + 清空栈。

### 6. 重复入栈检查

`Open` 入口先 `IsInStack(panelType)`，命中则 `LogWarning` 拒绝，避免同一面板出现两个栈项；这是层级守门之外的另一道兜底。

## 公开 API 速查

```csharp
// 查询
int StackDepth
Type GetStackTop()                                       // 栈顶面板类型,栈空返回 null
bool IsInStack<T>() / bool IsInStack(Type)               // 类型是否在栈中(任意位置)
bool IsStackTop<T>() / bool IsStackTop(Type)             // 类型是否在栈顶
IReadOnlyList<Type> GetStackSnapshot()                   // 栈快照(仅 Type 列表)

// 主操作
T Open<T>() / BaseUIPanel Open(Type)                     // 压栈 + 校验 Layer
bool Back()                                              // 弹栈(API 名是 Back 不是 CloseTop,旧文档有出入)
bool BackTo<T>() / bool BackTo(Type)                     // 弹到指定类型
bool PopToRoot()                                         // 弹到栈底
void ClearStack()                                        // 仅清空
void ClearStackAndHide()                                 // 清空 + Hide 所有
```

调用方：

```csharp
Game.Instance.GetManager<UIManager>().GetModule<StackPanelModel>().Open<MySubPanel>();
// 或
Game.Instance.GetManager<UIManager>().ShowPanelStack<MySubPanel>();
```

**API 命名小坑**：底层方法叫 `Back()`，但 `UIManager` 没有对应的 `BackPanelStack` 转发方法，业务方直接调 `GetModule<StackPanelModel>().Back()`。

## 事件

通过 `_ui.Call(...)` 转发到 UIManager 的事件总线：

| 事件 | 触发时机 | 字段 |
| --- | --- | --- |
| `PanelOpenedEvent` | `Open` 末尾 | `PanelName` |
| `PanelPushedEvent` | `Open` 末尾（栈专用） | `PanelName, StackDepth` |
| `PanelClosedEvent` | `Back` 中 Hide 后 | `PanelName` |
| `PanelPoppedEvent` | `Back` 末尾 + 外部关闭兜底 | `PanelName, StackDepth` |

`PanelPushedEvent` 与 `PanelOpenedEvent` 在 `Open` 末尾**同时广播**，业务方按需订阅。

## 与 UIManager 拆分重构（ea90105）的关联

`ea90105 refactor(TUI): UIManager 拆分为 4 个 BaseModule,薄壳转发零迁移` 把"加载/单实例显隐"从 `UIManager` 抽到 4 个 Module，**`StackPanelModel` 的接口基本不变**，但调用链变成：

```text
旧:业务方 → UIManager.PushPanel → UIManager._stack → 业务方
新:业务方 → UIManager.ShowPanelStack → StackPanelModel.Open → UILoaderModule.Load → UIVisibilityModule.Show
```

**对业务方零迁移**：`UIManager.ShowPanelStack<T>()` 转发到 `StackPanelModel.Open<T>()`，旧调用点不需要改。

## 与 Popup 修复（595240f）的边界

`StackPanelModel` 与 `PopupModule` 共享 `BaseUIPanel.Hidden` 事件，但**职责互不重叠**：

| 子系统 | Hidden 用途 | 动画重入处理 |
| --- | --- | --- |
| `StackPanelModel` | 外部关闭兜底 | `_suppressHiddenHandler` 标志位 |
| `PopupModule` | 隐藏事件广播 + 清 follow 上下文 | `shouldPlayShow` 判断复用已可见实例时不再重播 Show |

两者独立持有自己的 `Hidden` 事件订阅，互不干扰。

## 文件清单

| 操作 | 路径 |
| --- | --- |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/Model/StackPanelEntry.cs` |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/Model/StackPanelModel.cs` |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/BaseUIPanel.cs`（加 Hidden 事件 + Layer 序列化 + SetLayer 接口） |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/UIManager.cs`（栈转发 + OnDestroy 统一销毁） |
| 删除 | `Assets/Plugins/TGame/TUI/Runtime/UIPanelStackEntry.cs` |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/Sample/StackSamplePanel.cs` |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/Sample/StackSubPanel.cs` |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/Sample/TUISample.cs` |
| 修改 | `TUI.csproj`（注册新文件、删除旧文件引用） |

## 后续注意

- Unity Editor 首次 import 时，`Model/` 目录会自动生成 `.meta` 文件，无需手动创建。
- 旧 `UIPanelStackEntry` 已删除，如有外部代码引用，需要同步迁移到 `StackPanelEntry`。
- `BaseUIPanel.OnPushed/OnPopped` 参数类型由 `UIPanelStackEntry` 改为 `StackPanelEntry`，业务方重写时需同步。
- 层级守门只校验"新.Layer >= 顶.Layer"，不校验 Panel 实际父节点与 `_layerRoots[Layer]` 是否一致 —— 后续若需要可加，但当前实现已能挡住 90% 的"sibling 与 Layer 错位"使用错误。
- `StackPanelEntry.Instance` 可能为 `null`（被外部 Destroy 但栈条目未清理），`OnPushed/OnPopped` 钩子要先判空。
