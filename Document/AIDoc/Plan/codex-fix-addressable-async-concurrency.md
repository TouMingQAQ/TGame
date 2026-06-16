# Codex 任务：修复 Addressable 异步加载的并发缺陷

> **仓库**：TouMingQAQ/TGame
> **评审范围**：最近一条 feature 提交 `dcfaecd feat(TUI): UIManager 接入 AddressableModel 异步加载 UIPanel`（及其引入的 `a2683dd` Addressable 体系）
> **建议落库位置**：`Document/AIDoc/Plan/`（按 AGENTS.md / CLAUDE.md 约定，AI 实现计划存于此目录）
> **本文件编码**：UTF-8 **无 BOM**（.md 规则）；改动的 `.cs` 必须保持 **UTF-8 带 BOM**（.cs 规则）

---

## 0. 执行约定（Codex 必须遵守）

来自 `CLAUDE.md` / `AGENTS.md`，逐条照做：

1. **编码**：`.cs` 一律 UTF-8 **带 BOM**；不要把中文注释/字符串改成乱码。
2. **编译校验**：每改完一个 `.cs`，在 `UnityProject/` 下跑 `dotnet build <Module>.csproj`，必须 **0 error + 0 `warning CS`** 才算完成。`MSB3277`（包版本冲突）与代码无关，可忽略。
3. **不要直接 build 整个 `.sln`**（会拉起 20 个模块全量编译）。
4. **`.asmdef` 引用**：本任务**不新增外部依赖**（UniTask / TGame.Addressable.Runtime 引用在 `dcfaecd` 已加好），无需改 asmdef。若你的改动确实引入新依赖，再补 GUID。
5. **不改公共 API 签名**（除任务 3 明确允许的形参清理），保持调用方零迁移。

涉及文件与对应工程：

| 文件 | csproj |
| --- | --- |
| `UnityProject/Assets/Plugins/TGame/Addressable/Runtime/Model/AddressableModel.cs` | `TGame.Addressable.Runtime.csproj` |
| `UnityProject/Assets/Plugins/TGame/TUI/Runtime/Model/UILoaderModule.cs` | `TUI.csproj` |

---

## 1. 背景

`dcfaecd` 给 `AddressableModel` 接了一套 UniTask 异步加载 + 句柄池（引用计数），并让 `UIManager` 通过它异步加载 UIPanel。核心加载流程 `AddressableModel.LoadInternalAsync` 设计了「同一 key 并发去重」：第一个请求真正加载，后到者复用结果。**但去重的实现用错了 UniTask 原语，导致后到者在正常成功路径下永久挂起。** UI 层 `UILoaderModule.LoadAsync` 同样缺少并发去重，会造成实例泄漏 + 引用计数泄漏。

---

## 2. 问题清单

| # | 优先级 | 位置 | 一句话 |
| --- | --- | --- | --- |
| 1 | **P0** | `AddressableModel.cs:230-243` | 并发同 key 加载时，后到者 `await WaitUntilCanceled()` 永不被唤醒（该 awaitable 只在**取消**时完成，不在**成功**时完成）→ 调用链死等 |
| 2 | **P1** | `UILoaderModule.cs:133-196` | `LoadAsync(Type)` 在 `await` 前查缓存、`await` 后写缓存，中间无 in-flight 去重 → 并发同类型面板 → 一个 GameObject 成孤儿 + Addressable 引用计数泄漏 |
| 3 | **P2** | `AddressableModel.cs:314-374` | `PreloadInternalAsync` 的 `IProgress<float> progress` 形参从未被调用，也从不广播 `AddressablePreloadProgressEvent`，与类注释承诺不符 |

---

## 任务 1  — 【P0】AddressableModel 并发同 key 加载死等

### 1.1 位置
`UnityProject/Assets/Plugins/TGame/Addressable/Runtime/Model/AddressableModel.cs`
方法 `LoadInternalAsync<T>`，第二段「并发防重入」分支（约 230-243 行）：

```csharp
// 2. 并发防重入：若已有该 key 的进行中加载，等待其 CTS 取消或完成后重试句柄池
if (_loadingMap.TryGetValue(addrKey, out var existingCts))
{
    try { await existingCts.Token.WaitUntilCanceled(); } catch { /* cancelled */ }
    // 重试句柄池查重
    if (_handles.TryGetValue(addrKey, out var joinEntry) && joinEntry.Handle.IsValid())
    {
        joinEntry.RefCount++;
        _handles[addrKey] = joinEntry;
        BroadcastTarget?.Call(new AddressableLoadCompletedEvent(addrKey.Key, addrKey.AssetType, true, 0));
        return joinEntry.Handle.Result as T;
    }
    // 原加载取消/失败后句柄未写入，回退到新加载
}
```

### 1.2 根因（已核验 UniTask 源码）

`existingCts.Token.WaitUntilCanceled()` 解析到 `UniTask/Runtime/CancellationTokenExtensions.cs` 的扩展方法，返回 `CancellationTokenAwaitable`，其 awaiter：

```csharp
public bool IsCompleted => !cancellationToken.CanBeCanceled || cancellationToken.IsCancellationRequested;
public void UnsafeOnCompleted(Action continuation)
    => cancellationToken.RegisterWithoutCaptureExecutionContext(continuation);  // 仅注册到“取消”回调
```

即该 awaitable **只在 token 被 Cancel 时完成**。而第一个加载在**成功**路径下：

- 在 `finally` 里 `_loadingMap.Remove(addrKey)`，把句柄写入 `_handles`（`RefCount = 1`）；
- 其 `using var linkedCts` 随方法返回被 **Dispose** —— **Dispose 不会触发取消回调**。

所以后到者注册的 continuation 永远不被调用，`await` **永久挂起**（直到整个 Model 被 `Destroy` / `CancelAllLoading` 才会因全局取消而醒来）。这与第 39 行注释承诺的「后到者在 CTS 上等候并重试句柄池」相矛盾——当前实现只在「第一个加载被**取消**」时才会让后到者继续，正常成功的 join 路径完全失效。

### 1.3 触发场景（真实可复现）
同一帧/相近时刻对**同一 address** 发起两次以上异步加载，例如：
- 两处业务同时 `ShowPanelAsync<SamePanel>()`；
- 按钮连点导致重复 `LoadPanelAsync<T>()`；
- `PreloadByKeysAsync` 与按需 `LoadAsync` 命中同一 key。

第二个及之后的调用的 UniTask 链**永不返回** → 在 UI 场景表现为面板永远不显示 / Loading 界面永不关闭。

### 1.4 期望行为（不变量，必须全部满足）
1. 后到者必须在「进行中的加载**结束**（成功 / 失败 / 取消）」时被唤醒，然后**复查句柄池**（命中即 `RefCount++` 返回）。复查逻辑沿用现有那两段池命中代码，**不要改 RefCount 语义**。
2. 后到者必须仍然响应**自己的** `ct`：等待期间若自己的 `ct` 被取消，应抛 `OperationCanceledException` 向上传播（**不要**像现在这样无脑 `catch {}` 把自己的取消也吞掉）。
3. 不改变单请求成功路径的既有行为；不改变池命中 `RefCount++` 语义。
4. `CancelAllLoading()` / `Destroy()` 的全局取消行为保持不变（仍能取消所有 in-flight 加载）。

### 1.5 推荐修复（择一，方案 A 改动最小）

**方案 A：用「完成信号」替换「取消信号」（推荐）**

把 `_loadingMap` 的值从「裸 `CancellationTokenSource`」升级为「同时持有 CTS（用于取消）+ 一个完成信号 `UniTaskCompletionSource`（用于唤醒后到者）」的小结构；第一个加载在 `finally` 中无条件 `TrySetResult()`，后到者改为 await 这个完成信号并附加自己的 `ct`。

参考实现（请按当前真实代码适配，标识符以仓库为准）：

```csharp
// 字段：替换原 Dictionary<AddressableKey, CancellationTokenSource> _loadingMap
private readonly Dictionary<AddressableKey, InFlight> _loadingMap = new();

private sealed class InFlight
{
    public CancellationTokenSource Cts;                 // 用于 CancelAllLoading 全局取消
    public readonly UniTaskCompletionSource Completed = new();  // 加载结束（成功/失败/取消）时 TrySetResult
}
```

- **后到者分支**（替换 1.1 那段）：

  ```csharp
  if (_loadingMap.TryGetValue(addrKey, out var inflight))
  {
      // 等“进行中的加载结束”，同时尊重自己的 ct（自己被取消则抛 OCE 向上传播）
      await inflight.Completed.Task.AttachExternalCancellation(ct);
      if (_handles.TryGetValue(addrKey, out var joinEntry) && joinEntry.Handle.IsValid())
      {
          joinEntry.RefCount++;
          _handles[addrKey] = joinEntry;
          BroadcastTarget?.Call(new AddressableLoadCompletedEvent(addrKey.Key, addrKey.AssetType, true, 0));
          return joinEntry.Handle.Result as T;
      }
      // 原加载失败/取消未写入句柄 → 回退到新加载（继续往下走第 3 段）
  }
  ```

- **第一个加载**：第 3 段创建 `linkedCts` 后，构造 `InFlight { Cts = linkedCts }` 写入 `_loadingMap[addrKey]`；在 `finally` 中先 `_loadingMap.Remove(addrKey)` 再 `inflight.Completed.TrySetResult()`（顺序：先从 map 摘除，避免后到者醒来后又看到旧 entry；`TrySetResult` 唤醒已在等待的后到者，它们随后复查 `_handles`）。
- **`CancelAllLoading()`**：原先遍历 `_loadingMap.Values` 调 `cts.Cancel()`，现改为遍历 `entry.Cts?.Cancel()`；可一并 `entry.Completed.TrySetCanceled()` 让等待者及时醒来后回退。

> 已确认仓库内存在所需原语：`UniTask/Runtime/UniTaskCompletionSource.cs`、`UniTask/Runtime/UniTaskExtensions.cs`（`AttachExternalCancellation`）。

**方案 B：保存「可多次 await」的 in-flight 任务**
把实际加载体抽成返回 `UniTask` 的私有方法，`.Preserve()` 后存入 `Dictionary<AddressableKey, UniTask> _loadingTasks`；后到者 `await _loadingTasks[addrKey].AttachExternalCancellation(ct)` 后复查句柄池。`Preserve()` 在 `UniTask/Runtime/UniTask.cs` 提供，可被多个 awaiter 安全 await。

### 1.6 验收标准
- 编译：`TGame.Addressable.Runtime.csproj` 0 error / 0 `warning CS`。
- 行为：对同一 key 发起 **N 个并发** `LoadAsync<T>`：
  - 底层 `Addressables.LoadAssetAsync` 只触发 **1 次**；
  - 全部 N 个调用都正常返回**同一**资源（无挂起）；
  - 此时 `GetRefCount<T>(key) == N`；
  - 调用 N 次 `Release<T>(key)` 后 `RefCount == 0` 且句柄被 `Addressables.Release`（`HandleCount` 归零）。
- 等待期间取消其中一个调用的 `ct`，该调用抛 `OperationCanceledException`，**不影响**其它并发调用拿到资源。

---

## 任务 2 — 【P1】UILoaderModule.LoadAsync 缺少 in-flight 去重

### 2.1 位置
`UnityProject/Assets/Plugins/TGame/TUI/Runtime/Model/UILoaderModule.cs`
方法 `LoadAsync(Type type, CancellationToken ct)`（约 133-196 行）。

### 2.2 根因
缓存 `_loaded` 在 `await`（约 157 行 `await addrModel.LoadAsync<GameObject>(...)`）**之前**查（约 135 行），在 `await` **之后**写（约 192 行），中间没有任何「同类型加载进行中」的去重表。于是两个并发的同 `Type` 调用：
1. 都通过开头的 `_loaded` 命中检查（此刻都还没写入）；
2. 都 `await` 加载（任务 1 修好后，`AddressableModel` 会把 `RefCount` 累加到 2）；
3. 都 `Instantiate`，各得一个 GameObject；
4. 都写 `_loaded[type]`（后写覆盖前写）→ **前一个 GameObject 成孤儿**：既不在缓存、也不会被 `Unload`/`OnDestroy` 销毁，永久泄漏。
5. `_loadedAddresses[type]` 只记一个 address；`Unload` 只 `Release` 一次 → `RefCount` 卡在 1 → **Addressable 资源永不释放**。

这与第 34 行注释「`LoadAsync` 幂等：二次调用直接返回缓存，不重复 Instantiate」**矛盾**（并发下不幂等）。

### 2.3 期望行为
- 并发同 `Type` 的 `LoadAsync` 共享**同一次**底层加载：只 `Instantiate` 一个 GameObject，`AddressableModel` 只 `+1` 引用，`_loadedAddresses[type]` 与之一致。
- 同步 `Load` 与已有「假 null 死引用清理」逻辑保持不变。
- 取消、未注册、缺组件、加载失败等既有错误分支语义保持不变。

### 2.4 推荐修复
新增按 `Type` 的 in-flight 表，让并发调用复用同一 `UniTask`：

```csharp
// 字段
private readonly Dictionary<Type, UniTask<BaseUIPanel>> _loading = new();

public UniTask<BaseUIPanel> LoadAsync(Type type, CancellationToken ct = default)
{
    // 缓存命中（含假 null 清理）后直接返回——保持原逻辑
    if (_loaded.TryGetValue(type, out var existing))
    {
        if (existing != null && existing.gameObject != null)
            return UniTask.FromResult(existing);
        _loaded.Remove(type);
    }
    // in-flight 去重：并发同类型复用同一任务
    if (_loading.TryGetValue(type, out var inflight))
        return inflight;

    var task = LoadCoreAsync(type, ct).Preserve();  // Preserve 允许多 awaiter
    _loading[type] = task;
    return task;
}

// 把原 LoadAsync 主体（注册查询 → Addressable 加载 → Instantiate → Init → 写 _loaded/_loadedAddresses）
// 整体搬进 LoadCoreAsync；务必在 finally 里 _loading.Remove(type)，保证成功/失败/取消都不残留。
private async UniTask<BaseUIPanel> LoadCoreAsync(Type type, CancellationToken ct)
{
    try
    {
        // ……原 LoadAsync 主体……
        return panel; // 或各错误分支的 null
    }
    finally
    {
        _loading.Remove(type);
    }
}
```

注意：
- `Preserve()` 后的任务可被多个 awaiter 安全 await（`UniTask/Runtime/UniTask.cs`）。
- 进入 `LoadCoreAsync` 开头**再查一次** `_loaded`（双重检查），覆盖「任务入表瞬间另一调用刚好完成」的窄竞态（可选但更稳）。
- 该去重只防并发；串行二次调用仍走 `_loaded` 缓存，零额外开销。

### 2.5 验收标准
- 编译：`TUI.csproj` 0 error / 0 `warning CS`。
- 行为：对同一 `Type` 并发 `LoadPanelAsync<T>()` 多次 →
  - 只生成 **1 个** Panel GameObject（无孤儿）；
  - `AddressableModel.GetRefCount<GameObject>(address) == 1`；
  - `UnloadPanel<T>()` 后该 address `RefCount == 0`、GameObject 被销毁、`_loadedAddresses` 不残留。

---

## 任务 3 — 【P2】PreloadInternalAsync 的 progress 形同虚设

### 3.1 位置
`UnityProject/Assets/Plugins/TGame/Addressable/Runtime/Model/AddressableModel.cs`
方法 `PreloadInternalAsync<T>`（约 314-374 行），形参 `IProgress<float> progress`（约 319 行）。

### 3.2 根因
- 形参 `progress` 全程**未被调用**（无 `progress.Report(...)`）。
- 方法只广播 `AddressablePreloadCompletedEvent`，从不广播 `AddressablePreloadProgressEvent`——尽管类注释（第 21 行）写明会广播 `AddressablePreloadProgressEvent`，且该事件类型确实存在：`Addressable/Runtime/Model/AddressableEvent.cs` 中 `AddressablePreloadProgressEvent(string context, int completed, int total)`。
- 现用 `await UniTask.WhenAll(tasks)` 一次性等待，拿不到「逐个完成」的节点，因此无法报进度。

### 3.3 修复（二选一，A 更符合现有 API 承诺）

**方案 A：接上进度上报（推荐）**
不要直接 `WhenAll`，改为给每个 inner task 包一层「完成即计数 + 上报」：

```csharp
int total = tasks.Count, completed = 0;
var wrapped = new List<UniTask>(total);
foreach (var t in tasks)
{
    wrapped.Add(ReportingWrap(t));
}
await UniTask.WhenAll(wrapped);

async UniTask ReportingWrap(UniTask inner)
{
    await inner;
    var done = System.Threading.Interlocked.Increment(ref completed);
    progress?.Report((float)done / total);
    BroadcastTarget?.Call(new AddressablePreloadProgressEvent(context, done, total));
}
```
（`completed`/`total` 命名按现有上下文调整；`tasks` 即现有那批 `LoadInternalAsync` 任务。）

**方案 B：删除死形参**
若产品上不需要进度，则从 `PreloadByLabelAsync` / `PreloadByKeysAsync` / `PreloadInternalAsync` 移除 `IProgress<float> progress` 形参，同步删掉 `UIManager` 里 `PreloadPanelsByLabelAsync` / `PreloadPanelsByKeysAsync` 的 `progress` 形参，并修正类注释第 21 行（去掉 `AddressablePreloadProgressEvent`）。**这会改公共签名**——仅在确认无业务方依赖时采用，否则用方案 A。

### 3.4 验收标准
- 编译：`TGame.Addressable.Runtime.csproj` 0 error / 0 `warning CS`。
- 方案 A：批量预热 K 个资源时，`progress` 收到递增至 `1.0` 的回调，`AddressablePreloadProgressEvent` 广播 K 次（`completed` 从 1 递增到 K）。
- 方案 B：解决方案内无对已删形参的引用，注释与实现一致。

---

## 3. 统一验证

在仓库 `UnityProject/` 目录下执行（无输出 = 0 error 0 代码 warning = 通过）：

```bash
cd UnityProject
dotnet build TGame.Addressable.Runtime.csproj -nologo 2>&1 | grep -E "error CS|warning CS"
dotnet build TUI.csproj                       -nologo 2>&1 | grep -E "error CS|warning CS"
```

- `MSB3277`（包版本冲突）可忽略，与代码无关。
- 若环境缺少 Unity 引用程序集导致 `dotnet build` 无法解析（CLAUDE.md 记录其位于 `G:\Unity Editor\6000.3.2f1\...`），则退回 Unity Editor 内 `Ctrl+R` / `Assets → Reimport All` 验证，但仍以「0 编译错误」为完成门槛。
- 建议在 Unity Test Framework（EditMode）下补充任务 1/任务 2 的并发回归用例（断言「1 次底层加载 + RefCount==N + N 次 Release 归零」「并发同类型只 1 个 GameObject」）。测试执行在 Unity Editor 内完成。

---

## 4. 不在范围内（避免过度改动）

- `TextMeshProScroller`（`562154b`）：已审阅，订阅 `TEXT_CHANGED_EVENT`、`Mathf.Min` 守界、`_cacheDirty` 处理均得当，本轮**不动**。
- `AddressableModel` 与 `AddressableManager` 各自持有独立句柄池（UIManager 下挂的是独立实例）——这是已知且被接受的设计，**不要**合并。
- 不重命名公共类型/方法，不调整 asmdef，不引入新第三方依赖。

> 完成后：按 AGENTS.md 把本计划归档于 `Document/AIDoc/Plan/`；若期间发现新 bug，记到 `Document/AIDoc/Bugs/`。
