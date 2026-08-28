# CODELY.md

Instructional context file for AI-driven development in this repository.

> **Also read:** [`CLAUDE.md`](CLAUDE.md) (detailed rules + build commands) and [`Agents.md`](Agents.md) (file encoding, tool precedence, workflow conventions).

---

## Project Overview

**TGame** is a Unity 6 game project built on a custom modular framework (also named "TGame"). The project uses URP rendering, Addressables for asset management, and a self-contained plugin architecture organized under `Assets/Plugins/TGame/`.

| Field | Value |
|---|---|
| **Engine** | Unity 6000.3.2f1 (Unity 6) |
| **Render Pipeline** | Universal Render Pipeline (URP) 17.3.0 |
| **Template** | URP Blank |
| **Product Name** | TGameV1 |
| **Entry Scene** | `Assets/Scenes/SampleScene.unity` |
| **Solution** | `UnityProject/TGameV1.sln` |
| **Unity Install Path** | `G:\Unity Editor\6000.3.2f1\` |

---

## Repository Layout

```
TGame/                         ← Git root
├── CLAUDE.md                  ← Detailed project rules (read this!)
├── Agents.md                  ← AI agent workflow conventions
├── CODELY.md                  ← This file
├── Build/                     ← Build output (empty / gitignored)
├── Document/
│   ├── AIDoc/                 ← AI-generated docs
│   │   ├── Analysis/          ← Code analysis, dependency graphs
│   │   ├── Bugs/              ← Bug records (delete on fix)
│   │   ├── Plan/              ← Implementation plans
│   │   ├── Snapshots/         ← Project state snapshots
│   │   └── Tasks/             ← Cross-session task tracking
│   └── Config/                ← Project configuration docs
├── Logs/
└── UnityProject/              ← Unity project root
    ├── Assets/
    │   ├── NuGet/             ← Third-party NuGet packages (UniTask, etc.)
    │   ├── Packages/          ← UPM packages
    │   ├── Plugins/TGame/     ← All custom TGame framework modules
    │   ├── Resources/
    │   ├── Scenes/            ← SampleScene.unity (entry)
    │   ├── Settings/          ← URP assets (PC/Mobile renderer + RP asset)
    │   └── TextMesh Pro/
    ├── Packages/
    │   └── manifest.json      ← UPM dependency manifest
    ├── ProjectSettings/
    └── *.csproj               ← Per-asmdef C# project files (dotnet build targets)
```

---

## TGame Plugin System

All custom framework code lives under `Assets/Plugins/TGame/`. Each subsystem has its own Assembly Definition (`.asmdef`) and compiles independently.

| Module | Directory | asmdef | Description |
|---|---|---|---|
| **TCore** | `TCore/Runtime/` | `TGame` | Root singleton `Game`, `BaseManager`, `BaseModule`, core modules (Event, Timer, ObjectPool), TDebug logging, GameBootstrapper |
| **TUI** | `TUI/Runtime/` | `TUI` | UI management: UIManager, UIRoot, BaseUIPanel, BaseUIPopup, UILoader, StackPanel, MVVM, addressable-based panel loading |
| **Tween** | `Tween/Runtime/` | `TGame.Tween` | TTweenPlay (player), TTweenTimeLine (timeline orchestrator), TTweenNode, UI Toolkit editor windows |
| **Console** | `Console/Runtime/` | `TGame.Console` | Runtime console with command registration (`CommandAttribute`), parsing, and execution |
| **SceneNavigator** | `SceneNavigator/Runtime/` | `SceneNavigator.Runtime` | SceneEntry markers, ScenePathAttribute, Editor drawer |
| **Addressable** | `Addressable/Runtime/` | `TGame.Addressable.Runtime` | Addressable asset management wrappers |
| **FSM** | `FSM/Runtime/` | — | Finite state machine (FSMControl, FSMState) |
| **GameSystem** | `GameSystem/` | — | GameSystemManager, GameTimeSystem |
| **ToolBox** | `ToolBox/Editor/` | `ToolBox` | Editor toolbox window (sidebar + content), Box auto-registration via UI Toolkit |
| **EditorToolBar** | `EditorToolBar/Editor/` | `TGame.EditorToolBar` | Custom editor toolbar extension |
| **Mobile** | `Mobile/SafeArea/` | — | Mobile SafeArea adaptation |
| **Common** | `Common/` | — | Shared utilities (e.g., LargeNumber) |
| **Sample** | `Sample/` | — | Example panels and demo scenes (HelloPanel, TweenPanel, etc.) |
| **Font** | `Font/` | — | Deng SDF font asset + 7000-character set |

### TCore Three-Layer Architecture

```
Game (MonoBehaviour singleton, DontDestroyOnLoad)
 └─ BaseManager (MonoBehaviour, IModuleHost)
     ├─ BaseModule (atomic functional unit, enable/disable/Init/Destroy/Tick)
     └─ BaseModule ...
```

- `Game` — Root singleton. Manages `BaseManager` registry via `AddManager<T>` / `GetManager<T>`.
- `BaseManager` — Manages `BaseModule` instances. Drives `FixedUpdate` tick for all modules. Implements `IModuleHost`.
- `BaseModule` — Atomic functionality. Lifecycle: `Init()` → `Tick(deltaTime)` (when `Enable`) → `Destroy()`.

Implemented core modules (in `TCore/Runtime/Module/`):
- **EventModule** — Type-safe event bus
- **TimerModule** — Named timers
- **ObjectPoolModule\<T\>** — Generic object pool
- **ModuleEntity** — Module entity wrapper

### TUI Architecture

```
UIManager (sealed BaseManager, DefaultExecutionOrder -7900)
 ├── AddressableModule<BaseUIPanel>  — Shared Addressable handle pool
 ├── UIRegistryModule                 — Global panel registry (Type → address)
 ├── UIRootManagerModule              — UIRoot management (by Type key)
 └── _uiRoot : UIRoot (IModuleHost)
      ├── UILoaderModule     — Addressable loading + instance cache + dedup
      ├── UILayerRootModule  — 7-layer root Transforms
      ├── StackPanelModule   — Stack-based navigation (Open/CloseTop/BackTo/PopToRoot)
      ├── PopupModule         — Popup window management
      └── UIVisibilityModule  — Show/Hide + event broadcast
```

- `BaseUIPanel` — UI panel base class with DOTween Sequence animation support and 4 lifecycle hooks
- `BaseUIPopup` — Popup window base class with anchor following and flip direction
- `UILoader` / `UILoaderByReference` — Panel loading strategies
- `StackPanelModel` — Stack-based panel management (validates `new.Layer >= top.Layer`)
- MVVM support under `TUI/Runtime/MVVM/`

### ToolBox Editor

UI Toolkit-based editor window with sidebar-content layout:
- Static groups (Program/Asset/Build), each containing multiple Boxes
- Box auto-registration via `BoxRegistration` static property (Name + Icon + Factory)
- `_windowGroup` serialized field restores Box list after recompilation
- Available Boxes: HelloBox, PathBox, DebugBox, ColorBox, AnimationCurveBox, BuildBox, ConsoleBox, SceneNavigatorBox

---

## Key Dependencies

### Unity Package Manager (from `manifest.json`)

| Package | Version | Purpose |
|---|---|---|
| `com.unity.render-pipelines.universal` | 17.3.0 | URP rendering pipeline |
| `com.unity.inputsystem` | 1.17.0 | New Input System (Active Input Handling: 1) |
| `com.unity.addressables` | 3.0.0 | Asset addressable management |
| `com.unity.ai.navigation` | 2.0.13 | NavMesh AI navigation |
| `com.unity.timeline` | 1.8.12 | Timeline sequence editing |
| `com.unity.visualscripting` | 1.9.9 | Bolt visual scripting |
| `com.unity.test-framework` | 1.6.0 | Unity Test Framework |
| `com.unity.ugui` | 2.0.0 | uGUI (Canvas) |
| `com.besty.unity-skills` | git | Unity Editor automation API |

### NuGet Packages (from `Assets/packages.config`)

| Package | Version | Purpose |
|---|---|---|
| **UniTask** | (via NuGet) | Async/await for Unity |
| **Newtonsoft.Json** | 13.0.4 | JSON serialization |
| **EPPlus** | 8.5.4 | Excel spreadsheet operations |
| **Microsoft.Extensions.Configuration** | 8.0.x | Configuration framework |

### Other Third-Party

- **DOTween** — Tween animation engine (used by TUI, Tween modules)
- **TextMesh Pro** — Text rendering (built-in with Unity)

---

## Building and Running

### Open the Project

1. Open Unity Hub
2. Add project from `D:\UnityProject\TGame\UnityProject\`
3. Open with Unity 6000.3.2f1 (installed at `G:\Unity Editor\6000.3.2f1\`)

### Quick C# Compilation Check (recommended after every code change)

**Do NOT build the full `.sln`** — it triggers 20-module compilation and is slow. Instead, build the specific module:

```bash
cd UnityProject
dotnet build <ModuleName>.csproj -nologo
```

Examples:
```bash
cd UnityProject
dotnet build TGame.csproj -nologo                    # TCore module
dotnet build TUI.csproj -nologo                       # TUI module
dotnet build TGame.Tween.csproj -nologo               # Tween module
dotnet build TGame.Console.csproj -nologo             # Console module
dotnet build ToolBox.csproj -nologo                   # ToolBox editor
dotnet build TGame.EditorToolBar.csproj -nologo       # EditorToolBar
```

Available `.csproj` files (one per asmdef):
`Assembly-CSharp`, `Assembly-CSharp-Editor`, `Assembly-CSharp-Editor-firstpass`, `Assembly-CSharp-firstpass`, `DOTween.Modules`, `SceneNavigator.Runtime`, `TFramework.Buff`, `TFramework.Framework.Runtime`, `TGame.Addressable.Runtime`, `TGame.Console`, `TGame`, `TGame.EditorToolBar`, `TGame.Tween`, `ToolBox`, `TUI`, `UniTask`, `UniTask.Addressables`, `UniTask.DOTween`, `UniTask.Editor`, `UniTask.Linq`, `UniTask.TextMeshPro`

**Pass criteria:** 0 errors + 0 `warning CS`. `MSB3277` warnings (package version conflicts) can be ignored.

Filter for code issues only:
```bash
cd UnityProject && dotnet build <Module>.csproj -nologo 2>&1 | grep -E "error CS|warning CS"
```
No output = passed.

**Limitations:** `dotnet build` checks syntax/type/reference errors only. It cannot validate runtime-only features like `UnityEditor.Toolbar` reflection calls or Unity internal APIs.

### Tests

- **Edit Mode Tests:** Unity Editor → Window → General → Test Runner → Run All
- **Play Mode Tests:** Same window, switch to PlayMode tab
- `dotnet test` may not work due to Unity runtime dependencies

### Build

Unity Editor → File → Build Profiles → Select target platform → Build

---

## Development Conventions

### File Encoding

- **All text files** (`.cs`, `.json`, `.md`, `.xml`, `.yml`, `.shader`, `.asset`, `.meta`, `.csproj`, `.sln`) use **UTF-8 without BOM**
- Line endings: **CRLF** (Windows checkout, `git config core.autocrlf`)
- Default to ASCII when creating/editing files; only use non-ASCII when there is a clear reason

### Code Style

- C# namespaces follow directory structure: `TGame.TCore.Runtime`, `TGame.TUI`, `TGame.TCore.Runtime.Module`, etc.
- `asmdef` `rootNamespace` is set per module (e.g., `TGame.TCore` for TCore, `TGame.TUI` for TUI)
- `[DefaultExecutionOrder]` attributes are used to control initialization order:
  - `Game`: `-9000`
  - `BaseManager` / `UIManager`: `-8000` / `-7900`
  - `GameBootstrapper`: `-100`
- `sealed` on leaf managers (e.g., `UIManager`)
- `partial` on extensible base classes (e.g., `Game`, `BaseManager`)
- Debug logging uses colored tags: `Debug.Log($"<color=#66ccff>[{GetType()}]</color> ...")`

### Assembly Definition (asmdef) Rules

- **Every new `.asmdef` or external dependency addition must verify `references` includes the required asmdef GUIDs.** Missing references cause compile errors.
- Reference existing modules (e.g., `TUI.asmdef`, `TGame.asmdef`) for known GUID sets.
- `autoReferenced: true` is the default — modules are automatically available to `Assembly-CSharp`.
- `includePlatforms: []` means all platforms (Runtime modules). Editor modules should restrict to Editor platform.

### Namespace Collision Prevention

When using `DG.Tweening.Tween` inside `namespace TGame.Tween`, **always use the fully qualified name** `DG.Tweening.Tween` to avoid resolution ambiguity with the namespace `TGame.Tween`.

### After Modifying .cs Files

1. Run `dotnet build <ModuleName>.csproj -nologo` from `UnityProject/`
2. Ensure 0 errors + 0 `warning CS` (ignore `MSB3277`)
3. In Unity Editor, press `Ctrl+R` to reimport if needed

### AI Documentation

All AI-generated documents go under `Document/AIDoc/`:
- `Analysis/` — Code analysis, dependency graphs, performance heatmaps
- `Tasks/` — Cross-session task state and progress
- `Snapshots/` — Project state snapshots for quick context recovery
- `Bugs/` — Bug records (delete the corresponding file after fix)
- `Plan/` — Implementation plans (save before execution, archive/delete when done)

### URP Settings

Located in `Assets/Settings/`:
- `PC_RPAsset.asset` / `PC_Renderer.asset` — PC render pipeline
- `Mobile_RPAsset.asset` / `Mobile_Renderer.asset` — Mobile render pipeline
- `DefaultVolumeProfile.asset` — Default volume profile
- `SampleSceneProfile.asset` — Sample scene volume profile
- `TDebugSettings.asset` — TDebug configuration

---

## Key Files Quick Reference

| File | Purpose |
|---|---|
| `CLAUDE.md` | Detailed project rules, build commands, architecture docs |
| `Agents.md` | AI agent workflow conventions, tool precedence, encoding rules |
| `UnityProject/Packages/manifest.json` | UPM dependency manifest |
| `UnityProject/Assets/packages.config` | NuGet package versions |
| `UnityProject/Assets/NuGet.config` | NuGet source configuration |
| `UnityProject/ProjectSettings/ProjectVersion.txt` | Unity version |
| `UnityProject/TGameV1.sln` | Visual Studio solution (do NOT build directly) |
| `Assets/Plugins/TGame/TCore/Runtime/Game.cs` | Root singleton `Game` |
| `Assets/Plugins/TGame/TCore/Runtime/TGame.BaseClass.cs` | `BaseManager`, `BaseModule`, `IModuleHost` |
| `Assets/Plugins/TGame/TUI/Runtime/UIManager.cs` | UI system entry point |
| `Assets/Plugins/TGame/TUI/Runtime/BaseUIPanel.cs` | UI panel base class |
| `Assets/Plugins/TGame/TCore/Runtime/Module/EventModule.cs` | Event bus module |
| `Assets/Plugins/TGame/TCore/Runtime/Module/TimerModule.cs` | Timer module |
| `Assets/Plugins/TGame/TCore/Runtime/Module/ObjectPoolModule.cs` | Object pool module |
| `Assets/Plugins/TGame/TCore/Runtime/Debug/TDebug.cs` | Debug logging system |
| `Assets/Plugins/TGame/Console/Runtime/ConsoleControl.cs` | Runtime console |
| `Assets/Plugins/TGame/FSM/Runtime/FSMControl.cs` | Finite state machine |
| `Assets/Plugins/TGame/GameSystem/GameSystemManager.cs` | Game system manager |
