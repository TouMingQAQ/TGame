# ToolBox 使用说明

## 简介

ToolBox 是一个多功能 Editor 工具，基于 `[ToolBox]` 特性自动识别并渲染内容类。

## 添加标签页

为任意类添加 `[ToolBox("名称")]` 特性即可在 ToolBox 中生成标签页：

```csharp
[ToolBox("我的工具")]
public class MyTool : IToolBoxContent
{
    public void DrawContent()
    {
        // 自定义 UI
    }
}
```

## 使用 ScriptableObject

支持从指定路径加载 SO 资产作为配置页：

```csharp
[ToolBox("项目配置", Path = "Assets/Settings/Config.asset")]
public class ProjectConfigSO : ScriptableObject
{
    public int SomeValue;
}
```

## 特性参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| Name | Tab 显示名称 | 必填 |
| Path | SO 资产路径 | 空 |
| Order | 排序 | -1 |

## 注意事项

- 普通类必须实现 `IToolBoxContent` 接口
- SO 类未找到资产文件时自动创建临时实例
- 分割线宽度自动持久化
