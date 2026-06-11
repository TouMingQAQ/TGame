// CustomToolbarSettingsWindow.cs
// 配置窗口:启用/禁用、槽位、顺序、宽度、参数。
// 第一阶段不做拖拽排序,只做表格 + Order 数字输入。

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TGame.EditorToolBar
{
    public class CustomToolbarSettingsWindow : EditorWindow
    {
        private Vector2 _scroll;
        private SerializedObject _settingsSO;
        private string _filter = "";

        [MenuItem("Tools/Editor ToolBar/Settings")]
        public static void Open()
        {
            var win = GetWindow<CustomToolbarSettingsWindow>("ToolBar Designer");
            win.minSize = new Vector2(560, 320);
        }

        private void OnEnable()
        {
            _settingsSO = new SerializedObject(CustomToolbarSettings.GetOrCreate());
        }

        private void OnGUI()
        {
            if (_settingsSO == null || _settingsSO.targetObject == null)
            {
                _settingsSO = new SerializedObject(CustomToolbarSettings.GetOrCreate());
            }

            // Host 状态
            DrawHostStatus();

            EditorGUILayout.Space(4);
            DrawToolbar();

            _settingsSO.UpdateIfRequiredOrScript();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            var items = _settingsSO.FindProperty("Items");
            EditorGUILayout.PropertyField(items, new GUIContent("Registered Items"), true);
            if (_settingsSO.ApplyModifiedProperties())
            {
                // Settings 资产被修改 → 重建工具栏 UI 树
                CustomToolbarRenderer.ForceRebuild();
            }

            // 自动补全:如果发现新组件但没在配置里,点一下按钮
            DrawAutoFillHint();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            DrawFooter();
        }

        private void DrawHostStatus()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var state = CustomToolbarHost.State;
                var label = $"Host: {state}";
                if (state == CustomToolbarHost.InitState.ContainersAttached)
                    GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(220));
                else
                {
                    GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(220));
                    if (!string.IsNullOrEmpty(CustomToolbarHost.LastError))
                        GUILayout.Label(CustomToolbarHost.LastError, EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Reattach", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    CustomToolbarHost.Reattach();
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Rebuild Registry", EditorStyles.toolbarButton, GUILayout.Width(120)))
                {
                    CustomToolbarRegistry.Scan();
                    CustomToolbarRenderer.ForceRebuild();
                }
                if (GUILayout.Button("Reset Missing", EditorStyles.toolbarButton, GUILayout.Width(120)))
                {
                    RemoveMissingItems();
                }
            }
        }

        private void RemoveMissingItems()
        {
            var settings = CustomToolbarSettings.GetOrCreate();
            settings.Items.RemoveAll(c => CustomToolbarRegistry.FindById(c.Id) == null);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            CustomToolbarRenderer.ForceRebuild();
        }

        private void DrawAutoFillHint()
        {
            var settings = CustomToolbarSettings.GetOrCreate();
            var hasMissing = false;
            foreach (var item in CustomToolbarRegistry.Items)
            {
                if (!settings.Items.Exists(c => c.Id == item.Id))
                {
                    hasMissing = true;
                    break;
                }
            }
            if (hasMissing)
            {
                EditorGUILayout.HelpBox(
                    "Detected unregistered items. Click 'Auto-Fill Defaults' to add them with default settings.",
                    MessageType.Info);
                if (GUILayout.Button("Auto-Fill Defaults", GUILayout.Height(24)))
                {
                    CustomToolbarRenderer.ForceRebuild(); // ForceRebuild 内部会补全
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Settings Asset", GUILayout.Height(22)))
                {
                    Selection.activeObject = CustomToolbarSettings.GetOrCreate();
                    EditorGUIUtility.PingObject(CustomToolbarSettings.GetOrCreate());
                }
                if (GUILayout.Button("Open ToolBox (debug)", GUILayout.Height(22)))
                {
                    Debug.Log($"[EditorToolBar] Current items:\n{DescribeItems()}");
                }
            }
        }

        private string DescribeItems()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var item in CustomToolbarRegistry.Items)
            {
                sb.AppendLine($"  - {item.Id}  ({item.DisplayName})  default slot={item.DefaultSlot} order={item.DefaultOrder}");
            }
            return sb.ToString();
        }
    }
}
