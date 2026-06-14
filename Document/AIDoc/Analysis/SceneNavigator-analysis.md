# SceneNavigator 场景导航分析

> 最近更新：2026-06-14，基于 master 分支 HEAD（1eb5fca）。
> 范围：`Assets/Plugins/TGame/SceneNavigator/`。

## 概览

| 项目 | 值 |
| --- | --- |
| 程序集 | `TGame.SceneNavigator.Runtime`（运行时） + SceneNavigator.Editor（在 SceneNavigatorBox 内） |
| 命名空间 | `TGame.SceneNavigator` |
| 文件数 | 5 个 .cs（3 Runtime + 2 Editor） |
| 核心资产 | `SceneNavigatorProfile.asset`（ScriptableObject） |
| 提交 | `db01bac`（面板栈）/ `76a71f1 fix(SceneNavigator): 修复检测 Bootstrapper 时关闭最后一个场景的错误` |

## 设计目标

让"启动某个业务场景"这件事**统一管理**：

- 不用每次都从 Project 窗口拖场景到 Hierarchy
- 一份 ScriptableObject 资产（`SceneNavigatorProfile`）集中管理"业务场景列表"
- 提供"初始化场景 → 启动 → 自动跳回原场景"工作流
- Editor 和 Toolbar 入口共享同一份配置

## 架构

```text
Runtime/
 ├─ SceneEntry                      (Serializable, scenePath + alias)
 ├─ ScenePathAttribute              (PropertyAttribute, Inspector 标记)
 └─ SceneNavigatorProfile           (ScriptableObject, List<SceneEntry>)

Editor/
 ├─ SceneNavigatorBox               (ToolBox 侧边面板,IToolBoxContentVisualElement)
 └─ ScenePathDrawer                 (CustomPropertyDrawer, [ScenePath] 字段渲染为 SceneAsset ObjectField)
```

**5 个类**全部围绕"场景路径 → ScriptableObject → 编辑器面板"这一条主线。

## 核心类型

### SceneEntry

```csharp
[Serializable]
public class SceneEntry
{
    [ScenePath] public string scenePath;   // 相对 Assets/ 的路径,如 "Assets/Scenes/Game.unity"
    public string alias;                   // 可选别名,优先于文件名显示
}
```

### ScenePathAttribute + ScenePathDrawer

`ScenePathAttribute` 是空属性，仅作标记：

```csharp
[AttributeUsage(AttributeTargets.Field)]
public class ScenePathAttribute : PropertyAttribute { }
```

`ScenePathDrawer` 是 `CustomPropertyDrawer`，把 `string` 字段渲染为 `SceneAsset` ObjectField：

```csharp
public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
    if (property.propertyType != SerializedPropertyType.String) {
        EditorGUI.PropertyField(position, property, label);
        return;
    }
    var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);
    EditorGUI.BeginChangeCheck();
    sceneAsset = EditorGUI.ObjectField(position, label, sceneAsset, typeof(SceneAsset), false) as SceneAsset;
    if (EditorGUI.EndChangeCheck()) {
        var path = AssetDatabase.GetAssetPath(sceneAsset);
        property.stringValue = path;
    }
}
```

业务方在自家 `ScriptableObject` 用法：

```csharp
[ScenePath] public string[] scenes;
```

### SceneNavigatorProfile

```csharp
[CreateAssetMenu(fileName = "SceneNavigatorProfile", menuName = "TGame/Scene Navigator")]
public class SceneNavigatorProfile : ScriptableObject
{
    public List<SceneEntry> scenes = new();
}
```

存储位置约定：`Assets/Plugins/TGame/SceneNavigator/Resources/SceneNavigatorProfile.asset`（必须放 `Resources/` 目录以便 `Resources.Load`）。

## SceneNavigatorBox（ToolBox 面板）

`SceneNavigatorBox : IToolBoxContentVisualElement` —— 沿用 [ToolBox](ToolBox-analysis.md) 的 Box 注册机制：

```csharp
[ToolBox("快捷启动", Order = 0)]
public class SceneNavigatorBox : IToolBoxContentVisualElement
{
    public static BoxRegistration Registration => new() {
        Name = "快捷启动", Group = "程序", Icon = "SceneAsset Icon",
        Factory = () => new SceneNavigatorBox().CreateContent()
    };
    ...
}
```

UI 结构：

```text
┌─────────────────────────────────┐
│ [搜索框] [<场景名> ▼]            │
├─────────────────────────────────┤
│ [▶ 运行此场景]                   │
│ (运行中: [■ 停止运行])            │
├─────────────────────────────────┤
│ 初始化启动                       │
│ [初始场景 ObjectField]            │
│ [▶ 初始化并启动]                 │
│ (Bootstrapper 校验提示)           │
└─────────────────────────────────┘
```

### 搜索过滤

```csharp
private void BuildFilteredList() {
    if (string.IsNullOrWhiteSpace(_searchText))
        _filtered = _profile.scenes.ToArray();
    else {
        var kw = _searchText.ToLower();
        _filtered = _profile.scenes
            .Where(s => (!string.IsNullOrEmpty(s.alias) && s.alias.ToLower().Contains(kw))
                     || (!string.IsNullOrEmpty(s.scenePath) && s.scenePath.ToLower().Contains(kw)))
            .ToArray();
    }
}
```

匹配 `alias` 或 `scenePath`，**忽略大小写**。

### 运行流程（RunSelectedScene）

```csharp
private void RunSelectedScene() {
    var entry = _filtered[_selectedIndex];
    if (string.IsNullOrEmpty(entry.scenePath)) return;
    var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.scenePath);
    if (sceneAsset == null) { Debug.LogError(...); return; }

    _prePlayScenePath = SceneManager.GetActiveScene().path;
    EditorSceneManager.playModeStartScene = sceneAsset;
    EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
}
```

**退出 PlayMode 时**（`OnPlayModeStateChanged`）自动恢复原场景：

```csharp
private static void OnPlayModeStateChanged(PlayModeStateChange state) {
    if (state == PlayModeStateChange.EnteredEditMode) {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorPrefs.DeleteKey(GameBootstrapper.TargetSceneKey);

        var savedPath = _prePlayScenePath;
        _prePlayScenePath = null;
        EditorSceneManager.playModeStartScene = null;

        if (!string.IsNullOrEmpty(savedPath)) {
            EditorApplication.delayCall += () => {
                if (SceneManager.GetActiveScene().path != savedPath)
                    EditorSceneManager.OpenScene(savedPath);
            };
        }
    }
}
```

### 初始化启动（RunInitScene + GameBootstrapper 联动）

与 `RunSelectedScene` 类似，但用 `GameBootstrapper.TargetSceneKey` 传递"原场景路径"给运行时 `GameBootstrapper` 组件（`TCore/Runtime/GameBootstrapper.cs`），由它在 PlayMode 启动时跳回。

**Bootstrapper 校验**（`ValidateInitScene` + `SceneHasBootstrapper`）—— 这是 SceneNavigatorBox **独有**的能力，EditorToolBar 没有：

```csharp
private void ValidateInitScene() {
    if (EditorApplication.isPlaying) { /* 播放模式不可用 */ return; }
    if (_initSceneAsset == null) { /* 提示选择 */ return; }

    var initHasBootstrapper = SceneHasBootstrapper(path);
    var currentHasBootstrapper = SceneHasBootstrapper(SceneManager.GetActiveScene().path);

    _initSceneValid = initHasBootstrapper && !currentHasBootstrapper;
    _initBootButton?.SetEnabled(_initSceneValid);
    // 提示信息根据校验结果切换
}
```

校验 3 个状态：

| 初始场景 | 当前场景 | 按钮状态 | 提示 |
| --- | --- | --- | --- |
| 有 Bootstrapper | 无 Bootstrapper | 启用 | "检测到 GameBootstrapper，初始化完成后自动跳回当前场景。" |
| 有 Bootstrapper | 有 Bootstrapper | 禁用 | "当前场景也包含 GameBootstrapper，加载后会导致循环，请切换场景后重试。" |
| 无 Bootstrapper | 任意 | 禁用 | "初始场景中未检测到 GameBootstrapper 组件，将不会执行初始化跳转。" |

### SceneHasBootstrapper 的关键修复（`76a71f1`）

早期实现用 `EditorSceneManager.OpenScene(path) + CloseScene(scene)` 切换检测，但**关闭最后一个场景会触发 "Unloading the last loaded scene ... is not supported" 错误**。

修复（`SceneNavigatorBox.cs:395-424`）：**复用已加载场景**避免 Open+Close：

```csharp
private static bool SceneHasBootstrapper(string scenePath) {
    if (EditorApplication.isPlaying) return false;

    // 若场景已加载（包括当前 active 场景），复用已有引用
    var existing = EditorSceneManager.GetSceneByPath(scenePath);
    bool wasAlreadyLoaded = existing.IsValid() && existing.isLoaded;

    var scene = wasAlreadyLoaded
        ? existing
        : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

    var found = false;
    foreach (var rootGO in scene.GetRootGameObjects())
        if (rootGO.GetComponentInChildren<GameBootstrapper>(true) != null) { found = true; break; }

    // 仅当我们临时打开了它、且不是唯一场景时才关闭
    if (!wasAlreadyLoaded && EditorSceneManager.sceneCount > 1)
        EditorSceneManager.CloseScene(scene, true);

    return found;
}
```

## 自动创建 Profile 资产

`EnsureProfileExists()` 在 Profile 不存在时**自动创建**：

```csharp
private static void EnsureProfileExists() {
    var fullPath = $"{ResFolder}/{ResFileName}.asset";
    if (AssetDatabase.LoadAssetAtPath<SceneNavigatorProfile>(fullPath) != null) return;

    if (!AssetDatabase.IsValidFolder(ResFolder)) {
        var parent = "Assets/Plugins/TGame/SceneNavigator";
        if (!AssetDatabase.IsValidFolder(parent))
            AssetDatabase.CreateFolder("Assets/Plugins/TGame", "SceneNavigator");
        AssetDatabase.CreateFolder(parent, "Resources");
    }

    var profile = ScriptableObject.CreateInstance<SceneNavigatorProfile>();
    AssetDatabase.CreateAsset(profile, fullPath);
    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
}
```

**注意**：`QuickLaunchToolbar`（[EditorToolBar-analysis.md](EditorToolBar-analysis.md)）和 `SceneNavigatorBox` 都有**几乎一致**的 `EnsureProfileExists` 实现，两处独立维护。

## 与 GameBootstrapper 的协作

| 组件 | 职责 |
| --- | --- |
| `GameBootstrapper` (TCore) | Editor 启动时从 `EditorPrefs[TargetSceneKey]` 读目标场景路径，**有就加载并 DeleteKey**，无就 LogWarning 跳过。静态标志 `_hasBootstrapped` 防循环加载。`#if UNITY_EDITOR` 包裹，生产环境无效。 |
| `SceneNavigatorBox` | Editor 把"原场景路径"写到 `EditorPrefs[TargetSceneKey]`，启动后清理。 |
| `QuickLaunchToolbar` (EditorToolBar) | 同上。 |

`GameBootstrapper.TargetSceneKey = "TGame_InitBoot_TargetScene"` —— **三处共享**的常量。

## 关键设计点

### 1. 双入口共享配置

`SceneNavigatorBox`（ToolBox 侧边面板）和 `QuickLaunchToolbar`（主工具栏）功能高度重合，通过共享 `SceneNavigatorProfile` + `EditorPrefs["SceneNavigator.InitScenePath"]` 保持一致。详见 [EditorToolBar-analysis.md](EditorToolBar-analysis.md) 对比表。

### 2. EditorPrefs 而非 ScriptableObject 存 InitScene

`InitScene` 用 `EditorPrefs["SceneNavigator.InitScenePath"]` 存（不是 Profile 字段），理由：

- InitScene 是"个人开发偏好"，不应进版本控制
- Profile 是"项目级场景列表"，进版本控制

### 3. Bootstrapper 校验防止循环加载

`GameBootstrapper` 自身有 `_hasBootstrapped` 静态标志兜底，但**更早一步**在 Editor 端做校验，**避免进入 PlayMode 后才发现循环**——是更友好的 UX。

## 依赖与配置

### asmdef 引用

`SceneNavigator.Runtime.asmdef` 无外部引用（Runtime 层），纯 Unity API。

`SceneNavigatorBox.cs` 引用：

- `TGame.TCore.Runtime`（`GameBootstrapper`）
- `TGame.ToolBox`（`IToolBoxContentVisualElement`）

## 文件清单

| 操作 | 路径 |
| --- | --- |
| 新增 | `Assets/Plugins/TGame/SceneNavigator/Runtime/SceneEntry.cs` |
| 新增 | `Assets/Plugins/TGame/SceneNavigator/Runtime/ScenePathAttribute.cs` |
| 新增 | `Assets/Plugins/TGame/SceneNavigator/Runtime/SceneNavigatorProfile.cs` |
| 新增 | `Assets/Plugins/TGame/SceneNavigator/Editor/SceneNavigatorBox.cs` |
| 新增 | `Assets/Plugins/TGame/SceneNavigator/Editor/ScenePathDrawer.cs` |
| 新增 | `Assets/Plugins/TGame/SceneNavigator/Runtime/SceneNavigator.Runtime.asmdef` |

## 后续注意

- **Profile 必须放 Resources/**：`SceneNavigatorProfile` 用 `Resources.Load<SceneNavigatorProfile>("SceneNavigatorProfile")` 加载，路径必须是 `Assets/Plugins/TGame/SceneNavigator/Resources/SceneNavigatorProfile.asset`。
- **`ScenePath` 字段路径会进版本控制**：业务方的 `ScriptableObject` 用 `[ScenePath] public string[] scenes` 时，路径字符串是序列化数据，跨分支合并需小心。
- **`_prePlayScenePath` 静态字段在多 SceneNavigatorBox 实例时共享**：目前 ToolBox 单例，无问题；如未来允许多实例，需改为实例字段。
- **`GameBootstrapper` 仅 Editor 有效**：生产环境无 Bootstrapper，SceneNavigatorBox 校验也只在 Editor 运行。
- **Bootstrapper 校验打开场景会触发 Hierarchy 闪烁**：用 `OpenSceneMode.Additive` 临时加载，关闭后恢复，避免主场景重载。但频繁校验仍会有视觉抖动。
- **重复实现**：`EnsureProfileExists` 在 ToolBar 和 ToolBox 入口两处独立维护，长期看应抽取公共静态类。
