using System;
using System.Collections.Generic;
using System.Threading;
using TGame.Addressable;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// TUI 模块示例:MonoBehaviour 启动器。
    /// 启动流程:PreloadPanelsAsync(按 label 预热 + 自动注册 Type→address)→ ShowPanelAsync(SamplePanel)。
    /// 面板注册不再需要逐个 RegisterPanelAsync —— 预热时从 prefab 反查 BaseUIPanel 子类自动写表。
    /// 注意:层级信息写在 prefab 上的 BaseUIPanel._layer 字段,不在此处配置。
    /// </summary>
    public class TUISample : MonoBehaviour
    {

        
    }
}
