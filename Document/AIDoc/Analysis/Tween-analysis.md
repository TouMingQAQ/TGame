# Tween 动画系统分析

> 最近更新：2026-06-14，基于 master 分支 HEAD（1eb5fca）。
> 范围：`Assets/Plugins/TGame/Tween/`。
> 外部依赖：`com.demigiant.dotween`（DOTween，3.x）。

## 概览

| 项目 | 值 |
| --- | --- |
| 程序集 | `TGame.Tween`（Runtime + Editor 包装） |
| 命名空间 | `TGame.Tween` |
| 文件数 | 16 个 .cs（11 Node + 2 Player/TimeLine + 1 Editor + 2 Window） |
| 外部依赖 | DOTween（用 `using DG.Tweening;`） |
| 提交 | `9037ed8 feat(Tween): 接入 Animator 状态动画` / `1eb5fca Update animator tween state duration` / `9850401 alias` / `5143b3f` 等 |

## 设计目标

把"复杂动画编排"做成**可视化、可序列化、可复用**的组件体系：

1. 每个动画元素（Move/Scale/Rotate/Fade/Color/Callback/Delay/Punch/Shake/AnimatorState）是一个 `TTweenNode` 组件
2. `TTweenPlay` 把多个 Node 按时间轴编排成 Sequence
3. `TTweenTimeLine` 把多个 Play 编排到更上层时间轴
4. 可视化窗口（`TTweenPlayWindow` / `TTweenTimeLineWindow`）让设计师在 Editor 中**拖块、设时长、设起始时间**，所见即所得

与直接用 DOTween API 相比：**配置和代码分离**（动画时长、起始时间、缓动函数等都在 Inspector/可视化窗口里），**可在不进入 PlayMode 的情况下预览时间轴布局**（但**实际播放仍需 PlayMode**）。

## 架构

```text
TTweenNode (abstract MonoBehaviour)
 ├─ TTweenMove / TTweenMoveLocal    (位置移动)
 ├─ TTweenScale                     (缩放)
 ├─ TTweenRotate                    (旋转)
 ├─ TTweenFade                      (CanvasGroup 透明度)
 ├─ TTweenColor                     (Graphic 颜色)
 ├─ TTweenPunch                     (弹性回弹)
 ├─ TTweenShake                     (抖动)
 ├─ TTweenCallback                  (UnityEvent 回调)
 ├─ TTweenDelay                     (时间间隔)
 └─ TTweenAnimatorState             (Animator 状态采样,9037ed8 新增)
        ↓
TTweenPlay (MonoBehaviour, 收集子 TTweenNode, 编排 Sequence)
        ↓
TTweenTimeLine (MonoBehaviour, 收集多个 TTweenPlay, 二级编排)

Editor:
 ├─ TTweenPlayEditor / TTweenTimeLineEditor    (Default Inspector + 预览按钮)
 ├─ TTweenPlayWindow                            (可视化时间轴窗口, ITweenNode 数据源)
 ├─ TTweenTimeLineWindow                        (可视化时间轴窗口, IPlay 数据源)
 └─ TTweenTimelineWindowBase                    (两窗口公共基类, 渲染/拖拽/滚动/缩放)
```

## 核心类型

### ITweenNode

```csharp
public abstract class TTweenNode : MonoBehaviour
{
    public abstract DG.Tweening.Tween BuildTween();
    public virtual float GetPlaybackDuration() => _duration;
    public float Duration { get; set; }   // 序列化字段
}
```

`BuildTween()` **不自动播放**，由父编排器统一 `Sequence.Insert(startTime, tween)` 控制时间线。

**重要约束**：`namespace TGame.Tween` 内使用 `DG.Tweening.Tween` **必须用完全限定名**（如 `public override DG.Tweening.Tween BuildTween()`），避免与 `TGame.Tween` 命名空间产生解析歧义。这是项目级规则。

### TTweenPlay

```csharp
[AddComponentMenu("TGame/Tween/TTweenPlay")]
public class TTweenPlay : MonoBehaviour
{
    public struct NodeEntry
    {
        public TTweenNode node;
        public float startTime;
        public string alias;        // 别名,空时回退到 node 名称
    }

    public bool AutoPlayOnStart;
    public int Loops = 1;
    public LoopType LoopType = LoopType.Restart;
    public bool IgnoreTimeScale = true;
    public List<NodeEntry> NodeEntries;

    public void Play();           // 构建 Sequence + Play
    public void Stop();           // Rewind
    public void Pause();          // Pause
    public void Resume();
    public void Kill();           // 销毁 Sequence
    public DG.Tweening.Tween BuildTween();   // 供 TTweenTimeLine 嵌入
}
```

**注意**：`BuildTween()` 返回 `DG.Tweening.Tween`（**不是** `Sequence`），因为 `Sequence.Insert` 接受 `Tween`，而每个 `TTweenNode.BuildTween` 内部可能返回 `Sequence` 或 `Tween`，统一用基类 `Tween` 接收。

**alias 机制**（`9850401 feat(Tween): 条目支持 alias 别名`）：当多个 Node 是同类型（如两个 `TTweenMove`）时，Inspector 默认显示文件名无法区分。`alias` 字段给每个 NodeEntry 一个可读标签，**空时回退到 node 的 GameObject 名**。

**alias 在可视化窗口**中显示为 Block 文字，是 Designer 调试时间轴的关键字段。

### TTweenTimeLine

与 `TTweenPlay` 同构，**Entry 引用 `TTweenPlay` 而非 `TTweenNode`**：

```csharp
public struct TimeLinePlayEntry
{
    public TTweenPlay play;
    public float startTime;
    public string alias;
}
```

构建流程：

```csharp
public void Play() {
    Kill();
    _runtimeSequence = BuildSequenceFromEntries();
    _runtimeSequence?.Play();
}

private Sequence BuildSequenceFromEntries() {
    var seq = DOTween.Sequence();
    seq.SetAutoKill(false);
    seq.SetUpdate(_ignoreTimeScale);
    seq.SetLink(gameObject);
    if (_loops != 1) seq.SetLoops(_loops, _loopType);
    foreach (var entry in _entries) {
        if (entry.play == null) continue;
        var tween = entry.play.BuildTween();
        if (tween == null) continue;
        seq.Insert(entry.startTime, tween);
    }
    return seq;
}
```

`SetLink(gameObject)` 让 DOTween 在 GameObject 销毁时**自动 Kill** 关联 Tween，避免悬挂引用。

`SetUpdate(_ignoreTimeScale)` 让动画在 `Time.timeScale = 0` 时仍可推进（暂停菜单/技能动画常用）。

## 11 个 Node 详解

按"通用模式"和"特殊模式"分类：

### 通用模式：To/From + ChangeStartValue

5 个 transform-affecting Node 共享同一模式（`TTweenMove` / `TTweenMoveLocal` / `TTweenScale` / `TTweenRotate`）：

```csharp
public override DG.Tweening.Tween BuildTween()
{
    Vector3 start = _fromValue;
    Vector3 end = _targetValue;
    if (_mode == XxxMode.From) { start = _targetValue; end = _fromValue; }

    var tween = Target.DOScale(end, Duration);   // DOMove / DORotate / DOLocalMove
    tween.ChangeStartValue(start);

    // 关键:用 Sequence 包一个 0 时长回调设 start,避免 BuildTween() 时立刻改 Transform
    var seq = DOTween.Sequence();
    seq.AppendCallback(() => { Target.position = start; });  // ← 仅播放时才设
    seq.Append(tween);
    ApplyEase(tween);
    return seq;
}
```

**为什么用 `Sequence` + `AppendCallback` 包装？**  
`ChangeStartValue` 只在 Tween 启动时生效，但**直接 `Target.position = start` 会立刻改 Transform**，破坏 Editor 视图。包成 Sequence 后，`AppendCallback` 在 Tween 实际播放第一帧才执行，**Editor 中不触发副作用**。

`TTweenMove` 还多一个 `Additive` 模式：`DOMove(Target.position + _targetValue, ...)`，**忽略 `_fromValue`**，相对当前位置做偏移。

### 通用模式：CanvasGroup / Graphic

`TTweenFade` (CanvasGroup) 和 `TTweenColor` (Graphic) 结构相同：

```csharp
private Target Get => _target ?? GetComponent<Target>();   // 缺省取自身
public override DG.Tweening.Tween BuildTween() {
    if (Target == null) return null;
    var tween = Target.DOFade(_targetValue, Duration);   // / DOColor
    tween.ChangeStartValue(_fromValue);
    var seq = DOTween.Sequence();
    seq.AppendCallback(() => { Target.alpha = _fromValue; });
    seq.Append(tween);
    if (_customCurve != null && _customCurve.length > 0) tween.SetEase(_customCurve);
    else tween.SetEase(_ease);
    return seq;
}
```

`TTweenColor` 标了 `[RequireComponent(typeof(Graphic))]` —— 必须挂在 Image/Text/RawImage 等 Graphic 派生类上。

`TTweenFade` 没有 RequireComponent，因为 CanvasGroup 通常是独立组件。

### 特殊模式：Punch / Shake

`TTweenPunch`（`DOPunchPosition`）和 `TTweenShake`（`DOShakePosition`）**用 DOTween 高级 API**：

```csharp
// TTweenPunch
public override DG.Tweening.Tween BuildTween() {
    return Target.DOPunchPosition(_strength, Duration, _vibrato, _elasticity);
}

// TTweenShake
public override DG.Tweening.Tween BuildTween() {
    return Target.DOShakePosition(Duration, _strength, _vibrato, _randomness, _snapping, _fadeOut);
}
```

不需要 `ChangeStartValue` 包装 —— 起始位置就是当前位置。

### 特殊模式：Callback / Delay

```csharp
// TTweenCallback
public override DG.Tweening.Tween BuildTween() {
    return DOTween.Sequence().AppendCallback(() => _onInvoke?.Invoke());
}

// TTweenDelay
public override DG.Tweening.Tween BuildTween() {
    return DOTween.Sequence().AppendInterval(Duration);
}
```

`TTweenCallback` 用 `UnityEvent` 暴露回调钩子，业务方在 Inspector 拖拽目标和方法。`Duration` 字段被忽略（回调是 0 时长）。

### 特殊模式：TTweenAnimatorState（最新）

`9037ed8 feat(Tween): 接入 Animator 状态动画` —— 用 DOTween 驱动 Animator 的 `normalizedTime`，把 Animator Controller 中的一段 State 接入时间轴。

```csharp
public override DG.Tweening.Tween BuildTween() {
    var target = ResolveTarget();
    if (target == null || string.IsNullOrEmpty(_stateName)) return null;
    if (!TryResolveStateHash(target, out _stateHash)) return null;

    float normalizedTime = _fromNormalizedTime;
    float duration = ResolveDuration(target);

    var tween = DOTween.To(
        () => normalizedTime,
        value => { normalizedTime = value; Sample(target, normalizedTime); },
        _toNormalizedTime,
        duration);

    var seq = DOTween.Sequence();
    seq.Append(tween);
    seq.OnPlay(() => { CaptureAnimatorSpeed(target); Sample(target, normalizedTime); });
    seq.OnComplete(() => RestoreAnimatorSpeed(target));
    seq.OnRewind(() => RestoreAnimatorSpeed(target));
    seq.OnKill(() => RestoreAnimatorSpeed(target));
    return seq;
}
```

**关键设计**：

- **DOTween 驱动 Animator 时间**：通过 `DOTween.To(Getter, Setter, endValue, duration)` 持续 Setter 更新 `normalizedTime`，再调 `target.Update(0f)` 强制采样
- **State 时长解析**（`TryGetStateLength`）：临时 `target.Play(stateHash, layer, fromNormalizedTime); target.Update(0f);` 拿到 `sampledState.length`，**再恢复原 State**
- **Animator.speed 冻结**（`_freezeAnimatorSpeed = true`）：播放期间 `target.speed = 0`，避免 Animator 自身时间推进与 Tween 冲突；`OnComplete/OnRewind/OnKill` 恢复 `_savedSpeed`
- **State 名称解析**：先用直接名 `Animator.StringToHash("StateName")` 查 `target.HasState(layer, hash)`，失败则尝试 `"LayerName.StateName"` 全路径（处理 sub state machine）
- **Inspector 自定义**：用 `[TTweenAnimatorStateName]` + `TTweenAnimatorStateNameDrawer` 在 Editor 中**渲染为下拉框**（列出 Controller 该 Layer 的所有 State），`5133b3f` 后可手动编辑

**GetPlaybackDuration 覆盖**：

```csharp
public override float GetPlaybackDuration() {
    return TryGetPlaybackDuration(out var duration) ? duration : 0.0001f;
}
```

返回**真实 Animator State 时长**（按 `_fromNormalizedTime` → `_toNormalizedTime` 范围 × state length），供 `TTweenPlay.CalculateTotalDuration` 计算总时长。

## 可视化窗口

两个窗口共享 `TTweenTimelineWindowBase` 基类，**只覆盖数据源抽象**：

```csharp
public abstract class TTweenTimelineWindowBase : EditorWindow
{
    protected abstract UnityEngine.Object TargetObject { get; }
    protected abstract int EntryCount { get; }
    protected abstract float CalculateTotalDuration();
    protected abstract float GetEntryStartTime(int index);
    protected abstract float GetEntryDuration(int index);
    protected abstract string GetEntryLabel(int index);
    protected abstract string EntriesPropertyName { get; }
    protected virtual string StartTimePropertyName => "startTime";
    protected abstract string WindowTitle { get; }
    protected abstract string ListHeader { get; }
    protected abstract string AddButtonText { get; }
    protected abstract string CollectButtonText { get; }
    protected abstract void AddEntry();
    protected abstract void CollectFromChildren();
    protected abstract void PlayTarget();
    protected abstract void KillTarget();
    protected abstract void DrawEntryFields(Rect r, int index, SerializedProperty entryProp, SerializedProperty startTimeProp, float startX);
    ...
}
```

### 窗口结构

```text
┌─────────────────────────────────────┐
│ Toolbar: ▶ Play | ▶ Restart | ■ Stop | Zoom: [▬▬] 1.0x | Fit │
│ Status: Editing: <Name>  |  Entries: N                  │
├─────────────────────────────────────┤
│ Canvas (130px height, IMGUI 绘制)    │
│  ┌────────────────────────────────┐ │
│  │ [Block 1]    [Block 2]         │ │  ← 拖块改 startTime
│  │ Total: 3.20s    0.5s  1.0s ... │ │
│  └────────────────────────────────┘ │
│              [Scrollbar]             │
├─────────────────────────────────────┤
│ ReorderableList:                     │
│  #0 | NodeA | alias | 0.00 | 0.30  [x] │
│  #1 | NodeB | alias | 0.30 | 0.50  [x] │
│  ...                                 │
├─────────────────────────────────────┤
│ [+ Add Node]   [Collect Children]    │
└─────────────────────────────────────┘
```

### 关键技术点

**滚轮缩放（以鼠标位置为中心）**：

```csharp
if (Event.current.type == EventType.ScrollWheel && rect.Contains(Event.current.mousePosition)) {
    float ct = _scrollTime + (Event.current.mousePosition.x - tlRect.x) / tlRect.width * visibleTime;
    ct = Mathf.Clamp(ct, 0, totalTime);
    _zoom *= Event.current.delta.y > 0 ? ZoomInFactor : ZoomOutFactor;
    _zoom = Mathf.Clamp(_zoom, ZoomMin, ZoomMax);
    _zoomSlider.SetValueWithoutNotify(_zoom);
    float nv = totalTime / _zoom;
    _scrollTime = Mathf.Clamp(
        ct - (Event.current.mousePosition.x - tlRect.x) / tlRect.width * nv,
        0, Mathf.Max(0, totalTime - nv));
    Event.current.Use();
}
```

**拖块改 startTime**（`8362d51 refactor(Tween): 抽取可视化编辑器基类` 后）：

```csharp
if (Event.current.type == EventType.MouseDown && Event.current.button == 0) {
    foreach (var (br, i, _, _, st) in blocks) {
        var hr = new Rect(br.x - 4, br.y - 2, br.width + 8, br.height + 4);
        if (hr.Contains(Event.current.mousePosition)) {
            Undo.RecordObject(TargetObject, "Drag Entry Start Time");
            _dragIndex = i;
            _dragInitialTime = st;
            _dragInitialMouse = Event.current.mousePosition;
            Event.current.Use();
            break;
        }
    }
}
if (Event.current.type == EventType.MouseDrag && _dragIndex >= 0) {
    var ep = _entriesProp.GetArrayElementAtIndex(_dragIndex);
    ep.FindPropertyRelative(StartTimePropertyName).floatValue = dragDisplayTime;
    _serializedObject.ApplyModifiedProperties();
    Event.current.Use();
}
```

**裁剪到时间轴区域**（`8362d51 refactor(Tween): 抽取可视化编辑器基类 TTweenTimelineWindowBase，修复多项 Bug` 关键修复之一）—— 防止长块被错误跳过：

```csharp
if (endTime < _scrollTime - CullingMargin || st > _scrollTime + visibleTime + CullingMargin) continue;
...
var clipBr = ClipRect(br, tlRect);
if (clipBr.width <= 0 || clipBr.height <= 0) continue;
```

**跟手滚动**：拖块接近右边界时自动 `_scrollTime += ... * FollowSpeed`（`FollowMarginRatio = 0.75f`），让长序列也可视。

**时间刻度自适应**：

```csharp
protected static float GetTimeStep(float t) =>
    t <= 1f ? 0.25f : t <= 3f ? 0.5f : t <= 10f ? 1f : t <= 30f ? 5f : 10f;
```

**ReorderableList 拖拽排序**：用 `UnityEditorInternal.ReorderableList`，`onReorderCallback` 标 dirty。

**Duration 编辑实时同步**（`TTweenPlayWindow.DrawEntryFields`）：点击 Node 行 Duration 字段，**直接修改 `node._duration` 字段**（通过 `new SerializedObject(nodeObj)`）：

```csharp
if (nodeObj is TTweenAnimatorState) {
    using (new EditorGUI.DisabledScope(true))
        EditorGUI.FloatField(durRect, nodeObj.GetPlaybackDuration());
} else {
    var nodeSO = new SerializedObject(nodeObj);
    var durProp = nodeSO.FindProperty("_duration");
    float before = durProp.floatValue;
    EditorGUI.PropertyField(durRect, durProp, GUIContent.none);
    if (nodeSO.ApplyModifiedProperties() && !Mathf.Approximately(before, durProp.floatValue))
        _needsRepaint = true;
    nodeSO.Dispose();
}
```

`TTweenAnimatorState` 的 Duration **禁用编辑**（灰显），因为它由 Animator State 长度决定。

### Default Inspector 增强

`TTweenPlayEditor` / `TTweenTimeLineEditor`（`TTweenPlayEditor.cs`）在默认 Inspector 上加 3 个按钮：

- ▶ Play / ▶ Restart / ■ Stop
- Collect Children / Open Play Editor

**注意**：预览按钮仅在 `Application.isPlaying` 时有效，非 PlayMode 会 Log "Enter Play Mode to preview animation."。

## 关键设计点

### 1. ChangeStartValue 避免 BuildTween 副作用

所有"位置/缩放/旋转"Node 都不直接 `Target.position = start`，而是用 `Sequence.AppendCallback(() => ...)` 包一层。**目的**：Editor 模式下 `BuildTween` 被反复调用（Inspector 重绘、可视化窗口刷新），直接改 Transform 会让 GameObject 在 Hierarchy 里跳来跳去。

### 2. Sequence 嵌套

`TTweenPlay.BuildTween` 内部已经是一个 `Sequence`，`TTweenTimeLine.BuildSequenceFromEntries` 又把它 `Insert` 到外层 `Sequence`。DOTween 支持无限嵌套。

### 3. SetLink + IgnoreTimeScale

```csharp
seq.SetAutoKill(false);   // 允许 Restart
seq.SetUpdate(_ignoreTimeScale);  // 不受 Time.timeScale 影响
seq.SetLink(gameObject);  // GameObject 销毁时自动 Kill
```

三条都是**最佳实践**：
- `SetAutoKill(false)`：支持 `Play()` 多次调用（每次 Kill + Rebuild）
- `SetUpdate(ignoreTimeScale)`：UI 动画/技能动画常需要暂停时仍动
- `SetLink`：GameObject 销毁自动清理，避免悬挂 Tween 引发的 NullRef

### 4. 默认动画值的选择

| Node | 默认 _fromValue | 默认 _targetValue | 默认 Ease |
| --- | --- | --- | --- |
| TTweenMove | (0,0,0) | (0,0,0) | OutQuad |
| TTweenMoveLocal | (0,0,0) | (0,0,0) | OutQuad |
| TTweenScale | (1,1,1) | (1,1,1) | **OutBack**（明显弹动） |
| TTweenRotate | (0,0,0) | (0,0,0) | OutQuad |
| TTweenFade | 0 | 1 | Linear |
| TTweenColor | white | white | Linear |
| TTweenPunch | strength = (1,1,1) | — | — |
| TTweenShake | strength = (1,1,1) | — | — |
| TTweenAnimatorState | normalizedTime = 0 | 1 | Linear |

设计意图：Scale 默认 `OutBack` 是"按钮按下回弹"等反馈动画的典型缓动，**所见即所得**。其他移动类用 `OutQuad`（平缓停下）。

### 5. Animator 接入的特殊性

`TTweenAnimatorState` 是最新（`9037ed8`）的 Node，**与 DOTween 的核心差异**：

- DOTween 动画可以**任意时刻 SetEase + Insert 偏移**
- Animator State 长度由 Controller 决定，**不能直接拼接到 Sequence**
- 必须用 `DOTween.To` 持续驱动 `normalizedTime` + `target.Update(0f)` 采样
- **必须冻结 Animator.speed** 避免双重推进

`5133b3f` 进一步让 `TryGetPlaybackDuration` 在 Editor 中可访问，UI 用 `EditorGUI.DisabledScope(true)` 灰显。

## 依赖与配置

### asmdef 引用

`TGame.Tween.asmdef` 引用：

- `Unity.Animation`（Animator 集成，间接通过 `UnityEngine.Animator`）
- `DOTween`（`com.demigiant.dotween`）

### Editor 程序集

窗口基类 / Inspector 增强全部用 `#if UNITY_EDITOR` 包裹，**运行时不包含** Editor 代码。

## 文件清单

| 操作 | 路径 |
| --- | --- |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/ITweenNode.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/TTweenPlay.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/TTweenTimeLine.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/TTweenPlayEditor.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/TTweenPlayWindow.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/TTweenTimeLineWindow.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/TTweenTimelineWindowBase.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/Nodes/TTweenMove.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/Nodes/TTweenMoveLocal.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/Nodes/TTweenScale.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/Nodes/TTweenRotate.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/Nodes/TTweenFade.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/Nodes/TTweenColor.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/Nodes/TTweenPunch.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/Nodes/TTweenShake.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/Nodes/TTweenCallback.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/Nodes/TTweenDelay.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/Nodes/TTweenAnimatorState.cs` |
| 新增 | `Assets/Plugins/TGame/Tween/Runtime/TGame.Tween.asmdef` |

## 后续注意

- **预览必须 PlayMode**：可视化窗口的 ▶ Play 按钮在非 PlayMode 时 Log "Enter Play Mode to preview animation."，这是**有意**避免在 Editor 模式运行 DOTween 引起状态污染。
- **别名 alias 必须手动设**：`NodeEntry.alias` 默认空，回退到 GameObject 名。如果同类型多个 Node 共享同一 GameObject 名（如 `Image (1)` / `Image (2)`），需手动设 alias 区分。
- **重复名文件**：`TTweenMove` / `TTweenMoveLocal` / `TTweenScale` 等模式一致，可考虑抽 `TransformTweenNode<TValue>` 泛型基类。当前 11 个 Node 复制粘贴 ~30 行模板代码。
- **TTweenAnimatorState State 名称变化**：如果改 Animator Controller 的 State 名，**已存在的 `TTweenAnimatorState` 组件的 `_stateName` 不会自动更新**，需要手动改。下拉框（`TTweenAnimatorStateNameDrawer`）可帮助发现错位。
- **DOTween 版本升级**：`com.demigiant.dotween` API 偶有破坏性变更（如 `DOColor` 签名在 v3 调整过），升级时需逐个 Node 验证。
- **BuildTween 多次调用的副作用**：所有用 `Sequence.AppendCallback(() => Target.position = start)` 包装的 Node 都不会在 BuildTween 时立刻改 Transform（因为 Callback 在 Sequence.Play() 时才触发）。**但如果业务方直接拿 `node.BuildTween()` 手动 Play，副作用照常发生**。
- **GetPlaybackDuration 在 Editor 模式可用**：`TTweenPlay.CalculateTotalDuration` 在 Editor 中用 `node.GetPlaybackDuration()` 估算总时长，`TTweenAnimatorState` 会**真的** Play + Update 一次 Animator（`TryGetStateLength`），有副作用（会改变 Animator 的 current state 一次）。后续 Play 时会恢复，但频繁打开窗口会触发 Animator 闪烁。
