# StackPanelModel 重构分析

## 概览

| 项目 | 值 |
|------|-----|
| 模块 | `TUI.Model.StackPanelModel` |
| 父级 | `UIManager`(作为 `BaseModule` 挂在它下面) |
| 文件 | `Assets/Plugins/TGame/TUI/Runtime/Model/StackPanelModel.cs` + `StackPanelEntry.cs` |
| 提交 | feat(TUI): 抽出 StackPanelModel,Panel 栈逻辑与 UIManager 解耦 |
| 前置 | 旧版 `_stack` 直接内嵌在 `UIManager` 中,提交 `db01bac` |

## 重构前的问题

`UIManager` 同时承担两类职责:
1. **加载/单实例显隐注册表** —— `_loadedPanels` 字典 + `LoadPanel`/`ShowPanel`/`HidePanel`/`UnloadPanel`
2. **栈式 Panel 管理** —— `_stack` 列表 + `PushPanel`/`PopPanel`/`BackTo`/`PopToRoot` 等 11 个方法

职责耦合导致:
- **关系缺失**:`PushPanel` 不接收 parent,新面板叠加在"当前栈顶"上,无法表达"在 A 上面打开 B"以外的语义。
- **层级守门缺失**:若 A 实际注册在 Layer2、要打开的 B 注册在 Layer1,旧代码照常打开,导致 sibling 顺序与 Layer 顺序矛盾。
- **外部关闭无兜底**:业务方直接调 `uimgr.HidePanel<StackSubPanel>()` 时,栈顶条目残留,需要业务方自己管理。
- **模块结构不一致**:`BaseUIPanel.OnPushed`/`OnPopped` 钩子和 `PanelPushedEvent`/`PanelPoppedEvent` 事件都"沾"在 UIManager 上,不符合"Model 自治"的设计。

## 重构后架构

```
UIManager : BaseManager
 ├─ RegisterPanel / LoadPanel / ShowPanel / HidePanel / UnloadPanel  (单实例注册表)
 └─ GetModule<StackPanelModel>()  (栈逻辑)
     └─ Open<T> / CloseTop / BackTo / PopToRoot
        ├─ 层级守门(新.Layer >= 顶.Layer)
        ├─ 订阅 Panel.Hidden 事件 → 外部关闭兜底
        └─ SetAsLastSibling → 栈顶渲染最上
```

UIManager 退回到"加载/单实例显隐"职责,栈逻辑完全由 Model 自治。

## 关键设计

### 1. Model 形态选择

`StackPanelModel : BaseModule` —— 沿用 TCore 现有 `BaseModule` 体系(挂在 `BaseManager` 下、`FixedUpdate` 驱动 Tick),不另起 `BaseModel` 抽象类。

代价:`BaseModule.Tick(float deltaTime)` 不再需要(栈是被动事件驱动),保留空实现。

### 2. 外部关闭检测 — 订阅 `BaseUIPanel.Hidden` 事件

`BaseUIPanel` 新增 `event Action<BaseUIPanel> Hidden`,在 `OnHideComplete` 末尾广播。

Model 订阅此事件:
- 顶层被外部关闭 → `Hidden` 触发 → Model 移除栈项 → `RestoreStackTop` 自动 Show 上一层
- 顶层被 Model 自身 CloseTop 关闭 → 用 `_suppressHiddenHandler` 标志位抑制(`try/finally` 包裹),避免双重恢复

### 3. 层级守门 — Panel 序列化 `Layer` 字段

`BaseUIPanel` 加 `[SerializeField] UILayer _layer` + `internal void SetLayer(UILayer)` 写入接口。
`UIManager.LoadPanel` 在 Instantiate 后、`Init` 前写入(从 `_configs[type].Layer` 取值),保证运行时值与注册配置一致。

Model.Open 校验:`(int)newPanel.Layer >= (int)topPanel.Layer`,不满足 LogError + return null,栈不变。
空栈放行(无"当前顶层"可比)。

### 4. Destroy 兜底

外部 `Destroy(panel.gameObject)` 不会触发 `Hidden` 事件(没走 Hide 动画)。Model 在 `RestoreStackTop` 巡检时发现 `Instance == null` 或 `gameObject == null` → 移除该条目并继续向上找。

### 5. 订阅管理

Model 维护 `Dictionary<BaseUIPanel, Action<BaseUIPanel>> _subs`,记录每条订阅以便 `Destroy` 时反注册,避免内存泄漏。

`UIManager.OnDestroy` 链路:`ClearModule()` 会逐个调 `module.Destroy()`,Model 在自己的 `Destroy` 里反注册所有订阅 + 清空栈。

## 公开 API 速查

```csharp
// 查询
model.StackDepth
model.GetStackTop()
model.IsInStack<T>() / IsInStack(Type)
model.IsStackTop<T>() / IsStackTop(Type)
model.GetStackSnapshot()  // IReadOnlyList<Type>

// 主操作
model.Open<T>() / Open(Type)        // 压栈 + 校验 Layer
model.CloseTop()                     // 弹栈
model.BackTo<T>() / BackTo(Type)     // 弹到指定类型
model.PopToRoot()                    // 弹到栈底
model.ClearStack()                   // 仅清空
model.ClearStackAndHide()            // 清空 + Hide 所有
```

调用方:
```csharp
Game.Instance.GetManager<UIManager>().GetModule<StackPanelModel>().Open<MySubPanel>();
```

## 事件

通过 `_ui.Call(...)` 转发到 UIManager 的事件总线:
- `PanelOpenedEvent` / `PanelClosedEvent` —— 通用显隐
- `PanelPushedEvent(name, depth)` / `PanelPoppedEvent(name, depth)` —— 栈专用,带新栈深度

## 文件清单

| 操作 | 路径 |
| --- | --- |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/Model/StackPanelEntry.cs` |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/Model/StackPanelModel.cs` |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/BaseUIPanel.cs` |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/UIManager.cs` |
| 删除 | `Assets/Plugins/TGame/TUI/Runtime/UIPanelStackEntry.cs` |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/Sample/StackSamplePanel.cs` |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/Sample/StackSubPanel.cs` |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/Sample/TUISample.cs` |
| 修改 | `TUI.csproj`(注册新文件、删除旧文件引用) |

## 后续注意

- Unity Editor 首次 import 时,`Model/` 目录会自动生成 `.meta` 文件,无需手动创建。
- 旧 `UIPanelStackEntry` 已删除,如有外部代码引用,需要同步迁移到 `StackPanelEntry`。
- `BaseUIPanel.OnPushed`/`OnPopped` 参数类型由 `UIPanelStackEntry` 改为 `StackPanelEntry`,业务方重写时需同步。
- 层级守门只校验"新.Layer >= 顶.Layer",不校验 Panel 实际父节点与 `_layerRoots[Layer]` 是否一致 —— 后续若需要可加,但当前实现已能挡住 90% 的"sibling 与 Layer 错位"使用错误。
