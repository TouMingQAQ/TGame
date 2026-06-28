# Recent Git Commits Code Review - 2026-06-28

## Scope

Reviewed recent commits on `master`:

- `fa6a9de` - `feat(TUI): 重构资产注册与预热流程, UIRegistryModule 从 per-UIRoot 升至 UIManager 全局共享`
- `a4d6f35` - `fix(FSM): 修复状态切换/生命周期一致性与空白清理`
- `66711c9` - `refactor(Addressable): 合并 AddressableModel.SetManager 重载为单一 BaseManager 入口`
- Spot-checked `6979539` as context for Addressable/UI loader concurrency.

Excluded `3fddf27 添加Unity Skills` from detailed review because it is tooling/instruction material rather than runtime game code.

## Findings

### P0 - UIRoot can fail in Awake because UIManager is registered only in Start

Evidence:

- `Assets/Plugins/TGame/TUI/Runtime/UIRoot.cs:74-78`
- `Assets/Plugins/TGame/TUI/Runtime/UIManager.cs:43-49`
- `Assets/Plugins/TGame/TCore/Runtime/Game.cs:19-31`

`UIRoot.Awake()` calls `Game.Instance.GetManager<UIManager>()` and immediately dereferences `Owner`. But `UIManager` calls `game.AddManager(this)` in `Start()`. Unity runs `Awake` before `Start`, so a `UIRoot` on the same prefab/scene can run before `UIManager.Start()` has registered the manager. `Game.GetManager<UIManager>()` then returns `default`, and `Owner.GetModule<UIRootManagerModule>()` throws.

Suggested fix:

- Register `UIManager` in `Awake` or earlier, then initialize `_uiRoot` after registration.
- Prefer explicit initialization, e.g. `UIRoot.Init(UIManager owner, UIConfig config)`, instead of having `UIRoot` discover its owner through `Game.Instance` during `Awake`.

### P1 - RemoveState<T> now removes every FSM state, not just T

Evidence:

- `Assets/Plugins/TGame/FSM/Runtime/FSMControl.cs:239-248`
- `Assets/Plugins/TGame/FSM/Runtime/FSMControl.cs:256-270`

`RemoveState<T>()` checks that the requested state exists, but then calls `ClearStates(exitCurrent)`, and `ClearStates` iterates all states and calls `states.Clear()`. Removing one optional state therefore unregisters the default state and every other state too. After this, unrelated `ChangeState<X>()` calls fail because the whole registry is gone.

Suggested fix:

- Split "remove one state" from "clear all states".
- `RemoveState<T>()` should remove only `typeof(T)`, call `OnExit` only if that state is current, call `OnRemove`, clear that instance's `Control`, and leave the rest of `states` intact.

### P1 - UIConfig default tooltip is no longer wired, so DefaultToolTipTrigger cannot show

Evidence:

- `Assets/Plugins/TGame/TUI/Runtime/UIManager.cs:31`
- `Assets/Plugins/TGame/TUI/Runtime/UIConfig.cs:18-36`
- `Assets/Plugins/TGame/TUI/Runtime/UIComponent/DefaultToolTipTrigger.cs:29-37`
- `Assets/Plugins/TGame/TUI/Runtime/Model/PopupModule.cs:169-172`

`UIManager` still serializes `_config`, and `UIConfig` still documents that default tooltip should be auto-registered. In the new implementation `_config` is never read. `DefaultToolTipTrigger` calls `_ui.UIRoot.ShowPopup<DefaultToolTip>()`, but `PopupModule` has no registered `DefaultToolTip`, so it logs "Popup DefaultToolTip not registered" and returns null.

Suggested fix:

- During UIRoot initialization, call `Popup.SetDefaultOffset(_config.TooltipOffset)` and `Popup.Register(_config.DefaultTooltip)` when present.
- Consider keeping a public `RegisterPopup<T>` forwarding API on `UIManager` or `UIRoot` for non-default popups.

### P1 - Public hide-panel API was removed and the remaining Hide(Type) path is a no-op

Evidence:

- `Assets/Plugins/TGame/TUI/Runtime/UIManager.cs:64-93`
- `Assets/Plugins/TGame/TUI/Runtime/UIRoot.cs:141-146`
- `Assets/Plugins/TGame/TUI/Runtime/Model/UIVisibilityModule.cs:71-87`

Before `fa6a9de`, `UIManager` exposed `HidePanel<T>()` / `HidePanel(Type)`. The new `UIManager` and `UIRoot` expose load/show/unload/query but no hide-panel wrappers. `UIVisibilityModule.Hide(Type)` remains as an empty placeholder. This is a source/API regression for package consumers and makes the non-stack show/hide lifecycle one-way unless callers directly get the panel and call `Hide()`, which bypasses the intended event module path.

Suggested fix:

- Restore `UIManager.HidePanel<T>()`, `UIManager.HidePanel(Type)`, `UIRoot.HidePanel<T>()`, and `UIRoot.HidePanel(Type)`.
- Implement them as `var panel = GetPanel(type); Visibility.Hide(panel);` so `PanelClosedEvent` is still emitted.

### P2 - FSM ClearStates only clears Control for default-state type instances

Evidence:

- `Assets/Plugins/TGame/FSM/Runtime/FSMControl.cs:263-268`

Inside `FSMControl<TDefault>.ClearStates`, the cleanup tries `kv.Value is FSMState<TDefault> typedState`. That succeeds only for states whose generic self-type is exactly `TDefault`; every other registered `FSMState<TOther>` keeps its old `Control` reference after `Init()` or `RemoveState<T>()`.

Suggested fix:

- Add an internal cleanup method to `IFSMState`/`FSMState<T>` or another non-generic internal interface that can clear `Control` polymorphically.
- Then `ClearStates` and `RemoveState<T>` can clear every removed state consistently.

### P3 - Latest TUI commit introduces diff-check noise

Evidence:

- `git diff --check fa6a9de^ fa6a9de`

The latest TUI commit introduces trailing whitespace in generated Addressables assets/meta files, `ModuleEntity.cs`, `TGame.BaseClass.cs`, `StackSubPanel.cs`, and prefabs. Some Unity YAML whitespace may be generated and harmless, but the script/code whitespace is avoidable and makes future diff checks noisy.

Suggested fix:

- Clean script trailing whitespace.
- Decide whether Unity-generated `.asset`/`.prefab` trailing blanks should be ignored in tooling or normalized before commit.

## Positive Notes

- `TUI.csproj` builds cleanly after the latest TUI refactor.
- The new `ModuleEntity` direction is coherent for per-UIRoot state isolation, and its `Update` tick does give `PopupModule.Tick` a host after the migration.
- `AddressableModule` now uses `BaseModule.Host` for event dispatch; the removed `SetManager` field does not appear to cause compile/runtime ownership issues by itself.
- The UI loader's per-UIRoot cache plus shared `AddressableModule` direction is a good separation: instance lifecycle stays local, asset handle lifecycle is shared.

## Verification

Commands run:

- `git -C "D:\UnityProject\TGame" log --oneline --decorate -n 8`
- `git -C "D:\UnityProject\TGame" status --short`
- `dotnet build "D:\UnityProject\TGame\UnityProject\TUI.csproj" --no-restore`
- `dotnet build "D:\UnityProject\TGame\UnityProject\Assembly-CSharp-firstpass.csproj" --no-restore`
- `git -C "D:\UnityProject\TGame" diff --check fa6a9de^ fa6a9de`

Build result:

- `TUI.csproj`: 0 errors, 0 warnings.
- `Assembly-CSharp-firstpass.csproj`: 0 errors, 5 warnings. Warnings are pre-existing console unused/async warnings plus `System.Net.Http` version conflict from Unity/Addressables references.

