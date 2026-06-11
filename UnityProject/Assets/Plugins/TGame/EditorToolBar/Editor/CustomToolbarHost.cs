// CustomToolbarHost.cs
// 反射找到 Unity 内部主工具栏窗口,把 VisualElement 容器挂到 ToolbarZoneLeftAlign / ToolbarZoneRightAlign。
// Renderer 在容器内按槽位分桶,调用各 ICustomToolbarItem.Build() 把子元素 Add 进去。
//
// 注入失败时只打 warning,不抛异常 —— Unity 内部类型名随时可能变。

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.EditorToolBar
{
    public static class CustomToolbarHost
    {
        public enum InitState
        {
            NotStarted,
            AssembliesNotReady,
            RootNotFound,
            ContainersAttached,
            AlreadyAttached
        }

        public static InitState State { get; private set; } = InitState.NotStarted;
        public static string LastError { get; private set; }

        public static VisualElement LeftRoot { get; private set; }
        public static VisualElement RightRoot { get; private set; }

        [InitializeOnLoadMethod]
        private static void Bootstrap()
        {
            EditorApplication.delayCall += Initialize;
        }

        public static void Initialize()
        {
            if (State == InitState.AlreadyAttached || State == InitState.ContainersAttached)
                return;

            try
            {
                var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
                if (toolbarType == null)
                {
                    Fail(InitState.AssembliesNotReady, "Cannot find internal type 'UnityEditor.Toolbar'");
                    return;
                }
                Verbose($"[1/4] Found type: {toolbarType.FullName}");

                object toolbarInstance = AcquireToolbarInstance(toolbarType);
                if (toolbarInstance == null)
                {
                    Fail(InitState.AssembliesNotReady, "Cannot acquire Toolbar instance");
                    return;
                }
                Verbose($"[2/4] Acquired instance: {toolbarInstance.GetType().FullName}");

                var rootField = toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
                if (rootField == null)
                {
                    Fail(InitState.RootNotFound,
                        $"Field 'm_Root' not found on {toolbarType.FullName}. Fields: " +
                        string.Join(", ", toolbarType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name)));
                    return;
                }
                var root = rootField.GetValue(toolbarInstance) as VisualElement;
                if (root == null)
                {
                    Fail(InitState.RootNotFound, "m_Root is null or not a VisualElement");
                    return;
                }
                Verbose($"[3/4] Got m_Root ({root.childCount} children)");

                var leftZone = root.Q("ToolbarZoneLeftAlign") ?? root.Q("ToolbarZoneLeft");
                var rightZone = root.Q("ToolbarZoneRightAlign") ?? root.Q("ToolbarZoneRight");
                if (leftZone == null || rightZone == null)
                {
                    var names = new System.Collections.Generic.List<string>();
                    root.Query<VisualElement>().ForEach(e => { if (!string.IsNullOrEmpty(e.name)) names.Add(e.name); });
                    Fail(InitState.RootNotFound,
                        "ToolbarZoneLeftAlign / ToolbarZoneRightAlign not found. Top-level names: " +
                        string.Join(", ", names.Distinct().Take(40)));
                    return;
                }

                LeftRoot = CreateSlotContainer("TGame.EditorToolBar.LeftRoot");
                RightRoot = CreateSlotContainer("TGame.EditorToolBar.RightRoot");

                leftZone.Add(LeftRoot);
                rightZone.Add(RightRoot);

                State = InitState.ContainersAttached;
                LastError = null;
                Debug.Log("[EditorToolBar] [4/4] Toolbar injection attached. Open 'Tools/Editor ToolBar/Settings' to configure.");
                // 容器挂上后立刻触发 Renderer 重建整棵树
                CustomToolbarRenderer.ForceRebuild();
            }
            catch (Exception ex)
            {
                Fail(InitState.AssembliesNotReady, $"Initialize threw: {ex}");
            }
        }

        private static VisualElement CreateSlotContainer(string name)
        {
            return new VisualElement
            {
                name = name,
                pickingMode = PickingMode.Position,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexShrink = 0,
                    // 跟原生工具栏高度一致
                    height = 18,
                    marginTop = 1,
                    marginBottom = 1,
                }
            };
        }

        private static object AcquireToolbarInstance(Type toolbarType)
        {
            // 路径 A:静态属性 "get"
            var getProp = toolbarType.GetProperty("get", BindingFlags.Public | BindingFlags.Static);
            if (getProp != null)
            {
                try
                {
                    var inst = getProp.GetValue(null);
                    if (inst != null) return inst;
                }
                catch (Exception ex) { Verbose($"Toolbar.get prop invoke failed: {ex.Message}"); }
            }

            // 路径 B:静态字段 s_Instances / s_Instance
            foreach (var fname in new[] { "s_Instances", "s_Instance", "s_toolbar" })
            {
                var f = toolbarType.GetField(fname, BindingFlags.NonPublic | BindingFlags.Static);
                if (f == null) continue;
                try
                {
                    var v = f.GetValue(null);
                    if (v is System.Collections.IEnumerable e)
                    {
                        foreach (var item in e)
                            if (item != null && item.GetType() == toolbarType) return item;
                    }
                    else if (v != null && v.GetType() == toolbarType) return v;
                }
                catch (Exception ex) { Verbose($"Toolbar.{fname} read failed: {ex.Message}"); }
            }

            // 路径 C:遍历 EditorWindow / ScriptableObject 兜底
            try
            {
                foreach (var win in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    if (win != null && win.GetType() == toolbarType) return win;
            }
            catch (Exception ex) { Verbose($"FindObjectsOfTypeAll<EditorWindow> fallback failed: {ex.Message}"); }
            try
            {
                foreach (var so in Resources.FindObjectsOfTypeAll<ScriptableObject>())
                    if (so != null && so.GetType() == toolbarType) return so;
            }
            catch (Exception ex) { Verbose($"FindObjectsOfTypeAll<ScriptableObject> fallback failed: {ex.Message}"); }

            return null;
        }

        private static void Fail(InitState state, string message)
        {
            State = state;
            LastError = message;
            Debug.LogWarning($"[EditorToolBar] {message}\nToolbar injection disabled. Try 'Tools/Editor ToolBar/Force Reattach'.");
        }

        private static void Verbose(string message)
        {
            if (SessionState.GetBool("TGame.EditorToolBar.Verbose", false))
                Debug.Log($"[EditorToolBar] {message}");
        }

        [MenuItem("Tools/Editor ToolBar/Force Reattach")]
        public static void Reattach()
        {
            Detach();
            State = InitState.NotStarted;
            LastError = null;
            Initialize();
            // 重新挂载后,触发一次重建
            if (State == InitState.ContainersAttached) CustomToolbarRenderer.ForceRebuild();
        }

        [MenuItem("Tools/Editor ToolBar/Enable Verbose Log")]
        public static void EnableVerbose() => SessionState.SetBool("TGame.EditorToolBar.Verbose", true);

        [MenuItem("Tools/Editor ToolBar/Disable Verbose Log")]
        public static void DisableVerbose() => SessionState.SetBool("TGame.EditorToolBar.Verbose", false);

        public static void Detach()
        {
            if (LeftRoot != null && LeftRoot.parent != null) LeftRoot.RemoveFromHierarchy();
            if (RightRoot != null && RightRoot.parent != null) RightRoot.RemoveFromHierarchy();
            LeftRoot = null;
            RightRoot = null;
            if (State == InitState.ContainersAttached) State = InitState.NotStarted;
        }
    }
}
