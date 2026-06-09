# TUI MVVM Framework Design

## 概要

在 TUI 框架内提供一套与 UIManager 解耦的 MVVM 层，用于 Unity uGUI 的数据驱动 UI 开发。
MVVM 核心不依赖 UIManager，可与任何管理/面板系统配合使用。

### 目标

- 数据与表现分离（ViewModel ↔ View）
- uGUI 组件通过代码式扩展方法绑定 ViewModel 的 `ReactiveProperty<T>`
- 按钮等交互通过 `ICommand` 绑定，原生支持 UniTask 异步
- 输入验证内建支持（`ValidatableProperty<T>`）
- Per-Panel 生命周期，Panel 销毁时自动解除绑定
- **MVVM 核心与 UIManager 完全解耦**
- **ViewModel 仅有接口规范 `IViewModel`，无强制基类约束**

### 非目标

- 不做反射式/自动绑定（坚持代码式显式绑定，类型安全）
- 不改动现有 `BaseUIPanel` 及其动画系统
- 不引入第三方 MVVM 框架
- 不要求必须使用 `UIManager` — MVVM 可独立运作

---

## 解耦架构

### 分层设计

```
┌─────────────────────────────────────────────────┐
│  UIManagerIntegration  (可选)                     │
│  TUI/Runtime/MVVM/UIManagerIntegration.cs        │
│  依赖: MVVM Core + UIManager                      │
│  提供: UIManager 的 ShowMVVMPanel 扩展方法        │
└─────────────────────────────────────────────────┘
                      ↑ 可选引用
┌─────────────────────────────────────────────────┐
│  MVVM Core  (无外部依赖)                         │
│  TUI/Runtime/MVVM/                               │
│  ─────────────────────────                       │
│  ViewModel 仅有 IViewModel 接口契约               │
│  无强制基类，无 Unity 依赖                        │
│  BaseMVVMPanel 可独立运行                        │
└─────────────────────────────────────────────────┘
                      ↑ 继承
┌─────────────────────────────────────────────────┐
│  TUI Core (BaseUIPanel / UIManager)              │
│  不做改动                                         │
└─────────────────────────────────────────────────┘
```

---

## 目录结构

```
TUI/Runtime/MVVM/
├── IViewModel.cs              # ViewModel 接口契约 + SetProperty 扩展方法
├── ReactiveProperty.cs        # 可观察属性包装器
├── ReactiveCollection.cs      # 可观察集合
├── Command.cs                 # ICommand / RelayCommand / AsyncRelayCommand
├── CompositeDisposable.cs     # 一次性资源集合
├── BaseMVVMPanel.cs           # MVVM 版 Panel 基类（extends BaseUIPanel）
├── BindingExtensions.cs       # uGUI 组件绑定扩展方法
├── Validation/
│   ├── ValidatableProperty.cs    # 带验证的 ReactiveProperty
│   └── ValidationRule.cs         # 验证规则基类 + 内置规则
├── UIManagerIntegration.cs    # 〈可选〉UIManager 胶水层
```

---

## 接口规范

### IViewModel — 唯一的 ViewModel 契约

```csharp
public interface IViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>初始化，Panel.Bind() 时调用一次</summary>
    void Initialize();
    /// <summary>面板即将显示（BeforeShow）</summary>
    void OnBind();
    /// <summary>面板已隐藏（AfterHide）</summary>
    void OnUnbind();
    /// <summary>请求关闭关联的 View</summary>
    event Action RequestClose;
}
```

无强制基类，任何类只要实现 IViewModel 即可作为 ViewModel 使用。

### IMonoContextViewModel — 可选，Unity 生命周期访问

```csharp
/// <summary>
/// ViewModel 需要挂载 MonoBehaviour 来获得 Unity 生命周期（协程、Invoke 等）时实现此接口。
/// BaseMVVMPanel.Bind() 会自动检测并注入 panel 自身作为 MonoContext。
/// </summary>
public interface IMonoContextViewModel
{
    MonoBehaviour MonoContext { get; set; }
}
```

### 扩展方法 — 替代 BaseViewModel 的 SetProperty

```csharp
public static class ViewModelHelper
{
    /// <summary>
    /// 属性变更辅助。实现 IViewModel 的类可直接使用。
    /// 替代传统 BaseViewModel 中的 SetProperty 方法。
    /// </summary>
    public static bool SetProperty<T>(this IViewModel vm,
        ref T field, T value,
        PropertyChangedEventHandler handler,
        [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        handler?.Invoke(vm, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
```

---

## 支持三种 ViewModel 实现方式

### 方式 1：纯 C# 类（推荐，可测试）

```csharp
public class LoginViewModel : IViewModel
{
    public event PropertyChangedEventHandler PropertyChanged;
    public event Action RequestClose;

    public void Initialize() { }
    public void OnBind() { }
    public void OnUnbind() { }
    public void Dispose() { }

    // ReactiveProperty 直接用于绑定，无需手动 INPC
    public ReactiveProperty<string> Username { get; } = new();
    public ICommand SubmitCommand { get; }

    // 需要 INPC 通知的复杂属性用 SetProperty 扩展方法
    private int _count;
    public int Count
    {
        get => _count;
        set => this.SetProperty(ref _count, value, PropertyChanged);
    }
}
```

### 方式 2：MonoBehaviour（直接挂预制体，天然 Unity 生命周期）

```csharp
public class PlayerHUDViewModel : MonoBehaviour, IViewModel
{
    public event PropertyChangedEventHandler PropertyChanged;
    public event Action RequestClose;

    public ReactiveProperty<int> Hp { get; } = new(100);
    public ReactiveProperty<float> HpPercent { get; } = new(1f);

    private void Awake() { }
    public void Initialize() { }
    public void OnBind() { }
    public void OnUnbind() { }
    public void Dispose() { }
}
```

### 方式 3：纯 C# + MonoContext（需要协程等能力）

```csharp
public class GameMenuViewModel : IViewModel, IMonoContextViewModel
{
    public MonoBehaviour MonoContext { get; set; }   // 由 Bind() 自动注入
    public event PropertyChangedEventHandler PropertyChanged;
    public event Action RequestClose;

    // 通过 MonoContext 使用协程
    public ICommand LoadCommand { get; }

    public GameMenuViewModel()
    {
        LoadCommand = new AsyncRelayCommand(OnLoad);
    }

    private async UniTask OnLoad()
    {
        // 通过 MonoContext 访问 Unity 生命周期
        await UniTask.Delay(1000);
    }

    public void Initialize() { }
    public void OnBind() { }
    public void OnUnbind() { }
    public void Dispose() { }
}
```

---

## 核心类型详解

### 1. ReactiveProperty\<T\>

```csharp
public class ReactiveProperty<T>
```

包装一个值，值改变时发出通知。绑定系统的核心基础。

- `T Value { get; set; }` — 取值/赋值（自动 equality 检查，值不变不发通知）
- `event Action<T> ValueChanged` — 值变化事件
- `void SetValueAndForceNotify(T)` — 强制发通知
- `IDisposable Subscribe(Action<T>)` — 订阅变化，返回 disposable token
- `static implicit operator T(ReactiveProperty<T>)` — 隐式转换

```csharp
var name = new ReactiveProperty<string>("Player1");
name.Value = "Player2";                  // 自动触发 ValueChanged
string current = name;                   // 隐式转换
```

### 2. ReactiveCollection\<T\>

```csharp
public class ReactiveCollection<T> : IList<T>, INotifyCollectionChanged
```

类似 `ObservableCollection<T>`，用于 Dropdown 选项列表 / 动态集合。

### 3. CompositeDisposable

```csharp
public sealed class CompositeDisposable : IDisposable
```

一次性资源集合。所有绑定扩展方法返回 `IDisposable`，丢进这个集合统一管理。

```csharp
var bag = new CompositeDisposable();
bag.Add(text.BindText(vm.Title));
bag.Add(button.BindClick(vm.Command));
bag.Dispose(); // 解除所有绑定
```

### 4. ICommand / RelayCommand / AsyncRelayCommand

```csharp
public interface ICommand
{
    bool CanExecute { get; }
    event Action CanExecuteChanged;
    void Execute();
}
```

| 类型 | 说明 |
|------|------|
| `RelayCommand(Action, Func<bool>?)` | 同步命令 |
| `AsyncRelayCommand(Func<UniTask>, Func<bool>?)` | 异步命令，执行期间自动锁 CanExecute=false |
| `RelayCommand<T>(Action<T>, Func<T,bool>?)` | 带参数命令 |

### 5. BaseMVVMPanel\<TViewModel\>

```csharp
public abstract class BaseMVVMPanel<TViewModel> : BaseUIPanel
    where TViewModel : class, IViewModel
```

MVVM 版面板基类，继承自 `BaseUIPanel`，保持 DOTween 动画系统。**不依赖 UIManager**。

**新增成员：**
- `protected TViewModel ViewModel { get; private set; }`
- `protected CompositeDisposable Bindings { get; }`
- `void Bind(TViewModel viewModel)` — 设置 ViewModel，自动建立绑定
- `abstract void OnBindViewModel(TViewModel viewModel)` — 子类在此设置绑定

**Bind() 核心逻辑：**

```csharp
public void Bind(TViewModel viewModel)
{
    ViewModel = viewModel;

    // 自动注入 MonoContext（如果 ViewModel 支持）
    if (viewModel is IMonoContextViewModel monoVM)
        monoVM.MonoContext = this;

    // 自动连接关闭请求
    ViewModel.RequestClose += () => Hide();

    ViewModel.Initialize();
    Bindings = new CompositeDisposable();
    OnBindViewModel(viewModel);
}
```

**生命周期集成：**

```
Bind(vm)
  ├─ if vm is IMonoContextViewModel → vm.MonoContext = this
  ├─ vm.RequestClose += () => Hide()
  ├─ vm.Initialize()
  ├─ Bindings = new CompositeDisposable()
  └─ OnBindViewModel(vm)

Show() → BeforeShow()
  └─ ViewModel.OnBind()

Hide() → AfterHide()
  ├─ ViewModel.OnUnbind()
  └─ Bindings.Dispose()

OnDestroy()
  ├─ Bindings.Dispose()
  └─ ViewModel.Dispose()
```

### 6. uGUI 绑定扩展方法（BindingExtensions）

所有方法返回 `IDisposable`，统一由 `CompositeDisposable` 管理。

| 扩展目标 | 方法 | 方向 | 说明 |
|---------|------|:----:|------|
| `Text` | `BindText(ReactiveProperty<string>)` | V→V | 单向 |
| `Text` | `BindTextFormat<T>(ReactiveProperty<T>, string)` | V→V | 格式化 |
| `Button` | `BindClick(ICommand)` | V→V | 点击→执行命令 |
| `Toggle` | `BindIsOn(ReactiveProperty<bool>)` | V↔V | 双向 |
| `Slider` | `BindValue(ReactiveProperty<float>)` | V↔V | 双向 |
| `InputField` | `BindInput(ReactiveProperty<string>)` | V↔V | 双向 |
| `Dropdown` | `BindOptions(ReactiveCollection<string>)` | V→V | 选项列表 |
| `Dropdown` | `BindValue(ReactiveProperty<int>)` | V↔V | 选中索引 |
| `Image` | `BindSprite(ReactiveProperty<Sprite>)` | V→V | 单向 |
| `Graphic` | `BindColor(ReactiveProperty<Color>)` | V→V | 单向 |
| `GameObject` | `BindActive(ReactiveProperty<bool>)` | V→V | 单向 |
| `CanvasGroup` | `BindInteractable(ReactiveProperty<bool>)` | V→V | 单向 |
| `Text` | `BindErrors(INotifyDataErrorInfo)` | V→V | 验证错误文本 |

双向绑定通过标志位防止循环回写。

### 7. 数据验证

| 类 | 说明 |
|----|------|
| `ValidatableProperty<T>` | 继承 ReactiveProperty，加入规则和 INotifyDataErrorInfo |
| `ValidationRule<T>` | 抽象基类：`abstract ValidationResult Validate(T value)` |
| `NotNullOrEmptyRule` | 字符串非空验证 |
| `RegexRule` | 正则表达式匹配 |
| `RangeRule<T>` | 数值范围 |

### 8. UIManagerIntegration （可选）

独立文件，只在需要与 UIManager 配合时引用。

```csharp
public static class UIManagerIntegration
{
    public static TViewModel ShowMVVMPanel<TViewModel, TPanel>(this UIManager manager)
        where TViewModel : class, IViewModel, new()
        where TPanel : BaseMVVMPanel<TViewModel>
    {
        var panel = manager.LoadPanel<TPanel>();
        var vm = new TViewModel();
        panel.Bind(vm);
        panel.transform.SetAsLastSibling();
        panel.Show();
        manager.Call(new PanelOpenedEvent(typeof(TPanel).Name));
        return vm;
    }
}
```

---

## 完整示例

### 纯 C# ViewModel（登录面板）

```csharp
// === LoginViewModel.cs ===
public class LoginViewModel : IViewModel
{
    public event PropertyChangedEventHandler PropertyChanged;
    public event Action RequestClose;

    public ReactiveProperty<string> Username { get; } = new();
    public ReactiveProperty<string> Password { get; } = new();
    public ReactiveProperty<bool> IsLoading { get; } = new(false);
    public ReactiveProperty<string> ErrorMessage { get; } = new();
    public ValidatableProperty<string> Email { get; } = new();

    public ICommand LoginCommand { get; }
    public ICommand CloseCommand { get; }

    public LoginViewModel()
    {
        LoginCommand = new AsyncRelayCommand(OnLogin, CanLogin);
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
        Email.AddRule(new NotNullOrEmptyRule("邮箱不能为空"));
    }

    public void Initialize()
    {
        Username.Value = PlayerPrefs.GetString("last_username", "");
    }

    public void OnBind() { }
    public void OnUnbind() { }
    public void Dispose() { }

    private bool CanLogin()
        => !string.IsNullOrEmpty(Username.Value)
        && !string.IsNullOrEmpty(Password.Value)
        && !IsLoading.Value;

    private async UniTask OnLogin()
    {
        IsLoading.Value = true;
        ErrorMessage.Value = "";
        try
        {
            await UniTask.Delay(500);
            RequestClose?.Invoke();
        }
        catch (Exception e) { ErrorMessage.Value = e.Message; }
        finally { IsLoading.Value = false; }
    }
}

// === LoginPanel.cs ===
public class LoginPanel : BaseMVVMPanel<LoginViewModel>
{
    [SerializeField] private InputField _usernameInput;
    [SerializeField] private InputField _passwordInput;
    [SerializeField] private Button _loginBtn;
    [SerializeField] private Button _closeBtn;
    [SerializeField] private Text _errorText;
    [SerializeField] private GameObject _loadingOverlay;

    protected override void OnBindViewModel(LoginViewModel vm)
    {
        Bindings.Add(_usernameInput.BindInput(vm.Username));
        Bindings.Add(_passwordInput.BindInput(vm.Password));
        Bindings.Add(_loginBtn.BindClick(vm.LoginCommand));
        Bindings.Add(_closeBtn.BindClick(vm.CloseCommand));
        Bindings.Add(_errorText.BindText(vm.ErrorMessage));
        Bindings.Add(_loadingOverlay.BindActive(vm.IsLoading));
    }
}

// === 调用 ===
var panelObj = Instantiate(loginPanelPrefab);
panelObj.GetComponent<LoginPanel>().Bind(new LoginViewModel()).Show();

// 或通过 UIManager
UIManager.Instance.ShowMVVMPanel<LoginViewModel, LoginPanel>();
```

### MonoBehaviour ViewModel（HUD 面板）

```csharp
// === PlayerHUDViewModel.cs ===
public class PlayerHUDViewModel : MonoBehaviour, IViewModel
{
    public event PropertyChangedEventHandler PropertyChanged;
    public event Action RequestClose;

    public ReactiveProperty<int> Hp { get; } = new(100);
    public ReactiveProperty<int> MaxHp { get; } = new(100);
    public ReactiveProperty<float> HpPercent { get; } = new(1f);
    public ICommand UsePotionCommand { get; private set; }

    private void Awake()
    {
        UsePotionCommand = new RelayCommand(OnUsePotion);
    }

    public void Initialize() { /* 订阅事件 */ }
    public void OnBind() { }
    public void OnUnbind() { }
    public void Dispose() { }

    private void OnUsePotion() { /* 使用药水逻辑 */ }
}

// === PlayerHUD.cs ===
public class PlayerHUD : BaseMVVMPanel<PlayerHUDViewModel>
{
    [SerializeField] private Slider _hpSlider;
    [SerializeField] private Text _hpText;
    [SerializeField] private Button _usePotionBtn;

    // MonoBehaviour ViewModel 直接在预制体上 Serialize
    [SerializeField] private PlayerHUDViewModel _viewModel;

    protected override void Awake()
    {
        base.Awake();
        if (_viewModel != null) Bind(_viewModel);
    }

    protected override void OnBindViewModel(PlayerHUDViewModel vm)
    {
        Bindings.Add(_hpSlider.BindValue(vm.HpPercent));
        Bindings.Add(_hpText.BindTextFormat(vm.Hp, "HP: {0}"));
        Bindings.Add(_usePotionBtn.BindClick(vm.UsePotionCommand));
    }
}
```

---

## 实施清单

### Core MVVM (9 文件)
- [ ] `IViewModel.cs` — 接口 + ViewModelHelper 扩展
- [ ] `ReactiveProperty.cs` — 响应式属性包装器
- [ ] `ReactiveCollection.cs` — 可观察集合
- [ ] `CompositeDisposable.cs` — 资源管理
- [ ] `Command.cs` — ICommand / RelayCommand / AsyncRelayCommand
- [ ] `BaseMVVMPanel.cs` — MVVM 面板基类（泛型约束 class, IViewModel）
- [ ] `BindingExtensions.cs` — uGUI 绑定扩展
- [ ] `UIManagerIntegration.cs` — （可选）UIManager 胶水层

### Validation (2 文件)
- [ ] `ValidatableProperty.cs` — 带验证的属性
- [ ] `ValidationRule.cs` — 规则基类 + 内置规则

---

## 解耦检查清单

| 检查项 | 状态 |
|--------|:----:|
| IViewModel 是纯接口，不依赖 UnityEngine | ✓ |
| 无强制基类（不要 BaseViewModel） | ✓ |
| BaseMVVMPanel 泛型约束 `class, IViewModel` | ✓ |
| BaseMVVMPanel 不引用 UIManager | ✓ |
| BindingExtensions 不引用 UIManager | ✓ |
| Validation 模块不引用 UIManager | ✓ |
| 只有 UIManagerIntegration.cs 引用 UIManager | ✓ |
| 纯 C# ViewModel 可用（new ViewModel()） | ✓ |
| MonoBehaviour ViewModel 可用（挂载预制体） | ✓ |
| 带 MonoContext 的 ViewModel 可用（IMonoContextViewModel） | ✓ |
| BaseMVVMPanel 可独立 Instantiate + Bind + Show/Hide | ✓ |

---

## 设计决策记录

| 决策 | 选项 | 选定 |
|------|------|------|
| 目录位置 | TUI/MVVM 内 vs 独立模块 | TUI/MVVM |
| 与 UIManager 关系 | 耦合 vs 解耦 | **强制解耦** |
| ViewModel 规范 | 抽象类 vs 接口 | **IViewModel 接口，不要 BaseViewModel** |
| 实现方式 | 纯 C# / MonoBehaviour / 两者 | **三者都支持** |
| 绑定方式 | 代码式 vs 组件式 | 代码式扩展方法 |
| 异步 | UniTask 原生 vs 纯同步 | UniTask |
| 数据验证 | 内建 vs 不内置 | 内建 |
| 关闭交互 | VM.RequestClose 事件 | 事件方式，View 监听 |
| 绑定托管 | CompositeDisposable 模式 | 统一管理 |


---

## 方案总结

### 一句话

在 `TUI/Runtime/MVVM/` 下提供一套**与 UIManager 解耦**的 MVVM 框架，ViewModel 通过 **`IViewModel` 接口契约化**，支持纯 C# 类、MonoBehaviour、带 MonoContext 的混合实现三种方式。

### 核心类型关系

```
 ┌─────────────────────────────────────────────────────────┐
 │                  IViewModel (接口契约)                    │
 │  INPC + Initialize() + OnBind() + OnUnbind() + RequestClose │
 └─────────────────────────────────────────────────────────┘
        ↑ 实现                       ↑ 实现              ↑ 实现
 ┌──────────────┐  ┌──────────────────────┐  ┌──────────────────────┐
 │ 纯 C# ViewModel │  │ MonoBehaviour ViewModel│  │ C# + MonoContext VM  │
 │ class LoginVM  │  │ class HUDVM : Mono-  │  │ class MenuVM         │
 │ : IViewModel  │  │   Behaviour, IViewModel│  │ : IViewModel,        │
 │               │  │   (挂预制体, 天然生命周期)│  │   IMonoContextViewModel│
 └──────────────┘  └──────────────────────┘  └──────────────────────┘
```

### 数据流

```
 用户操作 (Button.click)
      ↓
 BindingExtensions (代码式订阅 ICommand)
      ↓
 ViewModel 执行命令 (ICommand.Execute)
      ↓ 更新数据
 ReactiveProperty<T>.Value = newValue
      ↓ 触发 ValueChanged
 BindingExtensions 更新 uGUI 组件
```

### 文件清单 (9+2 文件)

| 文件 | 职责 |
|------|------|
| `IViewModel.cs` | 接口 + ViewModelHelper.SetProperty 扩展 |
| `ReactiveProperty.cs` | 可观察值，自动通知 |
| `ReactiveCollection.cs` | 可观察集合 |
| `CompositeDisposable.cs` | 绑定资源统一管理 |
| `Command.cs` | ICommand / RelayCommand / AsyncRelayCommand |
| `BaseMVVMPanel.cs` | 面板基类，接受任何 IViewModel |
| `BindingExtensions.cs` | uGUI 组件绑定扩展 (12+ 组件) |
| `ValidatableProperty.cs` | 带验证的响应式属性 |
| `ValidationRule.cs` | 验证规则基类 + 内置规则 |
| `UIManagerIntegration.cs` | (可选) UIManager 胶水层 |

### 解耦边界

```
MVVM Core ──不引用──→ UIManager
     ↑
UIManagerIntegration (唯一引用 UIManager 的文件, 可选)
```
---

## 思维导图

```
TUI MVVM Framework
│
├── 设计原则
│   ├── 与 UIManager 解耦 (核心不引用 UIManager)
│   ├── 无强制基类 (只有 IViewModel 接口)
│   ├── 代码式绑定 (类型安全, 无反射)
│   ├── Per-Panel 生命周期
│   └── 可选集成 (UIManagerIntegration 独立)
│
├── 接口层 (IViewModel.cs)
│   ├── IViewModel
│   │   ├── Initialize()
│   │   ├── OnBind() / OnUnbind()
│   │   ├── RequestClose 事件
│   │   ├── INotifyPropertyChanged
│   │   └── IDisposable
│   ├── IMonoContextViewModel (可选)
│   │   └── MonoBehaviour MonoContext
│   └── ViewModelHelper
│       └── SetProperty<T>() 扩展方法
│
├── 响应式基础
│   ├── ReactiveProperty<T>
│   │   ├── Value { get; set } (自动 equality)
│   │   ├── ValueChanged 事件
│   │   └── Subscribe() / 隐式转换
│   ├── ReactiveCollection<T>
│   │   ├── IList<T>
│   │   └── CollectionChanged 事件
│   └── CompositeDisposable
│       └── Add() / Dispose()
│
├── 命令系统 (Command.cs)
│   ├── ICommand
│   │   ├── Execute()
│   │   ├── CanExecute
│   │   └── CanExecuteChanged
│   ├── RelayCommand (同步)
│   ├── AsyncRelayCommand (UniTask)
│   └── RelayCommand<T> (带参数)
│
├── 面板基类 (BaseMVVMPanel.cs)
│   ├── 继承 BaseUIPanel (保持 DOTween 动画)
│   ├── 泛型约束: class, IViewModel
│   ├── Bind(TViewModel vm)
│   │   ├── 检测 IMonoContextViewModel → 注入 panel
│   │   ├── 连接 RequestClose → Hide()
│   │   ├── vm.Initialize()
│   │   └── 创建 CompositeDisposable → OnBindViewModel()
│   └── 生命周期集成
│       ├── BeforeShow → vm.OnBind()
│       ├── AfterHide → vm.OnUnbind() + Bindings.Dispose()
│       └── OnDestroy → Bindings.Dispose() + vm.Dispose()
│
├── 绑定扩展 (BindingExtensions.cs)
│   ├── Text         → BindText() / BindTextFormat()
│   ├── Button       → BindClick(ICommand)
│   ├── Toggle       → BindIsOn()          (双向)
│   ├── Slider       → BindValue()         (双向)
│   ├── InputField   → BindInput()         (双向)
│   ├── Dropdown     → BindOptions() / BindValue()
│   ├── Image        → BindSprite()
│   ├── Graphic      → BindColor()
│   ├── GameObject   → BindActive()
│   ├── CanvasGroup  → BindInteractable() / BindAlpha()
│   └── Text         → BindErrors() (验证绑定)
│
├── 验证系统 (Validation/)
│   ├── ValidatableProperty<T>
│   │   ├── 继承 ReactiveProperty<T>
│   │   ├── AddRule(ValidationRule<T>)
│   │   ├── INotifyDataErrorInfo
│   │   └── 值变化 → 自动验证
│   └── ValidationRule<T>
│       ├── NotNullOrEmptyRule
│       ├── RegexRule
│       └── RangeRule<T>
│
└── 可选集成 (UIManagerIntegration.cs)
    ├── ShowMVVMPanel<TVM, TPanel>()
    ├── ShowMVVMPanel<TVM, TPanel>(TVM)
    └── GetPanelViewModel<TVM, TPanel>()
```

