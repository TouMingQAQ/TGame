# TUI 框架现状分析与优化方向

> 最近更新：基于当前工作区代码（TUI/Runtime 全部源码 + TCore/Addressable 依赖源码）静态分析。
> 分析范围：`Assets/Plugins/TGame/TUI/Runtime/` 全部文件、`TCore` 的 `BaseManager/BaseModule/ModuleEntity/EventModule/Game`、`Addressable/Runtime/Model/AddressableModule.cs`。

## 一、框架现状（代码实际形态）

与 `CLAUDE.md` 和旧文档描述不同，TUI 已演进为 **UIManager(全局薄壳) + UIRoot(per-上下文根) + 模块化(ModuleEntity)** 的形态：

```text
Game (DontDestroyOnLoad)
 └─ UIManager : BaseManager (DefaultExecutionOrder -7900)
     ├─ UIRegistryModule          全局注册表 Type → Addressable address（资产身份）
     ├─ AddressableModule<BaseUIPanel>  共享句柄池（来自 TGame.Addressable）
     ├─ UIRootManagerModule       UIRoot 注册表（按运行时 Type 索引）
     └─ _uiRoot : UIManagerRoot (UIRoot, 场景预制体)
         └─ ModuleEntity (IModuleHost, Update 驱动 Tick)
             ├─ UILayerRootModule   7 层根 Transform（Background ~ Tooltip）
             ├─ UILoaderModule      加载 + 单实例缓存
             ├─ UIVisibilityModule  Show/Hide + SetAsLastSibling + 事件广播
             ├─ StackPanelModule    栈式导航（单栈 + Layer 守门 + Hidden 兜底）
             └─ PopupModule         浮窗（注册表 / 自动定位 / 翻转 / 鼠标跟随）
```

基类体系：`BaseUIPanel`（每面板独立 Canvas + CanvasGroup + DOTween Sequence 生命周期）/ `BaseUIPopup`（内容 RT 定位）/ `LoadingPanel`（纯抽象）/ `BaseMVVMPanel<T>`（struct 数据绑定）/ `TButton` 系列 / `TextMeshProScroller`。UI 事件为 `PanelOpenedEvent/PanelClosedEvent/PanelPushedEvent/PanelPoppedEvent/PopupShownEvent/PopupHiddenEvent` 六个 struct。

**优点（值得保留）**：模块职责切割干净、per-UIRoot 隔离 + 全局注册表共享；异步 UniTask API；Popup 的解算（popup 根本地坐标 + 4 候选翻转 + clamp）正确且 O(1)；`BaseUIPopup` 的 blocksRaycasts 非阻塞策略内置；外部关闭栈顶自动恢复的 Hidden 事件兜底。

## 二、问题与风险（按优先级）

### P0 正确性 / 资源泄漏

| # | 问题 | 位置 |
| --- | --- | --- |
| 1 | **面板实例与 Addressable 句柄生命周期缺失**：`UILoaderModule.Unload` 只从 `_loaded` 删字典，**不 Destroy 面板 GO、不归还句柄**；面板实例化后从未对 Assets 递增引用计数 | `UILoaderModule.cs:96-99` |
| 2 | **`AssetHandle.Release()` 语义错误**：实现是 `Object.Destroy(Prefab)`——销毁的是**共享缓存里的 prefab 资产实例**，多实例/多 UIRoot 共享时会把别人的 prefab 也销毁；应是 `Addressables.Release(handle)` + 引用计数 | `AddressableModule.cs:15-20` |
| 3 | **并发加载去重缺失（与注释不符）**：类注释宣称有 `_loading` 去重字典，实际 `LoadAsync` 只查 `_loaded`，两次并发 `LoadPanelAsync<T>` 会 Instantiate 两个实例 | `UILoaderModule.cs:52-65` |
| 4 | **Addressable 加载路径不一致**：`PreLoadAsync(AssetReference)` 走 `LoadAssetAsync<GameObject>` + `TryGetComponent<T>`，而 `LoadByKeyAsync` 走 `Addressables.LoadAssetAsync<BaseUIPanel>`（组件类型直接 Load）——两条路径对同一资源语义不同 | `AddressableModule.cs:39-50, 101-110` |
| 5 | **`LoadByKey(string, Action<T>)` 命中缓存没 return**：会二次加载 + 二次回调 | `AddressableModule.cs:91-100` |
| 6 | **async void 风险**：`UIRoot.Start()` 是 `async void`，内部 `await Initialize()` 异常直接崩；`AddressableModule.PreLoadAsync(labels/key)` 也是 async void 且为 TODO 空实现 | `UIRoot.cs:108-111`、`AddressableModule.cs:27-35` |
| 7 | **事件总线实例割裂**：`UIVisibilityModule/StackPanelModule/PopupModule` 都通过 `Host.GetModule<EventModule>()` 广播——Host 是 **UIRoot 的 ModuleEntity**，`GetModule` 会自动新建一个 **UIRoot 自己的 EventModule 实例**；若业务方订阅挂在 Game/UIManager 上的 EventModule，**收不到任何 TUI 打开/关闭事件** | `UIVisibilityModule.cs:45`、`StackPanelModule.cs:88-90`、`PopupModule.cs:296`、`EventModule` 按 host 隔离 |

### P1 一致性 / 架构

| # | 问题 | 位置 |
| --- | --- | --- |
| 8 | **Layer 双重来源不一致**：面板实际挂载层级根由调用方 layer 参数（默认 Normal）决定，而栈守门读的是 **prefab 序列化 `_layer` 字段**——两处可能矛盾（如 `ShowPanelAsync<X>(UILayer.Popup)` 挂到 Popup 根，但 prefab `Layer=Normal`） | `UILoaderModule.cs:60`、`StackPanelModule.cs:70-75` |
| 9 | **动画起始状态在 Awake 时被捕获**：`FadeIn/ScaleIn/SlideIn*` 在 `BuildShowAnimation`（Awake）里就把 transform/canvasGroup 改写并捕获起点；`Restart/SmoothRewind` 回到的是**创建时刻的状态**，第一次 Show 后再 Hide/Show，位置/alpha 可能跳回旧起点 | `UIAnimationMaker.cs` 各方法、`BaseUIPanel.cs:83-104` |
| 10 | **API 不一致**：`UIManager.ShowPanelAsync<T>()` 无 layer 参数、`UIRoot.ShowPanelAsync<T>(layer)` 有；`ShowPopup` 有 8 个重载缺乏参数对象收敛；栈入口与普通入口都广播 `PanelOpenedEvent` | `UIManager.cs`、`UIRoot.cs` |
| 11 | **注册失败静默 null**：未注册的 `LoadAsync` 返回 null，业务层拿到 null 无错误提示；`UIRegistryModule.IsRegistered<T>(Type type)` 的 `type` 参数未使用（签名缺陷） | `UILoaderModule.cs:57-58`、`UIRegistryModule.cs:56` |
| 12 | **无 CancellationToken**：所有异步加载不带 ct，UIRoot 中途销毁后 continuation 继续跑，仅靠 Unity 假 null 兜底 | 全链路 |
| 13 | **预热串行**：`UIRoot.Initialize` 对 `_preloadUI` 逐个 `await`，可 `UniTask.WhenAll` 并行；多 UIRoot 各自预热同一资源会重复 `Register`（打 warning） | `UIRoot.cs:94-101` |

### P2 工程化 / 性能 / 清理

| # | 问题 | 位置 |
| --- | --- | --- |
| 14 | **每帧分配**：`ModuleEntity.Update` 每帧 `new List<Type>(_moduleMap.Keys)`；`BaseManager.FixedUpdate` 则是 foreach Values——语义也不一致（模块在 Tick 内增删的行为不同） | `ModuleEntity.cs:71-82` |
| 15 | **事件非类型化**：TUI 事件只带 `PanelName` 字符串，订阅方要 switch 字符串，无法直接拿面板实例/类型 | `UIEvent.cs` |
| 16 | **两套数据通路并存**：MVVM（`BaseModel<T>` 仅 struct + 每次 OnEnable 全量刷新）与 `BaseUIPopup.SetData<TData>(class)` 并存，未明确取舍 | `MVVM/`、`BaseUIPopup.cs:35` |
| 17 | **死代码/空壳**：`TButtonAnimationTween.cs` 空 Start/Update；`UILoader.cs`/`UILoaderByReference.cs` 全是空实现（TODO）；`LoadingPanel` 是纯抽象而 UIManager 并没有文档宣称的 `ShowLoadingPanel` API | 对应文件 |
| 18 | **Popup 不走 Addressable**：`PopupModule._prefabs` 直接持 prefab 引用，与面板的 Addressable 策略不一致，popup 数量大时无法流式/按需加载 | `PopupModule.cs:50,65-79` |
| 19 | **层级绘制顺序是隐式不变量**：7 个 layer root 依赖预制体里的兄弟顺序决定绘制顺序，面板各自带 Canvas 但 `sortingOrder` 从未显式设置——应显式化（每层 Canvas + sortingOrder/overrideSorting）或加 Editor 校验 | `UIRoot.cs:40-48`、`BaseUIPanel` |
| 20 | **`TButtonHold` 在 Update 里对序列化字段写绝对值**（`startHoldTime = MathF.Abs(startHoldTime)`），运行期污染序列化值；`IsAutoHoldEnd` 双重 if | `TButtonHold.cs:66,86-92` |
| 21 | **既有分析文档过时**：`TUI-StackPanelModel.md`/`TUI-PopupModule.md` 描述的是旧架构（StackPanelModel 挂在 UIManager 下、其他模块也挂在 UIManager 下），当前代码已改为 **per-UIRoot ModuleEntity**，需刷新 | `Document/AIDoc/Analysis/` |
| 22 | **无测试**：`PopupLayoutHelper.Solve` 是纯函数（翻转 + clamp）却无单测；栈状态机、并发加载去重均无测试 | 全模块 |

## 三、可优化方向（建议路线图）

### 短期止血（正确性）
1. 修 Addressable 生命周期：持有 `AsyncOperationHandle` + 引用计数，`Unload` = Destroy 实例 + 计数减一（=0 时 `Addressables.Release`）；`AssetHandle.Release` 重写为真正的释放语义。
2. `UILoaderModule` 补并发去重（`_loading: Dictionary<Type, UniTask<BaseUIPanel>>` 共享同一次加载），与类注释一致。
3. 统一 Addressable 加载路径（一律 `LoadAssetAsync<GameObject>` + `GetComponent<T>`）；`LoadByKey` 命中分支补 `return`。
4. 消灭 async void：`UIRoot.Start` 改显式 `UniTask` + `Forget`，`UIRoot.Initialize` 支持传入 `CancellationToken`（`GetCancellationTokenOnDestroy`）。
5. 统一事件总线实例：要么 UIManager 持有全局 EventModule 并注入各 UIRoot 模块，要么文档明确"订阅 UIRoot 自己的 EventModule"。
6. 修 `IsRegistered<T>(Type)` 签名；`LoadAsync` 未注册时给 LogError 而非静默 null。

### 中期（一致性 / 架构）
7. Layer 一致性：加载时以 layer 参数为准写回 `panel.Layer`（或反之），并加 Editor/运行时校验"实际父节点 == LayerRoot[Layer]"。
8. API 收敛：`ShowPopup` 收敛为 `PopupOptions` 参数对象；UIManager/UIRoot 转发层保持单一事实源。
9. 动画起点改为"每次 Show 前捕获"（`BuildShowAnimation` 重建起点或 Show 前 refresh），消除第二次 Show 跳回旧状态的问题。
10. 层级显式化：每层给独立 Canvas + 显式 sortingOrder（或 overrideSorting），加 Editor 校验器检查层顺序。
11. 取消与场景生命周期：异步全链路补 ct；UIRoot 增加"场景切换卸载策略"钩子。
12. Popup 统一走 Addressable 注册，支持按需加载。

### 长期（工程化）
13. 事件类型化：`PanelOpenedEvent<T>`/携带实例引用，去掉字符串 switch。
14. MVVM 明确取舍：加 class 数据 + 属性级通知 + 帧批处理，或移除当前极简 MVVM 缩减维护面。
15. 性能审计：`ModuleEntity.Update` 无分配化；`SetAsLastSibling` 高频调用收敛；`TextMeshProScroller` 的 ExecuteAlways 收敛。
16. 测试基建：`PopupLayoutHelper` 纯函数单测（EditMode Test Runner）、栈不变量测试、并发加载去重测试。
17. 文档刷新 + 补齐缺口 API（LoadingPanel 管理入口）。

## 四、与旧分析文档的差异提醒

- 旧文档称 `StackPanelModel` 挂在 `UIManager` 下——当前实际挂在 **UIRoot 的 ModuleEntity** 下（`StackPanelModule`，文件名/类名已改）。
- 旧文档称 `UILayerRootModule` 由 UIManager.Awake 填表——当前由 **UIRoot.Initialize** 填表（7 层）。
- 旧文档称 Popup 的 Tick 由 `UIManager.Update` 驱动——当前由 **ModuleEntity.Update（unscaledDeltaTime）** 驱动。
- 统一以本文件为准。