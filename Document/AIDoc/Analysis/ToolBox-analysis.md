# ToolBox 编辑器工具箱分析

> 最近更新：2026-06-14，基于 master 分支 HEAD（1eb5fca）。
> 范围：`Assets/Plugins/TGame/ToolBox/`。
> BuildBox 由于功能自成体系（构建管线 + Profile 管理 + CLI），单独章节详述。

## 概览

| 项目 | 值 |
| --- | --- |
| 程序集 | `TGame.ToolBox`（Editor 平台） |
| 命名空间 | `TGame.ToolBox` |
| 文件数 | 17 个（16 个 .cs + 1 个 .uss） |
| 入口 | Tools/ToolBox/{程序/资源/构建} 三个菜单 + 主窗口 |
| 提交 | `c974e37 fix(ToolBox): 渲染修复（USS 变量作用域、ScrollView 嵌套、IMGUI 桥接、对比色等）` |

## 设计目标

把所有"开发中常用但 Unity 不自带"的小工具**收口到一个 UI Toolkit 窗口**，按"程序/资源/构建"分组侧边栏切换，避免散落各处。

演进：早期 IMGUI 实现 → 改用 UI Toolkit + `TwoPaneSplitView` 布局（左侧 Tab + 右侧内容）。

## 架构

```text
ToolBoxWindow (EditorWindow)
 ├─ _groups: Dictionary<groupKey, (title, boxes)>   ← 静态分组定义
 │   ├─ "程序" → HelloBox, PathBox, DebugBox, SceneNavigatorBox
 │   ├─ "资源" → ColorBox, AnimationCurveBox
 │   └─ "构建" → BuildBox
 ├─ ToolBoxWindow.uss                              ← 主题 + 变量
 └─ BoxRegistration                                ← 静态注册描述符
     ├─ Name / Group / Icon
     └─ Factory: () => VisualElement               ← 业务方自填
```

## 核心类型

### BoxRegistration

```csharp
public class BoxRegistration
{
    public string Name { get; set; }
    public string Group { get; set; }
    public string Icon { get; set; }                       // Unity 内置图标名,如 "Folder Icon"
    public Func<VisualElement> Factory { get; set; }       // 创建 Box 内容的工厂
}
```

### IToolBoxContentVisualElement

```csharp
public interface IToolBoxContentVisualElement
{
    VisualElement CreateContent();
}
```

业务方实现这个接口 + 提供 `static BoxRegistration Registration` 即可注册一个 Box。

> **历史备注**：早期版本（`ToolBoxAttribute` 装饰 + `[ToolBox("名称")]`）作为 Box 注册入口，**2026-06 已被删除**。`ToolBoxAttribute` 未被 `ToolBoxWindow` 实际读取（窗口通过 `_groups` 静态字典注册 Box），仅是装饰性。`[ToolBox("控制台", Order = 1)]` 装饰在 `ConsoleBox`、`SceneNavigatorBox` 上**无对应注册路径**，是死代码。业务方统一以 `static BoxRegistration Registration` 显式注册。

## 窗口实现

### 分组定义

`ToolBoxWindow._groups`（`ToolBoxWindow.cs:23-41`）是**静态硬编码**：

```csharp
private static readonly Dictionary<string, (string title, List<BoxRegistration> boxes)> _groups = new()
{
    ["程序"] = ("程序工具", new List<BoxRegistration>
    {
        HelloBox.Registration,
        PathBox.Registration,
        DebugBox.Registration,
        SceneNavigator.SceneNavigatorBox.Registration,
    }),
    ["资源"] = ("资源工具", new List<BoxRegistration>
    {
        ColorBox.Registration,
        AnimationCurveBox.Registration,
    }),
    ["构建"] = ("构建工具", new List<BoxRegistration>
    {
        BuildBox.Registration,
    }),
};
```

**3 个分组共 7 个 Box**。新增 Box 需要在 `_groups` 加一行。

### 菜单入口

```csharp
[MenuItem("Tools/ToolBox/程序")] private static void OpenProgram() => OpenGroup("程序");
[MenuItem("Tools/ToolBox/资源")] private static void OpenAssets()  => OpenGroup("资源");
[MenuItem("Tools/ToolBox/构建")] private static void OpenBuild()   => OpenGroup("构建");
```

`OpenGroup(groupKey)` 通过 `ScriptableObject.CreateInstance<ToolBoxWindow>()` 创建**独立窗口**（不是 `GetWindow` 单例），每个菜单对应一个新窗口。

### 布局与恢复

```text
TwoPaneSplitView (Horizontal, sidebar | content)
 ├─ Sidebar (ScrollView + Button 列表)
 └─ Content (header + ScrollView 包装 reg.Factory())
```

**内容恢复**靠 `[SerializeField] string _windowGroup` —— ScriptableObject 派生 `EditorWindow` 会序列化 `SerializeField` 字段，**Domain Reload 后**通过 `_windowGroup` 找到 `_groups` 字典重建 `_filteredBoxes`。

### 状态持久化

| 数据 | 存储 |
| --- | --- |
| 窗口所属分组 | `[SerializeField] _windowGroup` |
| Sidebar 宽度 | `EditorPrefs["ToolBox.SidebarWidth"]` |
| StyleSheet 路径 | 硬编码 `"Assets/Plugins/TGame/ToolBox/Editor/ToolBoxWindow.uss"` |

### USS 主题

`ToolBoxWindow.uss` 用 `.tbx-root` 类声明**USS 自定义属性**（Unity USS 不支持 `:root` 伪类）：

```css
.tbx-root {
    --tbx-bg-base:       rgb(40, 40, 42);
    --tbx-accent:         rgb(70, 125, 210);
    --tbx-text-primary:   rgb(215, 215, 220);
    --tbx-sp-3: 8px;
    --tbx-radius:    4px;
    ...
}
```

子类通过 inheritance 拿到变量。`Unity USS 不支持 padding/margin 简写`，必须拆成 4 个方向（如 `padding-left + padding-right + padding-top + padding-bottom`）—— 在 .uss 文件头部注释里特别提醒。

**已知坑**：Unity 早期版本中 `padding` 简写会被静默忽略。`c974e37` 修复主要是这一类问题。

## 7 个 Box 概览

| Box | Group | 主要功能 | 依赖 |
| --- | --- | --- | --- |
| `HelloBox` | 程序 | 渲染 `README.md`（自实现轻量 markdown 解析器） | `Assets/Plugins/TGame/ToolBox/Editor/HelloBox/README.md` |
| `PathBox` | 程序 | 列出 Unity 6 个常用路径（`dataPath`、`persistentDataPath` 等），可复制/打开 | `Application` 静态 API |
| `DebugBox` | 程序 | TDebug 控制面板（Settings SO + 运行时开关 + 上下文控制 + 文件日志） | `TGame.TCore.Runtime` (TDebug, TDebugSettings) |
| `SceneNavigatorBox` | 程序 | 场景启动/初始化（详见 [SceneNavigator-analysis.md](SceneNavigator-analysis.md)） | `TGame.SceneNavigator` + `TGame.TCore.Runtime` |
| `ColorBox` | 资源 | 内置色卡（CSS 命名 100+ 色）+ DIY 色卡持久化（`ColorLibrary.asset`） | `Assets/Resources/ColorLibrary.asset` |
| `AnimationCurveBox` | 资源 | 内置 13 种经典曲线（线性/缓入缓出/弹性/反弹/过冲等）+ DIY 曲线（`CurveLibrary.asset`） | `Assets/Resources/CurveLibrary.asset` |
| `BuildBox` | 构建 | 完整构建面板（详见 [BuildBox](#buildbox-构建管线) 章节） | `BuildConfig` + `BuildProfile` + 多个 IBuildPipeline |

### HelloBox 自实现 markdown 解析器

**注意**：`HelloBox` 用**自实现的 200 行 markdown 解析**（`HelloBox.cs:178-258`），不依赖任何第三方库。支持的语法有限：

- 标题 `# H1`, `## H2`
- 列表 `- item`, `* item`
- 表格 `| col1 | col2 |`
- 代码块 ` ``` `
- 粗体 `**bold**`, 斜体 `*italic*`
- 行内代码 `` `code` ``
- 分隔线 `---` / `***`

**不支持**：图片、链接、嵌套代码块、HTML 标签。如果 README 增长需要更完整 markdown，可考虑换 `Markdig` 库或简化文档。

### DebugBox 与 TDebug 集成

`DebugBox` 直接调用 `TGame.TCore.Runtime.TDebug` 静态 API：

```csharp
var enableToggle = new Toggle("启用日志") { value = TDebug.IsEnabled };
enableToggle.RegisterValueChangedCallback(evt => TDebug.SetEnable(evt.newValue));

var minLevelField = new IntegerField("最低 Level") { value = TDebug.GetMinLevel() };
minLevelField.RegisterValueChangedCallback(evt => TDebug.SetMinLevel(evt.newValue));

var contextTagField = new TextField("Tag") { value = TDebug.ContextTag ?? "" };
contextTagField.RegisterValueChangedCallback(evt => TDebug.SetTag(evt.newValue));
```

**TDebug** 是项目自实现的日志系统（`TCore/Runtime/Debug/`），本 Box 暴露配置入口。详见 [TCore-analysis.md](TCore-analysis.md)。

### ColorBox / AnimationCurveBox 持久化

两者模式一致：

```text
用户操作 → 修改 _library.Entries → EditorUtility.SetDirty → AssetDatabase.SaveAssetIfDirty
```

`ColorLibrary.asset` 和 `CurveLibrary.asset` 都是 `ScriptableObject`，放在 `Assets/Resources/` 下，业务代码可 `Resources.Load<ColorLibrary>("ColorLibrary")` 读取。

**ColorBox 棋盘格背景**（`ColorBox.cs:208-227`）—— 透明色（`alpha < 0.999`）显示棋盘灰底 + 透明 overlay，否则纯色填充。棋盘纹理 lazy 生成一次后缓存：

```csharp
private static Texture2D _checkerTex;   // 静态缓存
private static Texture2D GetCheckerTexture() { /* 生成 16x16 棋盘 RGBA32 */ }
```

## BuildBox 构建管线

BuildBox 是 ToolBox 中**最复杂**的 Box（1259 行），独立成子系统。提供 4 个 Foldout：

| Foldout | 包含 | 持久化 |
| --- | --- | --- |
| 版本信息 | Product/Company/Bundle/Unity 版本 + 各平台 build number | PlayerSettings + `BuildConfig.platformBuildNumbers` |
| 构建配置 | Profile 选择 + 目标平台 + 输出路径 + 6 个 BuildOptions Toggle | `BuildConfig` + `EditorPrefs`（输出路径、Toggle 状态） |
| AssetBundle 构建 | AB 输出路径 + 全量重建 Toggle + AB Pipeline 下拉 + 构建按钮 | `BuildConfig.abOutputPath` / `abFullRebuild` / `useCustomABPipeline` |
| Player 构建 | Player Pipeline 下拉 + 自增版本号警告 + 构建按钮 + 复制 CLI 指令 + 打开输出目录 | `BuildConfig.useCustomPipeline` / `selectedPipelineTypeName` |

### BuildConfig

```csharp
[CreateAssetMenu(fileName = "BuildConfig", menuName = "TGame/Build Config")]
public class BuildConfig : ScriptableObject
{
    public string outputPath = "Builds";
    public bool developmentBuild;
    public bool autoconnectProfiler;
    public bool deepProfiling;
    public bool buildScriptsOnly;
    public bool cleanBuild;                                  // 见下"CleanBuild 修复"
    public string abOutputPath = "Builds/AssetBundles";
    public bool abFullRebuild;
    public bool useCustomPipeline;
    public string selectedPipelineTypeName;                 // IBuildPipeline 类的 FullName
    public bool useCustomABPipeline;
    public string selectedABPipelineTypeName;                // IABBuildPipeline 类的 FullName
    public List<PlatformBuildNumber> platformBuildNumbers;
    public string lastProfileGUID;                           // 会话持久化
}
```

**存储位置**：`Assets/Resources/BuildConfig.asset`（默认），缺失时 `LoadConfig()` 自动创建。

### BuildProfile 体系（Unity 6 官方）

`BuildProfile` 是 Unity 6 新引入的 ScriptableObject，**替代**旧 `EditorUserBuildSettings.selectedBuildTargetGroup`：

```csharp
internal static class BuildProfileManager
{
    public static List<BuildProfile> GetAllProfiles();      // FindAssets 扫描 Assets/Settings/BuildProfiles/
    public static BuildProfile CreateProfile(string name);  // 在 ProfilesRoot 创建
    public static void DeleteProfile(BuildProfile profile);
    public static void RenameProfile(BuildProfile profile, string newName);
    public static string GetProfileGUID(BuildProfile profile);
    public static BuildProfile FindByGUID(string guid);
    public static BuildProfile FindByName(string name);
    public static string[] PlatformNames { get; } = { "Windows", "macOS", "Linux", "Android", "iOS", "WebGL" };
}
```

**目录约定**：`Assets/Settings/BuildProfiles/<name>.asset`，缺则自动建。

**BuildBox UI** 在 Profile Dropdown 旁提供"新建/重命名/删除"按钮（带确认对话框）。

### IBuildPipeline 插件体系

```csharp
public class BuildPipelineContext
{
    public BuildTarget buildTarget;
    public BuildTargetGroup buildTargetGroup;
    public string outputPath;
    public string productName;
    public bool developmentBuild;
    public bool autoconnectProfiler;
    public bool deepProfiling;
    public bool buildScriptsOnly;
    public bool cleanBuild;
    public string[] scenes;
    public BuildProfile profile;          // Unity 6 BuildProfile (可为 null 向下兼容)
}

public interface IBuildPipeline
{
    bool Execute(BuildPipelineContext ctx);
}

[AttributeUsage(AttributeTargets.Class)]
public class BuildPipelineAttribute : Attribute
{
    public string Name { get; }
    public BuildPipelineAttribute(string name) => Name = name;
}
```

业务方实现示例：

```csharp
[BuildPipeline("我的 Player 构建")]
public class MyPlayerBuildPipeline : IBuildPipeline
{
    public bool Execute(BuildPipelineContext ctx) {
        // ... 自定义构建逻辑
        return true;
    }
}
```

`BuildBox.DiscoverPipelines()` 用 `TypeCache.GetTypesWithAttribute<BuildPipelineAttribute>()` 全工程扫描，**无需手动注册**。

AB Pipeline 同样的体系（`IABBuildPipeline` + `ABBuildPipelineAttribute` + `ABBuildPipelineContext`），独立接口。

### CleanBuild 修复

**问题**：`BuildOptions.CleanBuild` 在 Unity 6 已移除（参考 [unity6-buildoptions-cleanbuild-removed.md](../../.claude/memory/.../unity6-buildoptions-cleanbuild-removed.md)）。

**修复**（`BuildBox.cs:1079-1086`, `BuildCLI.cs:80-88`）：**在构建前手动删输出**：

```csharp
if (_config.cleanBuild) {
    if (File.Exists(outputPath)) File.Delete(outputPath);
    else if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
}
```

CLI 模式下用相同逻辑（`BuildCLI.cs:80-88`）。

### 自增版本号

`AutoIncrementBuildNumberForPlatform(target, config)` 静态方法在 BuildBox 和 BuildCLI 复用：

| 平台 | 写入字段 |
| --- | --- |
| Android | `PlayerSettings.Android.bundleVersionCode++` |
| iOS | `PlayerSettings.iOS.buildNumber = (n+1).ToString()` |
| Windows/macOS/Linux/WebGL | `BuildConfig.platformBuildNumbers[platformName].buildNumber++` |

`BuildBox.RefreshPlatformBuildNumberEntries()` 确保所有平台都有条目（缺失则补 `buildNumber = 0`）。

### BuildCLI 命令行入口

```csharp
public static class BuildCLI
{
    public static void Build()  // Unity.exe -executeMethod TGame.ToolBox.BuildCLI.Build
}
```

**命令行参数**（大小写不敏感）：

| 参数 | 必需 | 说明 |
| --- | --- | --- |
| `-profile <GUID\|Name>` | 是 | 接受 GUID（32 字符 hex）、资产路径、Name 三种形式 |
| `-outputPath <path>` | 是 | 输出路径 |
| `-buildOptions Dev,Profiler,DeepProfiling,ScriptsOnly,CleanBuild` | 否 | 逗号分隔 |
| `-sceneList <a.unity,b.unity>` | 否 | 不传则用 `EditorBuildSettings.scenes` |
| `-autoIncrementBN 0\|1` | 否 | 是否自增版本号 |

**FindBuildProfile 查找顺序**：

1. 按 GUID（32 字符 hex）查找
2. 按资产路径（自动加 `.asset` 后缀）
3. 按 Name 全工程遍历

**BuildBox "复制 CLI 指令" 按钮** 自动根据当前 UI 状态生成 `Unity.exe -batchmode -quit -executeMethod ...` 命令并写入系统剪贴板，**业务方在 CI/本地终端直接粘贴执行**。

## 关键设计点

### 1. Box 自注册

业务方 Box 通过 `static BoxRegistration Registration` 属性自注册。ToolBoxWindow 启动时从 `_groups` 静态字典读（**集中维护**，非反射/Attribute 扫描），与 ToolBoxAttribute 解耦。

### 2. UI Toolkit + USS 变量作用域

Unity USS 不支持 `:root`，变量必须挂在某个类上。`ToolBoxWindow` 在 `rootVisualElement` 上加 `tbx-root` 类，所有变量在 `.tbx-root` 块声明，子树通过 inheritance 拿到。

### 3. BuildOptions.CleanBuild 已移除

参考 `unity6-buildoptions-cleanbuild-removed.md` —— Unity 6 删除了 `BuildOptions.CleanBuild`，BuildBox 和 BuildCLI 都改为**手动 `File.Delete / Directory.Delete`**。

### 4. BuildProfile 双向兼容

`BuildPipelineContext.profile` 可为 `null`，业务方实现 `IBuildPipeline` 时应判空。Unity 5 及更早工程升级时仍可工作（context.profile 为 null 时回退到 `BuildPipeline.BuildPlayer(BuildPlayerOptions)`）。

### 5. 双层 ScrollView 嵌套防护

`c974e37` 修复注释明确提到：**BuildBox 不再自己 new ScrollView**，因为外层 `ToolBoxWindow.ShowContent` 已包了一层 ScrollView，**双层嵌套会导致内层不出滚动条 / Foldout 折叠时抖动**。所有 Box 应只返回 `VisualElement`，由外层包 ScrollView。

## 依赖与配置

### asmdef 引用

**ToolBox 模块没有独立 `.asmdef`**（`Assets/Plugins/TGame/ToolBox/` 下不存在 `.asmdef` 文件），所有 .cs 进 Unity 默认的 `Assembly-CSharp-Editor-firstpass.csproj`。这意味着：

- 跨模块引用（如 `TGame.TCore.Runtime` 的 `TDebug`）通过全局 Assembly 而非显式 asmdef references
- 编译产物更大，但无需维护 asmdef GUID 列表
- 与同工程下 `TGame.SceneNavigator.Runtime` / `TGame.EditorToolBar` 等有独立 asmdef 的模块形成对比

模块间依赖（代码层面）：

- `DebugBox` → `TGame.TCore.Runtime.TDebug` / `TGame.TCore.Runtime.TDebugSettings`
- `SceneNavigatorBox` → `TGame.SceneNavigator` + `TGame.TCore.Runtime.GameBootstrapper`
- `ConsoleBox` → `TGame.ToolBox.IToolBoxContentVisualElement`（虽然**未在 `ToolBoxWindow._groups` 注册**，但接口实现保留）

### 自动创建 SO 资产

| Box | 资产 | 自动创建 |
| --- | --- | --- |
| ColorBox | `Assets/Resources/ColorLibrary.asset` | ✅（首次打开时） |
| AnimationCurveBox | `Assets/Resources/CurveLibrary.asset` | ✅（首次打开时） |
| DebugBox | `Assets/Settings/TDebugSettings.asset` | ✅（首次打开时） |
| BuildBox | `Assets/Resources/BuildConfig.asset` | ✅（首次打开时） |
| SceneNavigatorBox | `Assets/Plugins/TGame/SceneNavigator/Resources/SceneNavigatorProfile.asset` | ✅（首次打开时） |

**目录约定**：项目级 SO（Debug、Build、SceneNavigator）放 `Assets/Resources/`，便于 `Resources.Load` 访问。

## 文件清单

| 操作 | 路径 |
| --- | --- |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/ToolBoxWindow.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/BoxRegistration.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/IToolBoxContentVisualElement.cs` |
| ~~新增~~（2026-06 删除） | ~~`Assets/Plugins/TGame/ToolBox/Editor/ToolBoxAttribute.cs`~~ |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/ToolBoxWindow.uss` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/HelloBox/HelloBox.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/PathBox.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/DebugBox.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/ColorBox.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/ColorLibrary.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/AnimationCurveBox.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/CurveLibrary.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/BuildBox.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/BuildConfig.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/BuildProfileManager.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/IBuildPipeline.cs` |
| 新增 | `Assets/Plugins/TGame/ToolBox/Editor/BuildCLI.cs` |

## 后续注意

- **新增 Box 必须改 `_groups`**：当前 `_groups` 静态字典是 ToolBoxWindow 内集中维护，加 Box 后**别忘了同步加进对应分组**。
- **ToolBoxAttribute 已删除（2026-06）**：`ToolBoxWindow` 历史上从未通过 Attribute 扫描 Box，`[ToolBox("...")]` 装饰在 `ConsoleBox` / `SceneNavigatorBox` 上也无注册路径，是死代码。如未来需要"反射发现 Box"机制，可重建新的 Attribute + `_groups` 双轨方案。
- **BuildPipeline 反射发现**：`TypeCache.GetTypesWithAttribute<BuildPipelineAttribute>()` 在每次 `BuildBox.CreateContent()` 调一次（`DiscoverPipelines()`）。如果工程有几百个 Pipeline 类，**首次打开有反射开销**，建议加缓存。
- **BuildBox 不会自动应用 `BuildConfig` 改动到 SO**：所有 UI 控件直接改 `_config` 字段 + `EditorUtility.SetDirty`，但**只有调用 `BuildPlayer` 时才把 UI 状态同步到 SO**。中间改完不点构建就退出 Editor，**改动会丢**（虽然 EditorPrefs 也保存了一份兜底）。
- **IMGUI 桥接 BuildBox 内部**：`BuildBox.BuildPlayer` 走 `BuildPipeline.BuildPlayer(BuildPlayerWithProfileOptions)`，是 Unity 6 官方接口。
- **HelloBox README.md**：项目里**实际存在** `Assets/Plugins/TGame/ToolBox/Editor/HelloBox/README.md`（自实现 markdown 解析器渲染），描述 Box 开发指南和分组注册步骤。HelloBox 首次打开会读它解析成 UI。
- **ConsoleBox 未在 ToolBoxWindow 注册**：`TGame.Console.Editor.ConsoleBox` 实现了 `IToolBoxContentVisualElement` 接口，但 `ToolBoxWindow._groups` 字典中**没有**它，**也没有 `[MenuItem("Tools/ToolBox/控制台")]` 入口**——是预留但未完成的接入点。删除 `[ToolBox("控制台", Order = 1)]` 装饰后保留接口实现。
- **重复 Profile 自动创建**：`SceneNavigatorBox` 和 `EditorToolBar.QuickLaunchToolbar` 都有 `EnsureProfileExists` 副本代码，长期可抽公共静态类。
