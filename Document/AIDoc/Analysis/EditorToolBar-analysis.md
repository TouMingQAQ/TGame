# EditorToolBar 主工具栏插件分析

> 最近更新：2026-06-14，基于 master 分支 HEAD（1eb5fca）。
> 范围：`Assets/Plugins/TGame/EditorToolBar/`。

## 概览

| 项目 | 值 |
| --- | --- |
| 程序集 | `TGame.EditorToolBar`（Editor 平台） |
| 命名空间 | `TGame.EditorToolBar.BuiltIn` |
| 文件数 | 2 个 .cs + 1 个 .asmdef |
| 核心 API | Unity 6000 `UnityEditor.Toolbars.MainToolbarElement`（非 IMGUI Overlay） |
| 提交 | `0c1d616 refactor(EditorToolBar): 改用 Unity 6000 MainToolbarElement API 重做` |

## 设计目标

把"启动特定场景、运行指定场景"这类高频操作**塞进 Unity 主工具栏**（顶部 Play/Pause/Step 那行），免去每次走 `File → Build Profiles` 或 `SceneNavigatorBox` 的繁琐步骤。

演进历史：
- 早期版本用 IMGUI Overlay（`UnityEditor.Overlays.Overlay`）实现，但与 Unity 6000 主工具栏集成度差。
- 当前版本（`0c1d616`）改用 Unity 6000 官方 `MainToolbarElement` API，**直接注册到主工具栏**，位置可控（`defaultDockPosition = Middle, defaultDockIndex`）。

## 架构

```text
Unity 主工具栏 (Middle 区)
 ├─ [TGame/QuickLaunch/InitScene]   MainToolbarButton  (点开 InitScenePickerWindow)
 ├─ [TGame/QuickLaunch/Scene]       MainToolbarDropdown (列出 SceneNavigatorProfile.scenes)
 ├─ [TGame/QuickLaunch/Boot]        MainToolbarButton "▶ 启动" (用 InitScene 作 playModeStartScene)
 └─ [TGame/QuickLaunch/Run]         MainToolbarButton "▶ 运行" (用选中的目标场景)
```

四个 element **按 dockIndex 1→2→3→3 排序**：`Boot (1) → InitScene (2) → Scene (3) → Run (3)`，运行时根据 `MainToolbar.Refresh(path)` 重建按钮文字（如 InitScene 选完后变 `Init: <场景名>`）。

## 4 个核心 Element

| Element | 类型 | dockIndex | 行为 | 持久化 |
| --- | --- | --- | --- | --- |
| `InitScene` | `MainToolbarButton` | 2 | 点击 → `InitScenePickerWindow` 选 `SceneAsset` → 写 EditorPrefs + 重建按钮文字 | `EditorPrefs["SceneNavigator.InitScenePath"]` |
| `Scene` | `MainToolbarDropdown` | 3 | 点击 → 弹 `GenericMenu`，列出 `SceneNavigatorProfile.scenes`（alias 优先） | `EditorPrefs["TGame.QuickLaunch.TargetScenePath"]` |
| `Boot` | `MainToolbarButton` | 1 | 用 InitScene 作 playModeStartScene 启动；保存当前场景路径到 `EditorPrefs[GameBootstrapper.TargetSceneKey]`，运行时 `GameBootstrapper` 读它跳回 | `EditorPrefs[GameBootstrapper.TargetSceneKey]`（一次性） |
| `Run` | `MainToolbarButton` | 3 | 用选中的目标场景作 playModeStartScene 启动；不保存/不跳回 | 无 |

**Boot vs Run 区别**：
- **Boot** = 测"需要框架初始化的场景"（如带 `GameBootstrapper` 的 Init 场景），启动后由 `GameBootstrapper` 跳回原业务场景
- **Run** = 直接测某个业务场景，不走 Init 流程

## 启动 / 运行流程

```csharp
// 启动（Boot）
public static void RunBoot() {
    if (string.IsNullOrEmpty(_initScenePath)) { Debug.LogWarning(...); return; }
    var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(_initScenePath);
    if (asset == null) { Debug.LogError(...); return; }

    var currentScenePath = SceneManager.GetActiveScene().path;
    if (!string.IsNullOrEmpty(currentScenePath))
        EditorPrefs.SetString(GameBootstrapper.TargetSceneKey, currentScenePath);
    EditorSceneManager.playModeStartScene = asset;
    RegisterPlayModeCleanup();
    EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
}

// 运行（Run）— 类似但用 _targetScenePath，不写 TargetSceneKey
```

`RegisterPlayModeCleanup()` 订阅 `EditorApplication.playModeStateChanged`，**`EnteredEditMode` 时清**：
- `EditorPrefs[GameBootstrapper.TargetSceneKey]`
- `EditorSceneManager.playModeStartScene = null`

## 状态与持久化

**所有状态都是 `static` 字段**，因为 `MainToolbarElement` 是**静态方法注册**（`[MainToolbarElement]` 标注的方法不能持有实例字段）：

```csharp
private static string _initScenePath = LoadInitScenePath();
private static string _targetScenePath = EditorPrefs.GetString(TargetScenePathKey, "");
```

| 字段 | EditorPrefs Key | 用途 |
| --- | --- | --- |
| `_initScenePath` | `SceneNavigator.InitScenePath` | Init 场景路径 |
| `_targetScenePath` | `TGame.QuickLaunch.TargetScenePath` | 目标场景路径 |

**注意**：`SceneNavigator.InitScenePath` 这个 key **和 `SceneNavigatorBox` 用同一个 key**，意味着两个工具共享同一份 InitScene 设置——这是设计意图（ToolBar 和 ToolBox 入口都能改/读 InitScene）。

## 关键设计点

### 1. 自动创建 Profile 资产

`LoadProfile()` 在 `SceneNavigatorProfile` 不存在时自动创建：

```csharp
private static void EnsureProfileExists() {
    if (AssetDatabase.LoadAssetAtPath<SceneNavigatorProfile>(ProfileFullPath) != null) return;
    if (!AssetDatabase.IsValidFolder(ProfileResFolder)) {
        const string parent = "Assets/Plugins/TGame/SceneNavigator";
        if (!AssetDatabase.IsValidFolder(parent))
            AssetDatabase.CreateFolder("Assets/Plugins/TGame", "SceneNavigator");
        AssetDatabase.CreateFolder(parent, "Resources");
    }
    var profile = ScriptableObject.CreateInstance<SceneNavigatorProfile>();
    AssetDatabase.CreateAsset(profile, ProfileFullPath);
    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
}
```

**复用**：`SceneNavigatorBox.EnsureProfileExists()` 是**几乎一模一样**的实现——这里又出现重复。ToolBar 与 ToolBox 入口两处独立维护。

### 2. MainToolbar.Refresh 重建按钮

每次 `InitScene` 或 `Scene` 状态变化都调 `MainToolbar.Refresh(elementPath)`，让按钮文字更新（`Init: <场景名>`）。**只 refresh 单个 element**，不全局重建。

### 3. delayCall + isPlaying

```csharp
EditorSceneManager.playModeStartScene = asset;
RegisterPlayModeCleanup();
EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
```

**用 `delayCall` 推迟 1 帧再设 `isPlaying`**，避免在当前 Editor 事件循环中触发 Play 导致状态错乱。

## 依赖与配置

### asmdef 引用

`TGame.EditorToolBar.Editor.asmdef` 引用 2 个 GUID（来自 `.asmdef`）：

- `bb594cc2fdbcb4e4fa268fccdc7e5e9e` — `TGame.SceneNavigator.Runtime`
- `fdd566e303b577841905c151dd32fa26` — `TGame.TCore.Runtime`（`GameBootstrapper.TargetSceneKey`）

### MenuItem 入口

```csharp
[MenuItem("Tools/Editor ToolBar/Set Init Scene")]
public static void ShowMenu() => ShowWindow();
```

`InitScenePickerWindow` 也可独立通过菜单打开。

## 与 SceneNavigatorBox 的关系

`SceneNavigatorBox`（在 [ToolBox-analysis.md](ToolBox-analysis.md) 中详述）实现了**几乎同样**的功能：

| 功能 | EditorToolBar | SceneNavigatorBox |
| --- | --- | --- |
| Init 场景设置 | ✅ | ✅ |
| 启动按钮（跳回原场景） | ✅ Boot | ✅ 初始化并启动 |
| 目标场景下拉 | ✅ Scene | ✅ |
| 运行按钮 | ✅ Run | ✅ 运行此场景 |
| PlayMode 退出清理 | ✅ | ✅（更完整：主动 `OpenScene(savedPath)` 恢复） |
| Bootstrapper 校验 | ❌ | ✅（检测 init scene 和当前 scene 是否都有 Bootstrapper） |
| 搜索过滤 | ❌ | ✅ |

**重复点**：
- `EditorPrefs["SceneNavigator.InitScenePath"]` 共用 key
- `EnsureProfileExists()` 实现几乎一致
- `OnPlayModeStateChanged` 清理逻辑一致

**差异**：
- EditorToolBar 用 MainToolbarElement（顶部工具栏），SceneNavigatorBox 用 EditorWindow（侧边面板）
- SceneNavigatorBox 多一层校验（`SceneHasBootstrapper`）
- SceneNavigatorBox 支持搜索过滤

**维护建议**：两处功能高度重合，长期看可抽取一个 `QuickLaunchService` 静态类做单一来源；目前两处独立维护，需改同步改。

## 文件清单

| 操作 | 路径 |
| --- | --- |
| 新增 | `Assets/Plugins/TGame/EditorToolBar/Editor/Program/InitScenePickerWindow.cs` |
| 新增 | `Assets/Plugins/TGame/EditorToolBar/Editor/Program/QuickLaunchToolbar.cs` |
| 新增 | `Assets/Plugins/TGame/EditorToolBar/Editor/TGame.EditorToolBar.Editor.asmdef` |

## 后续注意

- **Unity 6.0+ 必需**：`MainToolbarElement` API 在 Unity 6000.0.x 引入，旧版本（如 2022 LTS）不支持。
- **Editor only**：asmdef `includePlatforms: ["Editor"]`，构建时不会进 runtime。
- **MainToolbarElement 静态约束**：注册方法必须 `public static`，且不能持有实例字段——所有状态用 `static` 字段 + `EditorPrefs` 持久化。
- **dockIndex 重叠**：`Scene` 和 `Run` 都是 `defaultDockIndex = 3`，实际顺序由注册顺序决定（`Scene` 先注册）。如要稳定顺序，需用不同 index。
- **PlayMode 退出清理**：`RegisterPlayModeCleanup` 先 `-=` 再 `+=` 防止重复订阅，**对静态订阅要小心**——多窗口场景下仍可能泄漏。
