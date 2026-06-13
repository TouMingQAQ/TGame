# PopupModule 设计文档

## 概览

| 项目 | 值 |
|------|-----|
| 模块 | `TUI.Model.PopupModule` + `TUI.BaseUIPopup` + `TUI.DefaultToolTip` + `TUI.UIConfig` |
| 父级 | `UIManager`(作为 `BaseModule` 挂在它下面);`UIConfig` 作为 ScriptableObject 资产被 UIManager 引用 |
| 文件 | `Runtime/BaseUIPopup.cs` + `Runtime/DefaultToolTip.cs` + `Runtime/UIConfig.cs` + `Runtime/Model/PopupModule.cs` + `Runtime/Model/PopupLayoutHelper.cs` |
| 提交 | feat(TUI): 新增浮窗(Popup/Tooltip)模块,自动定位 + 翻转 + 单实例 + UIConfig 零样板 |
| 前置 | 现有 5 个 BaseModule(Registry/LayerRoot/Loader/Visibility/StackPanel) |

## 背景与目标

**问题:** TUI 子系统当前只有"全屏/半屏面板"语义(UILayer 6 层 + StackPanelModel 栈式管理),缺少"鼠标悬停显示、自动定位、避免越界"的小型浮窗能力。Tooltip/气泡/ContextMenu/HelpHint 这类 UI 在游戏里非常常见,但目前每个业务方都要重复实现:监听 PointerEnter/Exit、计算屏幕坐标、检测越界、写翻转逻辑。

**目标:** 在 UIManager 下新增一个**浮窗子系统**,提供:

1. 浮窗基类 `BaseUIPopup` + 默认实现 `DefaultToolTip`(只显示文本)
2. `PopupModule` 负责生命周期、自动定位、边界翻转、单实例语义
3. 新增 UILayer `Tooltip = 600`,**独占一层,始终最顶**(在 Modal 之上)
4. 翻转策略:默认放鼠标右下,越界时按"下→上、右→左"优先级自动翻到另一侧
5. 范围边界可指定:默认 = 整个屏幕,也可指定任意 `RectTransform`(适配子区域)
6. 泛型 API:`ShowPopup<T>(screenPos, setup?)` / `HidePopup<T>()` / `RegisterPopup<T>(prefab)`
7. **UIConfig ScriptableObject**:装载"非场景"配置(默认浮窗 prefab + 全局浮窗参数),UIManager.Awake 自动读取并注册,业务方零样板
8. 触发器**不内置** MonoBehaviour,业务方自己接 `IPointerEnterHandler`

## 新架构

```text
UIManager : BaseManager
 ├─ RegisterPanel / ShowPanel / HidePanel / LoadPanel / UnloadPanel  (现有 5 个 Module)
 ├─ GetModule<StackPanelModel>()                                      (栈逻辑)
 └─ GetModule<PopupModule>()                                          (新:浮窗)
     ├─ Register<T>(prefab)          → _prefabs[type] = prefab.gameObject
     ├─ Show<T>(pos, setup)          → 单实例 + Anchor + SetAsLastSibling + Show + Call(PopupShownEvent)
     ├─ Hide<T>()                    → BaseUIPopup.Hide(),动画完成后 Hidden 事件回调清 _active
     ├─ HideAll()                    → 遍历 _active 逐个 Hide
     └─ IsVisible<T>()
        ↓ 委托
        PopupLayoutHelper.Solve(screenAnchor, size, area, flip, offset) → (pos, pivot)

UIConfig : ScriptableObject  (Create → TGame/UI/UI Config)
 ├─ [SerializeField] DefaultToolTip _defaultTooltip
 ├─ [SerializeField] float _tooltipOffset = 8
 └─ [SerializeField] PopupFlipDirection _tooltipFlipDirection = BottomRight
        ↓ UIManager.Awake 自动读取并 RegisterPopup(_config.DefaultTooltip)

BaseUIPopup : BaseUIPanel
 ├─ [SerializeField] RectTransform _content    ← Module 改它的 pivot + anchoredPosition
 ├─ [SerializeField] float _defaultOffset
 ├─ abstract SetData<TData>(data)              ← 子类实现,setup 回调里调用
 └─ virtual Anchor(pos, area, flip, offset)    ← 调 PopupLayoutHelper.Solve
```

## 关键设计

### 1. UIConfig 零样板注册(非场景配置统一收口)

`UIConfig` 是 ScriptableObject,装载"非场景"配置(默认浮窗 prefab + 全局浮窗参数)。**不**装场景引用(layer root、canvas 等)— 那些仍由 `UIManager.prefab` 自身 SerializeField 管理,这是"配置资产"和"场景预制体"的明确边界。

`UIManager` 新增 `[SerializeField] UIConfig _config`,`Awake` 在填 TooltipRoot 之后读取 config 并自动 `RegisterPopup(_config.DefaultTooltip)`。业务方只需在 UIManager.prefab 上挂一次配置资产,**TUISample 等场景代码完全不用写 `RegisterPopup(defaultTooltip)`**。

设计优势:

- **零样板**:`UIManager.prefab` 配置好后,业务方直接 `ShowPopup<DefaultToolTip>(pos, p => p.SetText("Hi"))` 即可,无需先注册。
- **可热改**:`UIConfig` 是独立资产,改 `defaultTooltip` prefab / `tooltipOffset` 不需要修改 `UIManager.prefab` 引用链。
- **可扩展**:后续可加 `tooltipAnimationCurve`、`tooltipBackgroundSprite` 等,统一收纳。

### 2. 浮窗走独立 Layer(Tooltip = 600)

`UILayer` 新增 `Tooltip = 600`,值比 Top(500) 更大,确保**浮窗永远渲染在最顶**,即使在 Modal 弹窗之上仍可见。这符合典型 Tooltip 语义——"确认按钮的说明"在弹窗上仍能透出。

UIManager 新增 `[SerializeField] Transform _tooltipRoot`,`Awake` 填表到 `UILayerRootModule`,`Start` 注入 self 到 `PopupModule`。

### 3. PopupModule 独立加载(不走 UIRegistry/Loader/Visibility)

Popup 不走 `UIRegistryModule` + `UILoaderModule` + `UIVisibilityModule` 三件套,原因:

- **layer 强制 Tooltip**:`UIRegistryModule.Register(prefab, layer)` 需要业务方传 layer,但 Popup 永远挂 Tooltip,多余参数污染注册表语义。
- **Show 时序不同**:UIVisibilityModule 是"先 Load 再 Show",Popup 需要"先 Anchor 再 Show",否则动画第一帧位置会跳变。
- **单实例 + Hidden 订阅**:PopupModule 用自己的 `_active : Dictionary<Type, BaseUIPopup>` 管单实例 + `Hidden` 事件清字典,与 `UILoaderModule._loaded` 缓存分离更清晰。
- **独立 Prefab 路径**:`_prefabs : Dictionary<Type, GameObject>` 不污染 `_configs` 的 `(prefab, layer)` 二元组语义。

### 4. 内容 RectTransform 模式(避开 UILoaderModule 的 RT 强制重置)

`UILoaderModule.Load` 会强制重置 panel RT 为填充父节点:

```csharp
rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
```

PopupModule 不走 UILoaderModule,但仍遵守"panel 自身 RT 填满父节点"的约束——因为父节点是 `_tooltipRoot`(也是 0/0/1/1)。**Module 改的永远是 `_content` 子节点的 pivot + anchoredPosition**,不是 panel 自身 RT。

Prefab 内部结构:

```text
[PanelRoot]  (BaseUIPopup/DefaultToolTip/TooltipPanel + CanvasGroup + RectTransform)
│           anchorMin=0,0  anchorMax=1,1  offsetMin=0,0  offsetMax=0,0  pivot=0.5,0.5
│           ★ _content SerializeField 指向下一个节点
│
└── [Content] (RectTransform)
            anchorMin=0,0  anchorMax=0,0  pivot=0.5,0.5  sizeDelta=(200,80)
    ├── [Background] (Image)
    └── [Label/Title/Body] (TMP_Text)
```

### 5. 翻转算法(PopupLayoutHelper.Solve)

**输入:** `screenAnchor` (鼠标屏幕坐标), `contentSize` (浮窗 sizeDelta), `area` (边界 RT,null=全屏), `preferred` (调用方偏好方向), `offset` (像素间距,默认 8)

**输出:** `(Vector2 anchoredPos, Vector2 pivot)` — 写到 `_content` 上

**步骤:**

1. **确定 areaRect 屏幕坐标**:`area == null` → `new Rect(0, 0, Screen.width, Screen.height)`;`area != null` → `area.GetWorldCorners` 算 4 角转屏幕坐标
2. **4 个候选方向**:(pivot, flipDirection) 配对

   - `(0,0) → BottomRight` / `(1,0) → BottomLeft` / `(0,1) → TopRight` / `(1,1) → TopLeft`
   - 按 `preferred` 优先排序(冒泡 swap,O(1))
3. **每个候选**:

   - `shift = directionVector * offset`(BottomRight=(1,1), BottomLeft=(-1,1), TopRight=(1,-1), TopLeft=(-1,-1))
   - `bl = screenAnchor + shift - pivot * size` / `tr = screenAnchor + shift + (1-pivot) * size`
   - 完全在 `areaRect` 内 → 选这个,跳出
4. **全部越界**:选 `overlap = max(0, min(tr, areaMax) - max(bl, areaMin)).x * .y` 最大的
5. **计算最终 anchoredPos**:

   - `area == null`: `pos = screenAnchor + shift`(ScreenSpaceOverlay 下 Canvas 本地 = 屏幕)
   - `area != null`: `RectTransformUtility.ScreenPointToLocalPointInRectangle(area, anchor, cam, out local)` 转 area 本地坐标

**复杂度:** O(1)(4 个固定候选),无迭代,无 GC alloc(除 List/corners 临时数组)。

### 6. Anchor 时序:必须在 Show() 之前

`BaseUIPanel.Show()` 触发入场动画,动画开始后改 anchoredPosition 会导致第一帧位置跳变。PopupModule.Show 严格按顺序:

```text
1. setup?.Invoke(popup)        // 写入数据
2. popup.Anchor(...)           // 计算并应用最终位置
3. popup.transform.SetAsLastSibling()
4. if (!popup.IsVisible) popup.Show()   // 触发动画
5. _ui.Call(new PopupShownEvent(...))
```

`Anchor` 内部先 `LayoutRebuilder.ForceRebuildLayoutImmediate(_content)` 刷新尺寸(让 TMP/ContentSizeFitter 后的真实 size 生效),失败时回退到 `sizeDelta`。

### 7. 同 Type 单实例 + Hidden 事件回调清理

`_active : Dictionary<Type, BaseUIPopup>` — 同 Type 同时只显示一个。

Show 时:

- 命中且实例未销毁 → 复用 + 重新 setup + 重新 Anchor
- 未命中或死引用 → Instantiate 到 `_tooltipRoot` + 订阅 `Hidden` 事件

Hide 时:调 `popup.Hide()`,动画完成后由 `BaseUIPanel.Hidden` 事件触发 `OnPopupHidden` 回调:

- `_active.Remove(type)` — 下次 Show 才能"重新出现"
- 反注册 `Hidden` 订阅
- 广播 `PopupHiddenEvent`

外部 Destroy GO(MonoBehaviour)不走 Hide 动画,`Hidden` 事件不触发。下次 Show 检测 `popup == null || popup.gameObject == null` 清理死引用并重建。

### 8. CanvasGroup.blocksRaycasts = false(Prefab 手动设置)

Tooltip 不抢点击,鼠标能"穿透"浮窗触底下层 UI。BaseUIPanel.Reset 不自动设,需要在 Prefab 手动勾掉 `CanvasGroup.blocksRaycasts`。`DefaultToolTip` Prefab 必须在创建时设 `blocksRaycasts = false`。

### 9. screenPos 调用方传(不读鼠标)

框架不耦合 Input System(新/旧),TUI.asmdef 引用零增量。业务方在 `IPointerEnterHandler.OnPointerEnter` 拿到 `PointerEventData.position` 直接传。触屏/远程控制/键盘焦点等场景全支持。

## 公开 API 速查

```csharp
// UIManager 转发 API
uimgr.RegisterPopup<T>(prefab)                    // 预加载,挂在 UILayer.Tooltip
uimgr.ShowPopup<T>(screenPos, setup?)             // 泛型,setup 回调写入数据
uimgr.ShowPopup<T>(screenPos, setup, boundsArea)  // boundsArea = 任意 RectTransform
uimgr.ShowPopup<T>(screenPos, setup, boundsArea, flip)  // flip = 首选翻转方向
uimgr.HidePopup<T>()                              // 泛型
uimgr.HidePopup(type)                             // 按 Type
uimgr.HideAllPopups()                             // 全部隐藏
uimgr.IsPopupVisible<T>() / IsPopupVisible(type)  // 查询

// BaseUIPopup 子类必须实现
public override void SetData<TData>(TData data) where TData : class
{
    // 把 data 写入自己的 UI 元素(TMP_Text/Image/...)
}
```

业务方最小使用(挂在 UI 上):

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

**注意:`DefaultToolTip` 由 UIManager 的 UIConfig 自动注册,业务方无需手动 `RegisterPopup`**。要使用其他自定义 Popup 类型(继承 `BaseUIPopup`),仍需业务方在某处显式 `RegisterPopup` 一次。

## 事件

通过 `UIManager.Call<T>(...)` 广播:

- `PopupShownEvent(name)` —— 浮窗显示(setup 完成 + Show 触发)
- `PopupHiddenEvent(name)` —— 浮窗 Hide 动画完成(gameObject 已 Deactivate)

业务方订阅(可选,多数情况不需要):

```csharp
uimgr.Register<PopupShownEvent>(e => Debug.Log($"shown: {e.PopupName}"));
uimgr.Register<PopupHiddenEvent>(e => Debug.Log($"hidden: {e.PopupName}"));
```

## 文件清单

| 操作 | 路径 |
| --- | --- |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/UILayer.cs`(加 Tooltip = 600) |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/UIEvent.cs`(加 PopupShownEvent/PopupHiddenEvent) |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/UIManager.cs`(_tooltipRoot + _config SerializeField + Awake 填表 + 自动注册 defaultTooltip + Start 注入 + 六个 Popup 转发 API) |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/BaseUIPopup.cs` |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/DefaultToolTip.cs` |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/UIConfig.cs`(CreateAssetMenu 路径 TGame/UI/UI Config) |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/Model/PopupModule.cs` |
| 新增 | `Assets/Plugins/TGame/TUI/Runtime/Model/PopupLayoutHelper.cs` |
| 修改 | `Assets/Plugins/TGame/TUI/Runtime/Sample/TUISample.cs`(移除 defaultToolTip 字段和 RegisterPopup 调用,由 UIConfig 接管) |
| 修改 | `TUI.csproj`(注册 5 个新文件) |

## 后续注意

- **UIConfig 资产创建**:Project 视图右键 → Create → TGame/UI/UI Config → 命名(如 `UIConfig.asset`)。Inspector 填 `defaultTooltip` prefab 字段,可选填 `tooltipOffset` / `tooltipFlipDirection`。
- **UIManager.prefab 拖入 _config**:把上一步创建的 `UIConfig.asset` 拖到 `UIManager.prefab` 的 `_config` 字段。**`_tooltipRoot` 子节点也要同时加**(在 UIManager GameObject 下加 `TooltipRoot` RectTransform,`anchorMin=0,0 anchorMax=1,1` 铺满),并拖到 `_tooltipRoot` 字段。
- **`DefaultToolTip.prefab` 创建需 Editor 手动**:Prefab 内部结构参考本档"4. 内容 RectTransform 模式"。**注意:`CanvasGroup.blocksRaycasts` 必须勾掉**(Tooltip 不抢点击)。`DefaultToolTip` 还需在 Inspector 拖入一个 `TTweenPlay` 资产(项目已有 Tween 子系统)作为入场动画。
- **`BaseUIPopup.Reset()` 自动取第一个子 RT**:编辑器 Reset 时把 `_content` 自动指向第一个子 RectTransform(常见用法:Panel 根 → Content 子节点)。如果有多个子节点,需手动拖引用。
- **越界选 overlap 最大**:极端情况(浮窗 size > 屏幕)选覆盖面积最大的方向,允许部分超出。如果业务方需要"硬截断到边界",可在 `PopupLayoutHelper.Solve` 后追加 Clamp 步骤,后续按需加。
- **`HideAll()` 不立即清字典**:各 popup 异步动画完成后才在 Hidden 回调里清,业务方若有"立刻重新注册同名 popup"需求,需等动画完成或手动调 `UnloadPanel<BaseUIPopup>` 同步销毁。
- **Pivot 翻转副作用**:翻转后 `_content.pivot` 改变,会带动子节点布局偏移。这是预期行为(浮窗"锚角"在鼠标点,翻转后自然换到对侧),但若业务方在 `_content` 内用 `ContentSizeFitter`,第一次布局可能不正确,可在 `BuildShowAnimation` 重写里强制一次 `ForceRebuildLayoutImmediate`。
- **UIConfig 字段可后续扩展**:`tooltipBackgroundSprite`、`tooltipDefaultFontSize` 等全局参数都可加。`_defaultTooltip` 可加 `[Header("Custom Tooltips")]` + `List<CustomPopupEntry>` 支持一组预定义 Popup 类型,业务方按需注册。
