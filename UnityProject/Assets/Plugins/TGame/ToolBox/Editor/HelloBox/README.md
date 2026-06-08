# ToolBox 使用说明

## 简介

ToolBox 是一个多功能 Unity Editor 工具窗口，采用**侧边栏 + 内容区**布局。每个"Box"是一个独立的工具页面，通过 `BoxRegistration` 注册到对应分组。

## 架构

```
ToolBoxWindow (EditorWindow)
  ├── 侧边栏 (分组 + Box 列表)
  └── 内容区 (选中 Box 的 VisualElement)
        └── Factory() → VisualElement
```

- 分组定义在 `ToolBoxWindow._groups` 字典中
- 每个 Box 通过 `BoxRegistration` 描述名称、图标、分组和工厂方法
- Box 类实现 `IToolBoxContentVisualElement` 接口

## 添加一个 Box

### 1. 创建 Box 类

实现 `IToolBoxContentVisualElement` 接口，提供 `CreateContent()` 方法返回 `VisualElement`：

```csharp
using UnityEngine.UIElements;
using UnityEditor;

namespace TGame.ToolBox
{
    public class MyToolBox : IToolBoxContentVisualElement
    {
        public static BoxRegistration Registration => new()
        {
            Name = "我的工具",
            Group = "程序",
            Icon = "d_SettingsIcon",
            Factory = () => new MyToolBox().CreateContent()
        };

        public VisualElement CreateContent()
        {
            var root = new VisualElement();
            root.Add(new Label("Hello ToolBox!"));
            return root;
        }
    }
}
```

### 2. 注册到 ToolBoxWindow

在 `ToolBoxWindow.cs` 的 `_groups` 字典中找到对应分组，添加 `Registration`：

```csharp
["程序"] = ("程序工具", new List<BoxRegistration>
{
    HelloBox.Registration,
    PathBox.Registration,
    DebugBox.Registration,
    MyToolBox.Registration,   // ← 添加在这里
}),
```

## BoxRegistration 参数

| 参数 | 类型 | 说明 |
|------|------|------|
| `Name` | `string` | 侧边栏显示名称 |
| `Group` | `string` | 所属分组（"程序"/"资源"/"构建"） |
| `Icon` | `string` | Unity 内置图标名称（如 `d_console.infoicon`） |
| `Factory` | `Func<VisualElement>` | 创建内容面板的工厂方法 |

### 图标参考

图标使用 Unity 内置 `EditorGUIUtility.IconContent()` 名称。常见图标：

| 名称 | 说明 |
|------|------|
| `d_console.infoicon` | 信息 |
| `d_SettingsIcon` | 设置 |
| `Folder Icon` | 文件夹 |
| `Prefab Icon` | 预制体 |
| `d_Help` | 帮助 |
| `d_ColorPicker` | 颜色 |
| `Animation Icon` | 动画 |

> 名称以 `d_` 开头的为暗色主题专用图标，系统会自动降级到非 `d_` 版本。

## 现有功能一览

### 程序工具
- **欢迎使用ToolBox** — 本说明文档
- **常用路径** — Unity 目录速查与复制
- **Debug** — TDebug 日志系统运行时控制面板

### 资源工具
- **颜色工具箱** — 颜色速查、自定义颜色库
- **曲线库** — 动画曲线预设

### 构建工具
- **构建打包** — 集成 Unity 6 BuildProfile 的构建管理
  - Profile 资产选择、新建、重命名、删除
  - Player / AssetBundle 双流水线构建
  - 版本号自动递增
  - 自定义构建流水线扩展
  - 复制 CLI 指令（用于 CI/CD）
  - 命令行打包入口 `BuildCLI.Build`

## 注意事项

- Box 类使用**无参构造 + 工厂模式**，每次打开窗口会重新创建
- 图标名称需使用 Unity 内置有效名称，错误名称不会报错但显示空白
- 侧边栏宽度自动持久化到 `EditorPrefs`
- 分组在 `ToolBoxWindow.OnEnable()` 中从 `_groups` 字典恢复
