# Gamepad UI 接入说明

## 1. 模块边界与文件

代码位于 `UnityProject/Assets/Plugins/TGame/Gamepad/Runtime/`，命名空间为 `TGame.Gamepad`，程序集为 `TGame.Gamepad`。仅引用 Unity Input System、uGUI 及引擎基础 API，不引用 TUI、TCore、第三方动画或列表插件。

运行时组件：

| 组件 | 作用 |
| --- | --- |
| GamepadUI | 输入、有效聚焦路径、界面栈与分发 |
| GamepadFocus | 区域导航、父子 Tab、选择恢复 |
| GamepadItem | 导航目标、状态、光标锚点、选中变化 |
| GamepadAction | 单个逻辑操作的按下、重复、抬起、取消事件 |
| GamepadCursor | 区域共享光标，持续跟随目标位置 |
| GamepadList | 逻辑条目导航、异步显示、复用绑定 |

`GamepadInput.cs` 定义输入结构、操作名、列表结果和 `IGamepadList` 接口。模块内没有具体 ScrollRect 实现，也没有具体无限列表插件适配器。

## 2. 最小接入

1. 创建一个对象，挂载 GamepadUI，每次只启用一个管理器。
2. 在界面根节点挂载 GamepadFocus，指定为管理器的 Initial Focus。也可在界面打开时调用 `Push(focus)`。
3. 在原有按钮或开关上附加 GamepadItem，不替换现有控件。Item 默认从父级查找 Focus，也可显式指定。
4. 添加 GamepadAction，指定关联 Item，Operation 使用 `Submit`。
5. 将 Action 的 On Pressed 事件绑定到原控件公开的点击或开关方法。一个操作只接一种触发路径，避免重复点击。
6. 按需要配置默认目标和光标。

GamepadItem 的 Selectable 引用可用于同步原生控件的可用状态；Reset 时自动获取同对象 Selectable。自定义控件可以调用 `SetNavigable`、`SetInteractable` 同步状态。

GamepadItem 不会自动点击控件，也不调用原生 Selectable 导航。鼠标逻辑由原控件继续负责。

### 2.1 自动绑定器（简单单层 UI）

`GamepadAutoBinder` 是可选的接入辅助组件，用于只有一个 UI 层级的简单项目，给已有 uGUI 层级一次性补上手柄操作。把它放在 Canvas 下（或显式指定 Target Canvas）即可：自动创建或复用 GamepadUI、创建一个覆盖该 Canvas 的 GamepadFocus、按需创建共享 GamepadCursor，并周期扫描 Canvas 下已激活的 `Selectable` 增量挂载 GamepadItem / GamepadAction。它只增补组件，不替换原控件。

### 2.2 每个面板一个 GamepadFocus（推荐）

多面板/弹窗场景应为每个面板建立独立聚焦目标。`GamepadPanel` 挂在面板根节点上：

- 自动在面板根添加 `GamepadFocus`，并把面板内 `Selectable` 挂上 `GamepadItem`/`GamepadAction`，这些控件天然归属本面板的聚焦区。
- `ApplyFocus()`：显式让本面板成为当前聚焦目标（内部 `GamepadUI.Push`），下层面板自动退居其后。
- `RemoveFocus()`：显式取消本面板聚焦（内部 `GamepadUI.Remove`），自动回退到下层面板并恢复其上次选中项。
- 面板内的控件只属于本面板聚焦区；被覆盖的面板控件不会参与导航，因此不会出现旧面板按钮仍可聚焦的问题。

> 关键点：面板显示/隐藏不一定靠 `GameObject.SetActive`，所以不要让聚焦逻辑依赖激活状态。请在面板显示与关闭的代码路径里**主动调用** `ApplyFocus()` / `RemoveFocus()`。

示例 `Assets/Plugins/TGame/Sample/Gamepad/SampleGamepadBridge.cs` 通过 TUI 事件显式驱动：`PanelOpenedEvent` → `ApplyFocus()`；`PanelClosedEvent` 与 `BaseUIPanel.Hidden`（覆盖直接调用 `panel.Hide()`）→ `RemoveFocus()`。它不读取 active 状态来推断显隐。控制台/屏幕会输出 `[SampleGamepadBridge] 聚焦面板: XXX` 便于确认。

如果一个界面内部还有 Tab 子区域或虚拟列表，继续在面板的 `GamepadFocus` 下配置子 `GamepadFocus` / `GamepadList`，见后续章节。

### 2.3 手柄选中时显示固定提示文本

鼠标悬停用 `DefaultToolTipTrigger` 弹浮窗；手柄选中时通常不希望跟随鼠标弹窗，而是把同一段提示文本写到界面固定的文本位置。使用 `GamepadTooltipHint`：

- 它监听当前 `GamepadUI.SelectedItem`，当选中目标属于本面板时，读取该目标上 `DefaultToolTipTrigger.TooltipText`，写入固定 `TMP_Text`。
- 目标离开/取消选中时清空文本；鼠标悬停的浮窗逻辑不受影响。
- `Target` 为空且开启 `Auto Create` 时，会在面板底部自动创建一个 TMP 文本作为固定提示位。
- 示例桥接会给每个面板自动挂 `GamepadTooltipHint`；若要使用面板内已有的固定文本，给面板预制体手动加 `GamepadTooltipHint` 并指定 `Target`，桥接检测到已存在就不会重复添加。

## 3. 输入配置

未指定 Input Actions 时，管理器创建以下运行时映射：

| 操作名 | 默认输入 |
| --- | --- |
| Navigate | 左摇杆、十字键 |
| Submit | 南侧按钮，Xbox A |
| Cancel | 东侧按钮，Xbox B |
| Previous | 左肩键 |
| Next | 右肩键 |

指定 Input Actions 后，管理器克隆该资源，只启用 Action Map 指定的动作表，不启停原资源。自定义动作表需包含 Vector2 类型的 `Navigate` 和所需 Button 类型动作。其他命名的 Button 动作也会分发，GamepadAction 的 Operation 填同名即可。

当前直接输入采集覆盖方向和 Button 操作。额外 Vector2 等连续输入可由外部代码调用 `Dispatch(new GamepadInput(...))` 接入；尚未内置自由指针和通用滚动轴驱动。

管理器使用非缩放时间处理方向重复。GamepadAction 可读取 `LastInput.Value`；例如监听 Navigate 并在按下或重复时按方向修改滑条。该操作消费输入后，区域不会继续切换目标。

默认在聚焦栈非空时暂时关闭当前 EventSystem 的 Send Navigation Events，禁用管理器或清空栈时恢复原值，以避免原生移动、Submit、Cancel 重复执行；鼠标仍走现有指针事件。若外部系统也管理这个开关，应关闭模块的 Suppress Event System Navigation，由外部统一协调。

## 4. 交互与取消

一个 GamepadAction 对应一个逻辑操作，可在同一 Item 上挂多个 Action。Priority 越大越先处理，Consume 决定是否停止传播；相同优先级不应依赖隐含顺序。

- On Pressed：开始交互，例如切换开关。
- On Repeated：方向持续按住的重复操作，需要本组件已收到对应按下。
- On Released：释放最初接收按下的组件。
- On Canceled：失焦、组件禁用、手柄断开等引起的中止。

需要持续操作的业务应同时绑定 Released 和 Canceled 的清理逻辑。取消不会依赖目标仍有输入资格。

分发顺序：当前目标 Action → 当前列表 → 当前 Focus → 父 Focus。回调导致选择或聚焦改变后，本次输入停止向新界面传播。

## 5. Tab 与弹窗

为界面 A、Tab1、Tab2 分别配置 GamepadFocus。A 的 Children 数组指定 Tab 顺序，Tab 的 Parent 显式引用 A，或使用默认 Transform 父级查找。

A 的 On Child Requested 事件传入目标 Focus。外部脚本据此显示目标 Tab 内容并隐藏其他 Tab，再由模块切换焦点。该事件会在检查子页是否启用之前执行，因此可以启用隐藏页。

顶层界面调用 `Push(focus)`，关闭栈顶调用 `Pop()`。切换当前界面的子区域调用 `Focus(child)`。Pop 恢复下层界面最后选中的 Tab 和条目，不负责实际显示、隐藏或关闭界面。

On Entered、On Exited 表示区域进入或退出有效聚焦路径；共同祖先在切换 Tab 时保持进入状态。On Cancel 或 C# CancelRequested 用于返回请求；Consume Cancel 控制是否在该层停止传播。绑定关闭事件时应启用消费。

## 6. 导航与光标

上下左右显式邻居优先于区域的自动方向导航，自动导航按照目标屏幕位置计算。默认限制在当前 Focus 内，可配置区域边界循环；Tab 子区域使用独立选择记忆。

区域配置 GamepadCursor 后，共享光标跟随选中目标的 Cursor Anchor。Cursor Offset 使用该锚点的局部坐标，默认定位锚点 pivot。光标使用非缩放时间更新，并持续跟随滚动与布局变化。

Visual 应是 Canvas 内的独立装饰对象，并有 RectTransform 父节点。组件通过 Visual 上的 CanvasGroup 控制显示，不停用组件自身；不要把整个界面根节点当作 Visual。可配置平滑速度和切换目标时是否立即定位。不同 Canvas 分别使用各自的相机，跨显示器情况未验证。

不配置共享光标时，用 Item 的 On Selected、On Deselected 控制本控件的边框或其他表现。两种方式也可以组合。

本次实现共享光标跟随模式；摇杆自由指针模式尚未实现。

## 7. 无限列表适配

### 7.1 配置

列表根节点挂载 GamepadList，指定 Focus，并将其设为 Focus 的 Default List。第三方适配组件放在模块外部，实现 IGamepadList，然后通过 Inspector 的 Adapter 引用或 `SetAdapter` 传入。

同一 Focus 可包含多个列表；导航到另一个列表条目时切换 CurrentList。每个列表自己的逻辑选择独立保存。普通界面对象被选中时停止原列表请求。

### 7.2 适配接口契约

| 成员 | 契约 |
| --- | --- |
| Changed | 数据、顺序、有效性或视图绑定发生变化时通知；必须在 Unity 主线程触发 |
| IsValid(key) | 当前逻辑数据中是否存在该可选条目，与视图是否生成无关 |
| TryGetInitial | 返回默认条目的稳定标识；列表为空时返回 false |
| TryGetItem(key) | 返回当前代表该条目的视图，没有生成时返回 false |
| FindNeighbor | 查找逻辑相邻条目；支持异步分页，完成时返回 Found / Boundary / Failed |
| Show | 请求将条目显示到视口，完成时回调成功或失败；负责滚动、布局和视图生成 |

key 是非空字符串，应使用物品 ID 或其他稳定身份。不要用对象实例 ID；数据可插入、删除或排序时，也不宜用数组下标当稳定身份。

FindNeighbor 在主线程接收方向和当前 key，由适配器决定一维或网格规则；无论屏幕是否已有对象，都要根据逻辑数据查找。Boundary 表示确定的数据边界，网络失败等情况返回 Failed，不能伪装成边界。

Show 接收 Visible、Margin 或 Center 策略；当前 GamepadList 使用 Margin。边缘留白距离、缓动、变高布局计算由适配器自身配置和实现。接口不暴露 ScrollRect 或插件内部类型。

### 7.3 对象池绑定顺序

生成或复用条目时：

```csharp
// In the external adapter, after assigning the new data identity:
views[key] = item;
item.Bind(gamepadList, key);
Changed?.Invoke();
```

回收条目时，应先移除旧映射，再解除绑定，然后修改数据或回收到对象池：

```csharp
views.Remove(oldKey);
item.Unbind();
Changed?.Invoke();
```

TryGetItem 返回的对象必须同时满足 List 指向当前列表、ListKey 等于请求 key、Focus 匹配及对象处于有效状态。错误映射不会恢复成有效交互目标。对象曾作为列表视图绑定后，Unbind 会使其保持不可导航，直到再次 Bind，避免对象池中的闲置视图参与普通导航。

### 7.4 异步与取消

FindNeighbor、Show 以及全部回调都必须在 Unity 主线程调用。异步加载完成后先切回主线程。

每次请求接收 CancellationToken。适配器必须在执行滚动、布局或改写视图前检查取消，并停止旧动画或任务；核心中的版本校验只能阻止旧回调提交选择，不能替第三方插件撤销旧滚动副作用。

正常请求只完成一次，失败也应回调；取消后可直接结束。列表等待期间消费导航并暂停目标确认，不累计无界导航请求。显示完成后验证逻辑身份与实际对象，未生成目标则等待绑定变化通知。

若相邻查找同步返回 Boundary，可在同一次方向输入向外冒泡。若异步返回 Boundary，该次输入已消费；下一次相同方向输入才按 Bubble At Boundary 配置向外冒泡。Failed 不向外冒泡。

禁用列表、切换区域、切换数据源会取消请求；SelectedKey 保留用于恢复。数据删除或重排后触发 Changed，失效选择会重新选择有效初始项。

## 8. 验证与限制

模块编译命令，在 UnityProject 目录执行：

```powershell
dotnet build TGame.Gamepad.csproj -nologo
```

本地快速编译项目引用已安装的 Unity 6000.3.8f1 程序集，Unity 重新生成项目文件后以生成版本为准。模块实际依赖由 Runtime 下的程序集定义控制。`TGame.Gamepad.Tests.csproj` 可独立编译测试代码，但不能代替 Unity Test Runner 执行测试。

本次运行时和测试程序集均已编译通过，分别为 0 警告、0 错误。10 项 EditMode 测试尚未在 Unity 中执行；本项目没有连接中的 Unity Editor。

EditMode 测试位于模块 Tests/Editor 下，通过 Unity Test Runner 运行，覆盖聚焦恢复、交互取消、回收绑定和异步请求等关键行为。实际执行情况以本次交付说明为准；编译检查不替代 Unity 生命周期、Input System 运行和 Canvas 渲染验证。

尚未提供具体列表插件适配器，因为当前需求未指定插件及其版本。接入具体插件时应验证变高条目、快速滚动、条目复用、分页失败和关闭界面期间完成的加载请求。
