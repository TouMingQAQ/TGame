# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在此仓库中工作时提供指导。

## 项目概览

- **引擎:** Unity 6000.3.2f1 (Unity 6)
- **渲染:** Universal Render Pipeline (URP) 17.3.0
- **模板:** URP Blank
- **产品名:** TGameV1
- **入口场景:** `Assets/Scenes/SampleScene.unity`

## 规则

- 读写代码文件（.cs、.json、.md 等）时，始终使用 UTF-8 编码（不带 BOM）
- **新增或修改代码时，必须检查 .asmdef 引用是否完整**：每新建一个 .asmdef 或向已有模块添加外部依赖（如 DOTween、UniTask）时，需确认其 `references` 字段包含了所需的 asmdef GUID。可参考已有模块（如 TUI.asmdef、TGame.asmdef）确认引用的 GUID 集合。缺少引用会导致编译错误。
- **每次修改 .cs 文件后，必须用 `dotnet build <ModuleName>.csproj` 校验**：进入 **0 错误 + 0 个 `warning CS`** 状态才能回复用户完成。命令在 `UnityProject/` 目录下执行,见"构建与测试"小节。**`MSB3277` warning(包版本冲突)可忽略**,与代码无关。
- 在 `namespace TGame.Tween` 等命名空间内使用 `DG.Tweening.Tween` 时，**必须使用完全限定名** `DG.Tweening.Tween`，避免与命名空间 `TGame.Tween` 产生解析歧义。
- AI 生成的文档统一存放于 [Document/AIDoc/](Document/AIDoc/) 目录下:
  - [Analysis/](Document/AIDoc/Analysis/) — 代码分析、依赖关系、性能热力图
  - [Tasks/](Document/AIDoc/Tasks/) — 跨会话任务状态、进度备忘
  - [Snapshots/](Document/AIDoc/Snapshots/) — 项目状态快照，快速恢复上下文
  - [Bugs/](Document/AIDoc/Bugs/) — 用户提交的 Bug，AI 标记解决状态，修复后删除对应 Bug 文件
  - [Plan/](Document/AIDoc/Plan/) — AI 实现计划文档，执行前保存，完成后归档或删除

## 构建与测试

通过 Unity Hub 在 Unity Editor 中打开项目。解决方案文件位于 [UnityProject/TGameV1.sln](UnityProject/TGameV1.sln)，但 C# 编译在 Editor 内部完成——**不要直接构建 .sln 文件**(会拉起 20 个模块全量编译,慢且易触发跨模块包冲突)。

- **Edit mode 测试:** Window → General → Test Runner → Run All（`dotnet test` 可能因 Unity 运行时依赖而不可用）
- **Play mode 测试:** 同一个 Test Runner 窗口，切换到 PlayMode 标签页
- **构建:** File → Build Profiles → 选择目标平台 → Build

### 快速 C# 编译检查（推荐：每次改完代码都跑）

```bash
cd UnityProject
dotnet build <ModuleName>.csproj -nologo
```

例如本次 EditorToolBar 插件:

```bash
cd UnityProject
dotnet build TGame.EditorToolBar.csproj -nologo
```

- Unity 6000.3.2f1 已安装于 `G:\Unity Editor\6000.3.2f1\`,引用程序集路径 `G:\Unity Editor\6000.3.2f1\Editor\Data\UnityReferenceAssemblies\unity-4.8-api\`
- 单模块编译约 2-5 秒，**0 错误才算通过**
- 区分两种 warning：**`warning CS` 是代码问题必须修**;**`MSB3277`(包版本冲突)与代码无关,可忽略**
- 一次性过滤出代码层问题:

  ```bash
  cd UnityProject && dotnet build TGame.EditorToolBar.csproj -nologo 2>&1 | grep -E "error CS|warning CS"
  ```

  无输出 = 0 错误 0 代码 warning,完成
- 适用场景:语法/类型/引用错误检查。**不能**验证:`UnityEditor.Toolbar` 等 internal 类型的反射调用(在普通 .NET 编译里能过,运行时 Unity 内部改名才会失败)
- 全模块编译请走 Unity Editor 内部 `Assets → Reimport All` 或 `Ctrl+R`

## 关键依赖

- `com.unity.inputsystem` (1.17.0) — 新版 Input System（已启用，Project Settings → Player → Active Input Handling: 1）
- `com.unity.render-pipelines.universal` (17.3.0) — URP 渲染管线
- `com.unity.visualscripting` (1.9.9) — Bolt 可视化脚本
- `com.unity.ai.navigation` (2.0.9) — NavMesh AI 导航
- `com.unity.timeline` (1.8.9) — Timeline 序列编辑器
- `com.unity.test-framework` (1.6.0) — Unity Test Framework

## 架构

### TGame 插件体系

自定义游戏框架代码均位于 `Assets/Plugins/TGame/` 下，按功能划分为以下子系统：

| 子系统 | 位置 | 说明 |
| --- | --- | --- |
| **TCore** | `TCore/Runtime/` | 核心框架：三层体系（Game → Manager → System/Module），事件总线、定时器、对象池 |
| **TUI** | `TUI/Runtime/` | UI 管理系统：UIManager + BaseUIPanel，支持 DOTween 动画生命周期 |
| **SceneNavigator** | `SceneNavigator/Runtime/` | 场景导航系统：SceneEntry 入口 + ScenePathAttribute 路径标记 |
| **TTween** | `Tween/Runtime/` | Tween 动画体系：TTweenPlay（播放器）+ TTweenTimeLine（时间线编排器）+ TTweenNode 子节点 + UI Toolkit 可视化窗口 |
| **TGame.Console** | `TGame.Console/Runtime/` | 运行时控制台：命令注册/解析/执行系统 |
| **ToolBox** | `ToolBox/Editor/` | Editor 工具箱窗口：侧边栏 + 内容区 UI Toolkit 面板 |
| **Mobile** | `Mobile/SafeArea/` | 移动端 SafeArea 适配 |
| **Debug** | `TCore/Runtime/Debug/` | 调试日志系统：TDebug + 文件写入 + 配置 |
| **Common** | `Common/` | 跨模块共享的工具代码 |

每个子系统有自己的 Assembly Definition (`.asmdef`)，独立编译。

### TCore 框架（三层体系）

```text
TGame (MonoBehaviour 单例, DontDestroyOnLoad)
 └─ BaseManager (MonoBehaviour, 可多个)
     ├─ BaseSystem (纯 C# 对象, 0..N)
     │   └─ BaseModule (功能模块, 0..N)
     └─ BaseModule (功能模块, 0..N)
```

| 层级 | 职责 | 关键操作 |
| --- | --- | --- |
| **TGame** | 根单例，Manager 注册表 | AddManager / GetManager |
| **BaseManager** | System + Module 管理，Unity 生命周期驱动 | AddSystem / GetSystem / GetModule / Call |
| **BaseSystem** | 子系统逻辑，拥有自己的 Module 集合 | 同 Manager 的 System/Module API |
| **BaseModule** | 原子功能单元，可启用/禁用 | Init / Destroy / Tick |

已实现模块：**EventModule**（类型安全事件总线）、**TimerModule**（命名定时器）、**ObjectPoolModule\<T\>**（泛型对象池）、**GameObjectPoolModule\<T\>**（MonoBehaviour 对象池）。

### ToolBox Editor 工具箱

基于 UI Toolkit 的 Editor 窗口，采用侧边栏-内容区布局：

- **分组定义**：代码内静态分组（程序/资源/构建），每个组包含多个 Box
- **Box 注册**：每个 Box 通过 `BoxRegistration` 静态属性自注册（Name + Icon + Factory）
- **内容恢复**：`_windowGroup` 序列化字段在脚本重编译后自动恢复 Box 列表
- **USS 样式**：通过 `ToolBoxWindow.uss` 自定义外观

可用 Box：HelloBox、PathBox、DebugBox、ColorBox、AnimationCurveBox、BuildBox（集成构建管线选择器）、ConsoleBox、SceneNavigatorBox。

### SceneNavigator 场景导航

- `SceneEntry` — 场景入口点标记（MonoBehaviour），记录场景路径
- `ScenePathAttribute` — 在代码中标记场景路径，Editor 通过 `ScenePathDrawer` 渲染
- `SceneNavigatorBox` — ToolBox 中的场景导航面板

### TGame.Console 控制台

- 运行时控制台 (`ConsoleControl`)，支持命令注册和执行
- `CommandAttribute` — 标记方法为控制台命令
- `CommandUtility` — 命令解析工具
- Editor 集成 (`ConsoleEditor` / `ConsoleBox`)

### TUI UI 系统

- `UIManager` — UI 层级管理（通过 `UILayer` 枚举区分层级）
- `BaseUIPanel` — UI 面板基类，提供 DOTween Sequence 动画支持和四个生命周期钩子
- `IUIPanel` — UI 面板接口
- `UIAnimationMaker` — UI 动画工具

### 目录规范

- 运行时代码：`Assets/Plugins/TGame/<Module>/Runtime/`
- Editor 专用代码：`Assets/Plugins/TGame/<Module>/Editor/`
- 预制体：`Assets/Plugins/TGame/<Module>/Runtime/Prefab/`
- URP 设置：`Assets/Settings/`（PC_RPAsset / Mobile_Renderer 配置）
- 第三方依赖包（NuGet/UniTask）：`Assets/NuGet/`
- 入口场景：`Assets/Scenes/SampleScene.unity`
