using System.Collections.Generic;
using TGame.Console;
using TGame.Console.Command;
using UnityEditor;
using UnityEngine;

namespace TGame.Console.Editor
{
    [CreateAssetMenu(fileName = "ConsoleEditor",menuName = "TGame/Console/ConsoleEditor")]
    public class ConsoleEditor : ScriptableObject
    {

    }
    [CustomEditor(typeof(ConsoleEditor))]
    public class ConsoleEditorView : UnityEditor.Editor
    {
        private string commandValue;
        private string commandValueCache;
        private HashSet<CommandTip> commandList = new HashSet<CommandTip>();

        private Vector2 scrollViewPos;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();



            GUILayout.Label("输入指令");
            GUILayout.BeginHorizontal();
            commandValue = GUILayout.TextField(commandValue);
            var send = DrawRichTextButton("<color=#66ccff>Send</color>",GUILayout.Width(80));
            GUILayout.EndHorizontal();
            if (commandValueCache != commandValue)
            {
                ConsoleControl.CommandTipList(commandValue, in commandList);
                commandValueCache = commandValue;
            }
            scrollViewPos = GUILayout.BeginScrollView(scrollViewPos, false, false);
            foreach (var commandTip in commandList)
            {
                var inputStr = commandTip.InputStr;
                var showStr = commandTip.ShowStr;
                var click = DrawRichTextButton(showStr,GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth));
                if (click)
                {
                    switch (commandTip.TipType)
                    {
                        case CommandTipType.ValueTip:
                            commandValue += inputStr;
                            break;
                        case CommandTipType.ClassLevel:
                        case CommandTipType.MethodLevel:
                        default:
                            commandValue = inputStr;
                            break;
                    }
                    Repaint();
                }
            }
            GUILayout.EndScrollView();

            if (send)
            {
                if(string.IsNullOrEmpty(commandValue))
                    return;
                ConsoleControl.ExecuteCommand(commandValue);
                commandValue = string.Empty;
                commandValueCache = string.Empty;
                commandList.Clear();
                Repaint();
            }
            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }
        private bool DrawRichTextButton(string richText, params GUILayoutOption[] options)
        {
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(richText), EditorStyles.label, options);
            EditorGUI.LabelField(rect, richText, new GUIStyle(EditorStyles.label)
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            });
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                GUI.FocusControl(null);
                Event.current.Use();
                return true;
            }
            if (rect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(rect, new Color(0.6f, 0.8f, 1f, 0.3f));
            }
            return false;
        }
    }
}