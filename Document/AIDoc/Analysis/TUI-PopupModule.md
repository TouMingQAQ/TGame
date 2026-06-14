# PopupModule 设计文档

> 最近更新：2026-06-14，基于 master 分支 HEAD（1eb5fca）。
> 演进时间线：feat 初次实现 → ea90105（UIManager 拆分为 Module，Popup 作为独立 Module）→ 595240f（**fix(TUI): 修复 Popup 坐标与动画重入**——本版本的关键修复）。
> 配套文档：[TUI-StackPanelModel.md](TUI-StackPanelModel.md) 共享 Hidden 事件但职责分离。

## 概览

| 项目 | 值 |
| --- | --- |
| 模块 | `TGame.TUI.PopupModule` + `BaseUIPopup` + `DefaultToolTip` + `UIConfig` |
| 父级 | `UIManager`（作为 `BaseModule` 挂在它下面）；`UIConfig` 作为 ScriptableObject 资产被 UIManager 引用 |
| 文件 | `Runtime/BaseUIPopup.cs` + `DefaultToolTip.cs` + `UIConfig.cs` + `Model/PopupModule.cs` + `Model/PopupLayoutHelper.cs` |
| 命名空间 | `TGame.TUI` |
| 提交 | 首次实现 → ea90105 → 595240f |
| 前置 | UIManager 拆分后的 4 个 Module（Registry/LayerRoot/Loader/Visibility）+ StackPanelModel |

## 背景与目标

**问题：** TUI 子系统早期只有"全屏/半屏面板"语义（UILayer 6 层 + StackPanelModel 栈式管理），缺少"鼠标悬停显示、自动定位、避免越界"的小型浮窗能力。Tooltip/气泡/ContextMenu/HelpHint 这类 UI 在游戏里非常常见，但每个业务方都要重复实现：监听 PointerEnter/Exit、计算屏幕坐标、检测越界、写翻转逻辑。

**目标：** 在 UIManager 下新增一个**浮窗子系统**，提供：

1. 浮窗基类 `BaseUIPopup` + 默认实现 `DefaultToolTip`（只显示文本）
2. `PopupModule` 负责生命周期、自动定位、边界翻转、单实例语义
3. 新增 UILayer `Tooltip = 600`，**独占一层，始终最顶**（在 Modal 之上）
4. 翻转策略：默认放鼠标右下，越界时按"下→上、右→左"优先级自动翻到另一侧
5. 范围边界可指定：默认 = 整个屏幕，也可指定任意 `RectTransform`（适配子区域）
6. 泛型 API：`ShowPopup<T>(screenPos, setup?)` / `HidePopup<T>()` / `RegisterPopup<T>(prefab)`
7. **`UIConfig` ScriptableObject**：装载"非场景"配置（默认浮窗 prefab + 全局浮窗参数），UIManager.Awake 自动读取并注册，业务方零样板
8. 触发器**不内置** MonoBehaviour，业务方自己接 `IPointerEnterHandler`

## 新架构

```text
UIManager : BaseManager
 ├─ UIRegistryModule / UILayerRootModule / UILoaderModule / UIVisibilityModule
 ├─ StackPanelModel                                              (栈逻辑)
 └─ PopupModule                                                  (浮窗)
     ├─ Register<T>(prefab)          → _prefabs[type] = prefab.gameObject
     ├─ Show<T>(pos, setup, ...)     → 单实例 + Anchor + SetAsLastSibling + Show + Call(PopupShownEvent)
     ├─ Hide<T>()                    → BaseUIPopup.Hide()，动画完成后 Hidden 事件回调清 _active
     ├─ HideAll()                    → 遍历 _active 逐个 Hide
     ├─ IsVisible<T>()
     └─ Tick(dt)                     → 跟随鼠标:每帧拉 Input 重定位
        ↓ 委托
        PopupLayoutHelper.Solve(screenAnchor|target, size, popupRoot, area, flip, offset)
            → (pos, pivot) — 在 popup 根本地坐标中解算,兼容 CanvasScaler / Camera Canvas

UIConfig : ScriptableObject  (Create → TGame/UI/UI Config)
 ├─ [SerializeField] DefaultToolTip _defaultTooltip
 ├─ [SerializeField] float _tooltipOffset = 8
 └─ [SerializeField] PopupFlipDirection _tooltipFlipDirection = BottomRight
        ↓ UIManager.Awake 自动读取并 RegisterPopup(_config.DefaultTooltip)

BaseUIPopup : BaseUIPanel
 ├─ [SerializeField] RectTransform _content    ← Module 改它的 pivot + anchoredPosition
 ├─ [SerializeField] float _defaultOffset
 ├─ abstract SetData<TData>(data)              ← 子类实现,setup 回调里调用
 └─ virtual Anchor(pos|target, popupRoot, area, flip, offset)  ← 调 PopupLayoutHelper.Solve
```

## 关键设计

### 1. UIConfig 零样板注册（非场景配置统一收口）

`UIConfig` 是 ScriptableObject，装载"非场景"配置（默认浮窗 prefab + 全局浮窗参数）。**不**装场景引用（layer root、canvas 等）—— 那些仍由 `UIManager.prefab` 自身 SerializeField 管理，这是"配置资产"和"场景预制体"的明确边界。

`UIManager` 新增 `[SerializeField] UIConfig _config`，`Awake` 在填 TooltipRoot 之后读取 config 并自动 `RegisterPopup(_config.DefaultTooltip)`。业务方只需在 `UIManager.prefab` 上挂一次配置资产，**TUISample 等场景代码完全不用写 `RegisterPopup(defaultTooltip)`**。

`UIConfig` 同时暴露：

- `TooltipOffset`（默认 8 px）→ `UIManager.PopupOffset` 转发
- `TooltipFlipDirection`（默认 BottomRight）→ 业务方在 `ShowPopup` 不传 flip 时仍走这个默认值

设计优势：

- **零样板**：`UIManager.prefab` 配置好后，业务方直接 `ShowPopup<DefaultToolTip>(pos, p => p.SetText("Hi"))` 即可，无需先注册。
- **可热改**：`UIConfig` 是独立资产，改 `defaultTooltip` prefab / `tooltipOffset` 不需要修改 `UIManager.prefab` 引用链。
- **可扩展**：后续可加 `tooltipAnimationCurve`、`tooltipBackgroundSprite` 等，统一收纳。

### 2. 浮窗走独立 Layer（Tooltip = 600）

`UILayer` 新增 `Tooltip = 600`，值比 `Top(500)` 更大，确保**浮窗永远渲染在最顶**，即使在 Modal 弹窗之上仍可见。这符合典型 Tooltip 语义——"确认按钮的说明"在弹窗上仍能透出。

UIManager 新增 `[SerializeField] Transform _tooltipRoot`，`Awake` 填表到 `UILayerRootModule`，`Start` 注入 self 到 `PopupModule`。

### 3. PopupModule 独立加载（不走 UIRegistry/Loader/Visibility）

Popup 不走 `UIRegistryModule + UILoaderModule + UIVisibilityModule` 三件套，原因：

- **layer 强制 Tooltip**：`UIRegistryModule.Register(prefab, layer)` 需要业务方传 layer，但 Popup 永远挂 Tooltip，多余参数污染注册表语义。
- **Show 时序不同**：UIVisibilityModule 是"先 Load 再 Show"，Popup 需要"先 Anchor 再 Show"，否则动画第一帧位置会跳变。
- **单实例 + Hidden 订阅**：PopupModule 用自己的 `_active : Dictionary<Type, BaseUIPopup>` 管单实例 + `Hidden` 事件清字典，与 `UILoaderModule._loaded` 缓存分离更清晰。
- **独立 Prefab 路径**：`_prefabs : Dictionary<Type, GameObject>` 不污染 `_configs` 的 `(prefab, layer)` 二元组语义。

### 4. 内容 RectTransform 模式（避开 UILoaderModule 的 RT 强制重置）

`UILoaderModule.Load` 会强制重置 panel RT 为填充父节点：

```csharp
rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
```

PopupModule 不走 UILoaderModule，但 Popup 实例化后仍由 `SetupPopupRoot(rootRt)` 强制：

```csharp
rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;
rootRt.pivot = Vector2.zero;
rootRt.localScale = Vector3.one;
```

**Module 改的永远是 `_content` 子节点的 pivot + anchoredPosition**，不是 panel 自身 RT。

Prefab 内部结构：

```text
[PanelRoot]  (BaseUIPopup/DefaultToolTip/TooltipPanel + CanvasGroup + RectTransform)
│           anchorMin=0,0  anchorMax=1,1  offsetMin=0,0  offsetMax=0,0  pivot=0,0  scale=1,1,1
│           ★ _content SerializeField 指向下一个节点
│
└── [Content] (RectTransform)
            anchorMin=0,0  anchorMax=0,0  pivot=0.5,0.5  sizeDelta=(200,80)
    ├── [Background] (Image)
    └── [Label/Title/Body] (TMP_Text)
```

### 5. 翻转算法（PopupLayoutHelper.Solve）

**输入**：

- `screenAnchor: Vector2` **或** `target: RectTransform`（互为重载）
- `contentSize: Vector2`（浮窗 sizeDelta，layout 失败时回退设计尺寸）
- `popupRoot: RectTransform`（用于屏幕点 → 本地坐标的转换）
- `boundsArea: RectTransform`（`null` = popupRoot 范围）
- `preferred: PopupFlipDirection`（调用方偏好方向）
- `offset: Vector2`（像素间距）

**输出**：`(Vector2 anchoredPos, Vector2 pivot)` — 写到 `_content` 上。

**步骤**：

1. **确定 areaRect 屏幕坐标**：`boundsArea == null` → `(0, 0, popupRoot.rect.width, popupRoot.rect.height)`；非空 → `GetWorldCorners` 算 4 角转 popup 根本地坐标。
2. **4 个候选方向**：

   - `(0,1) → BottomRight` / `(1,1) → BottomLeft` / `(0,0) → TopRight` / `(1,0) → TopLeft`
   - 按 `preferred` 优先排序（O(1) 冒泡一次）
3. **每个候选**：

   - `pivotPos = targetRect.corner + directionOffset`（按 `dir` 选 `xMax/xMin` 和 `yMin/yMax`）
   - `bl = pivotPos - pivot * size` / `tr = bl + size`
   - 完全在 `areaRect` 内 → 选这个，跳出
4. **全部越界**：选 `overlap = max(0, min(tr, areaMax) - max(bl, areaMin)).x * .y` 最大的
5. **ClampPivotPosition**：用 `Mathf.Clamp(bl, areaMin, areaMax - size)` 收口，防止"选中的候选边缘有 1 像素越界"
6. **计算最终 anchoredPos**：

   - `screenAnchor`：先 `ScreenPointToLocalPointInRectangle(popupRoot, screen, canvasCam, out local)` 转 popup 根本地，再 `local - popupRoot.rect.min`
   - `target`：取 4 角 → 同样转到 popup 根本地坐标

**复杂度**：O(1)（4 个固定候选），无迭代。

**修复要点（595240f）**：早期版本直接以"屏幕坐标"作为解算空间，在 CanvasScaler 缩放或 Camera Canvas 下会偏离鼠标位置。本版统一在 `popupRoot` 根本地坐标中解算，`ScreenPointToLocalPointInRectangle` 自动应用 `Canvas.scaleFactor` + `worldCamera` 投影，确保 1 像素 = 1 像素。

### 6. Anchor 时序：必须在 Show() 之前

`BaseUIPanel.Show()` 触发入场动画，动画开始后改 anchoredPosition 会导致第一帧位置跳变。`PopupModule.Show` 严格按顺序：

```text
1. setup?.Invoke(popup)        // 写入数据
2. popup.Anchor(...)           // 计算并应用最终位置
3. popup.transform.SetAsLastSibling()
4. popup.ApplyPopupRaycastPolicy()  // blocksRaycasts = false
5. if (shouldPlayShow) popup.Show()  // 触发动画(仅当需要)
6. if (shouldPlayShow) _ui.Call(new PopupShownEvent(...))
7. if (followMouse) _follow[type] = ctx; else _follow.Remove(type)
```

`Anchor` 内部先 `LayoutRebuilder.ForceRebuildLayoutImmediate(_content)` 刷新尺寸（让 TMP/ContentSizeFitter 后的真实 size 生效），失败时回退到 `sizeDelta`。

### 7. 动画重入修复（595240f 关键修复）

**早期问题**：浮窗已可见时再次 `ShowPopup<T>(pos, setup)` 会无条件重播入场动画，导致 Tooltip 在每次鼠标移动时"抖一下"。

**修复**：`PopupModule.Show` 引入 `shouldPlayShow` 守卫：

```csharp
bool shouldPlayShow = !popup.IsVisible || popup.IsHiding;
...
if (shouldPlayShow) {
    popup.Show();
    _ui.Call(new PopupShownEvent(type.Name));
}
```

`BaseUIPanel.IsVisible = gameObject.activeSelf`，`IsHiding = _animState == AnimState.Hiding`（基于新加的 `AnimState` 枚举）。**已可见且非正在隐藏** → 复用位置，只走 `setup + Anchor + SetAsLastSibling + ApplyPopupRaycastPolicy`，不重播 Show 动画、不重广播 PopupShownEvent。

配套修复：

- 命中已可见的 popup 时**仍重新 Anchor**（让位置跟随最新的 `screenAnchor`）
- Hit 时不调 `SetupPopupRoot`（已在首次创建时设好，重复设会触发 layout 重算）

### 8. 鼠标跟随（followMouse）功能

`PopupModule` 持有 `Dictionary<Type, FollowContext> _follow`，`UIManager.Update` 拉 `Time.unscaledDeltaTime` 调 `PopupModule.Tick(dt)`：

```csharp
public override void Tick(float deltaTime) {
    if (_follow.Count == 0) return;
    Vector2 mouse = GetMousePos();   // 兼容新/旧 Input System
    foreach (var kv in _follow) {
        var ctx = kv.Value;
        if (_active.TryGetValue(ctx.Type, out var popup) && popup != null && popup.IsVisible) {
            popup.SetPosition(mouse, ctx.PopupRoot, ctx.BoundsArea, ctx.Flip, ctx.Offset);
        } else {
            deadTypes.Add(ctx.Type);  // 实例销毁/隐藏 → 清 _follow
        }
    }
}
```

`SetPosition`（`BaseUIPopup` internal）是 `Anchor` 的"轻量版"——不刷新 layout、不改 transform、不动 SetAsLastSibling，只重写 `_content.anchoredPosition + pivot`，避免每帧 layout rebuild 抖动。

`UIManager.Update` 用 `Time.unscaledDeltaTime` 而非 `Time.deltaTime`，**保证 timeScale = 0（如暂停菜单）时 Tooltip 仍跟随鼠标**。

**Input System 兼容**：

```csharp
private static Vector2 GetMousePos() {
#if ENABLE_INPUT_SYSTEM
    return UnityEngine.InputSystem.Pointer.current?.position.ReadValue() ?? Vector2.zero;
#else
    return Input.mousePosition;
#endif
}
```

### 9. 贴指定 RectTransform（target 重载）

除 `ShowPopup<T>(Vector2 screenAnchor, ...)` 外，2026 新增 `ShowPopup<T>(RectTransform target, ...)` 重载：

- 把 `target` 的 4 个 world corner 转 popup 根本地坐标，得到 `targetRect`
- 翻转时按 `dir` 选 `targetRect.xMax/xMin` 和 `yMax/yMin` 作为浮窗贴近角
- `followMouse` 不适用于此重载（自动清 `_follow`）

适合 "右键点击 GameObject 弹出 ContextMenu"、"按钮按下弹出下拉框" 这类"贴 UI 元素而非鼠标"场景。

### 10. 同 Type 单实例 + Hidden 事件回调清理

`_active : Dictionary<Type, BaseUIPopup>` — 同 Type 同时只显示一个。

Show 时：

- 命中且实例未销毁 → 复用 + 重新 setup + 重新 Anchor（**不重播 Show 动画**，见上节）
- 未命中或死引用 → Instantiate 到 `_tooltipRoot` + 订阅 `Hidden` 事件

Hide 时：调 `popup.Hide()`，动画完成后由 `BaseUIPanel.Hidden` 事件触发 `OnPopupHidden` 回调：

- `_follow.Remove(type)` — 清跟随上下文
- 广播 `PopupHiddenEvent`
- **不再从 `_active` 移除**——保留实例供下次 Show 复用（这与 StackPanelModel 的处理不同）

外部 Destroy GO（MonoBehaviour）不走 Hide 动画，`Hidden` 事件不触发。下次 Show 检测 `popup == null || popup.gameObject == null` 清理死引用并重建。

### 11. CanvasGroup.blocksRaycasts = false（Popup 内置）

`BaseUIPopup.SetCanvasGroupNonBlocking()` 在 `Show/Hide/AfterShow/BeforeHide` 钩子自动设 `_canvasGroup.interactable = false; _canvasGroup.blocksRaycasts = false`。**不再依赖 Prefab 手动设置**——避免漏勾导致 Tooltip 抢点击。

## 公开 API 速查

```csharp
// UIManager 转发 API
uimgr.RegisterPopup<T>(prefab)                            // 预加载,挂在 UILayer.Tooltip
uimgr.ShowPopup<T>(screenPos, setup?)                     // 泛型,setup 回调写入数据
uimgr.ShowPopup<T>(screenPos, setup, boundsArea)          // boundsArea = 任意 RectTransform
uimgr.ShowPopup<T>(screenPos, setup, boundsArea, flip)    // flip = 首选翻转方向
uimgr.ShowPopup<T>(screenPos, setup, boundsArea, flip, followMouse)  // 鼠标跟随
uimgr.ShowPopup<T>(screenPos, setup, boundsArea, flip, followMouse, offset)  // 显式 offset
uimgr.ShowPopup<T>(target: RectTransform, ...)            // 贴指定 RT
uimgr.HidePopup<T>()                                      // 泛型
uimgr.HidePopup(type)                                     // 按 Type
uimgr.HideAllPopups()                                     // 全部隐藏
uimgr.IsPopupVisible<T>() / IsPopupVisible(type)          // 查询

// BaseUIPopup 子类必须实现
public override void SetData<TData>(TData data) where TData : class {
    // 把 data 写入自己的 UI 元素(TMP_Text/Image/...)
}
```

业务方最小使用（挂在 UI 上）：

```csharp
public class MyTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData e)
    {
        Game.Instance.GetManager<UIManager>()
            .ShowPopup<DefaultToolTip>(e.position, p => p.SetText("Hello"));
    }
    public void OnPointerExit(PointerEventData e)
    {
        Game.Instance.GetManager<UIManager>().HidePopup<DefaultToolTip>();
    }
}
```

**注意**：`DefaultToolTip` 由 UIManager 的 UIConfig 自动注册，业务方无需手动 `RegisterPopup`。要使用其他自定义 Popup 类型（继承 `BaseUIPopup`），仍需业务方在某处显式 `RegisterPopup` 一次。

## 事件

通过 `UIManager.Call<T>(...)` 广播：

| 事件 | 触发时机 | 字段 |
| --- | --- | --- |
| `PopupShownEvent` | Show 完成（仅 `shouldPlayShow` 时） | `PopupName` |
| `PopupHiddenEvent` | Hide 动画完成（`Hidden` 事件回调） | `PopupName` |

业务方订阅（可选，多数情况不需要）：

```csharp
uimgr.Register<PopupShownEvent>(e => Debug.Log($"shown: {e.PopupName}"));
uimgr.Register<PopupHiddenEvent>(e => Debug.Log($"hidden: {e.PopupName}"));
```

## 修复与演进对比

| 版本 | 主要变更 | 涉及文件 |
| --- | --- | --- |
| 初次实现 | 浮窗子系统 + UIConfig 零样板注册 | 全部新文件 |
| ea90105 | UIManager 拆分为 4 Module，Popup 独立 | `UIManager.cs` 转发、模块化 |
| 595240f | **修复坐标 + 动画重入**：popup 根本地坐标解算 + `shouldPlayShow` 守卫 + `ApplyPopupRaycastPolicy` 内置 | `PopupModule.cs` / `BaseUIPopup.cs` / `PopupLayoutHelper.cs` |
| 后续新增 | `followMouse` 鼠标跟随 + `target: RectTransform` 贴指定元素 | `PopupModule.cs` / `BaseUIPopup.cs` |

## 文件清单

| 操作 | 路径 |
| --- | --- |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/UILayer.cs`（加 Tooltip = 600） |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/UIEvent.cs`（加 PopupShownEvent/PopupHiddenEvent） |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/UIManager.cs`（`_tooltipRoot` + `_config` SerializeField + Awake 填表 + 自动注册 defaultTooltip + Start 注入 + Update 驱动 Tick + 8 个 Popup 转发 API） |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/BaseUIPopup.cs` |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/DefaultToolTip.cs` |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/UIConfig.cs`（CreateAssetMenu 路径 TGame/UI/UI Config） |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/Model/PopupModule.cs` |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/Model/PopupLayoutHelper.cs` |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/Sample/TUISample.cs`（移除 defaultToolTip 字段和 RegisterPopup 调用，由 UIConfig 接管） |
| 修改 | `TUI.csproj`（注册 5 个新文件） |

## 后续注意

- **UIConfig 资产创建**：Project 视图右键 → Create → TGame/UI/UI Config → 命名（如 `UIConfig.asset`）。Inspector 填 `defaultTooltip` prefab 字段，可选填 `tooltipOffset` / `tooltipFlipDirection`。
- **UIManager.prefab 拖入 _config**：把上一步创建的 `UIConfig.asset` 拖到 `UIManager.prefab` 的 `_config` 字段。**`_tooltipRoot` 子节点也要同时加**（在 UIManager GameObject 下加 `TooltipRoot` RectTransform，`anchorMin=0,0 anchorMax=1,1` 铺满），并拖到 `_tooltipRoot` 字段。
- **`DefaultToolTip.prefab` 创建需 Editor 手动**：Prefab 内部结构参考本档"4. 内容 RectTransform 模式"。**`CanvasGroup.blocksRaycasts` 现在无需手动勾**（Popup 内置 `SetCanvasGroupNonBlocking`），但勾上不会冲突。`DefaultToolTip` 需在 Inspector 拖入一个 `TTweenPlay` 资产（项目已有 Tween 子系统）作为入场动画。
- **`BaseUIPopup.Reset()` 自动取自身 RT 作为 _content**：编辑器 Reset 时把 `_content` 自动指向自身的 RectTransform（Popup 实例化后会由 `SetupPopupRoot` 重置 panel 根 RT）。如果有多个子节点，需手动拖引用。
- **越界选 overlap 最大**：极端情况（浮窗 size > 屏幕）选覆盖面积最大的方向，**且会 `ClampPivotPosition` 收口到边界**，允许部分超出。如果业务方需要"硬截断到边界"，可在 `PopupLayoutHelper.Solve` 后追加 Clamp 步骤（已内置，参见 `ClampPivotPosition`）。
- **`HideAll()` 不立即清字典**：各 popup 异步动画完成后才在 Hidden 回调里清 follow 状态，业务方若有"立刻重新注册同名 popup"需求，需等动画完成或手动调 `Destroy` 同步销毁。
- **Pivot 翻转副作用**：翻转后 `_content.pivot` 改变，会带动子节点布局偏移。这是预期行为（浮窗"锚角"在鼠标点，翻转后自然换到对侧），但若业务方在 `_content` 内用 `ContentSizeFitter`，第一次布局可能不正确，可重写 `BuildShowAnimation` 强制一次 `ForceRebuildLayoutImmediate`。
- **鼠标跟随 + 时间缩放**：`UIManager.Update` 用 `Time.unscaledDeltaTime` 调用 `PopupModule.Tick`，暂停时 Tooltip 仍跟随鼠标。如需暂停时冻结，可改用 `Time.deltaTime`。
- **UIConfig 字段可后续扩展**：`tooltipBackgroundSprite`、`tooltipDefaultFontSize` 等全局参数都可加。`_defaultTooltip` 可加 `[Header("Custom Tooltips")]` + `List<CustomPopupEntry>` 支持一组预定义 Popup 类型，业务方按需注册。
