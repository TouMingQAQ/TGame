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
- AI 生成的文档统一存放于 [Document/AIDoc/](Document/AIDoc/) 目录下:
  - [Analysis/](Document/AIDoc/Analysis/) — 代码分析、依赖关系、性能热力图
  - [Tasks/](Document/AIDoc/Tasks/) — 跨会话任务状态、进度备忘
  - [Snapshots/](Document/AIDoc/Snapshots/) — 项目状态快照，快速恢复上下文
  - [Bugs/](Document/AIDoc/Bugs/) — 用户提交的 Bug，AI 标记解决状态，修复后删除对应 Bug 文件
  - [Plan/](Document/AIDoc/Plan/) — AI 实现计划文档，执行前保存，完成后归档或删除

## 构建与测试

通过 Unity Hub 在 Unity Editor 中打开项目。解决方案文件位于 [UnityProject/TGameV1.sln](UnityProject/TGameV1.sln)，但 C# 编译在 Editor 内部完成——不要直接构建 .sln 文件。

- **Edit mode 测试:** Window → General → Test Runner → Run All（`dotnet test` 可能因 Unity 运行时依赖而不可用）
- **Play mode 测试:** 同一个 Test Runner 窗口，切换到 PlayMode 标签页
- **构建:** File → Build Profiles → 选择目标平台 → Build

## 关键依赖

- `com.unity.inputsystem` (1.17.0) — 新版 Input System（已启用，Project Settings → Player → Active Input Handling: 1）
- `com.unity.render-pipelines.universal` (17.3.0) — URP 渲染管线
- `com.unity.visualscripting` (1.9.9) — Bolt 可视化脚本
- `com.unity.ai.navigation` (2.0.9) — NavMesh AI 导航
- `com.unity.timeline` (1.8.9) — Timeline 序列编辑器
- `com.unity.test-framework` (1.6.0) — Unity Test Framework

## 架构

这是一个全新的 Unity 项目，暂无自定义游戏代码。初始结构:

- [Assets/Scenes/](UnityProject/Assets/Scenes/) — 场景文件
- [Assets/Settings/](UnityProject/Assets/Settings/) — URP 管线资产（分别包含 `PC_RPAsset` 和 `Mobile_Renderer` 配置）
- [Assets/TutorialInfo/](UnityProject/Assets/TutorialInfo/) — 模板自带的 README 脚本（正式开发后可删除）

添加代码时遵循 Unity 标准约定:

- 脚本放在 `Assets/Scripts/`（需自行创建）
- 预制体放在 `Assets/Prefabs/`
- 运行时代码使用 `Assembly-CSharp`，Editor 专用代码使用 `Assembly-CSharp-Editor`；如需更精细的控制，创建 Assembly Definition 资产（`.asmdef`）
- URP 设置在 `Assets/Settings/` 中——在此调整渲染器特性和画质层级
