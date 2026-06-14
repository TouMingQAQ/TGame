using System;
using System.Collections.Generic;
using TGame.Console;
using TGame.Console.Command;
using TGame.ToolBox;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Application = UnityEngine.Application;

namespace TGame.Console.Editor
{
    public class ConsoleBox : IToolBoxContentVisualElement
    {
        private const string UssPath = "Assets/Plugins/TGame/TGame.Console/Editor/ConsoleBox.uss";
        private const int MaxLogEntries = 500;

        private VisualElement _root;
        private TextField _inputField;
        private Button _keepBtn;
        private ScrollView _hintScroll;
        private ScrollView _logScroll;
        private readonly HashSet<CommandTip> _tips = new();
        private string _lastCommand;
        private bool _keepCommandOnSend;

        public VisualElement CreateContent()
        {
            _root = new VisualElement();
            _root.style.flexGrow = 1;
            _root.style.height = Length.Percent(100);
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.paddingLeft = 6;
            _root.style.paddingRight = 6;
            _root.style.paddingTop = 4;
            _root.style.paddingBottom = 4;

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (sheet != null)
                _root.styleSheets.Add(sheet);

            BuildBakeRow();
            BuildLogPanel();
            BuildHintList();
            BuildInputRow();

            Application.logMessageReceived += OnLogMessage;
            _root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                Application.logMessageReceived -= OnLogMessage;
            });

            RefreshHints();
            return _root;
        }

        private void BuildBakeRow()
        {
            var btn = new Button(ConsoleControl.Init);
            btn.text = "Init";
            btn.style.height = 26;
            btn.style.marginBottom = 4;
            _root.Add(btn);
        }

        private void BuildLogPanel()
        {
            var header = new Label("输出日志");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 12;
            header.style.marginBottom = 2;
            header.style.paddingBottom = 2;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.33f, 0.33f, 0.33f);
            header.style.flexShrink = 0;
            _root.Add(header);

            _logScroll = new ScrollView();
            _logScroll.style.flexGrow = 1;
            _logScroll.style.flexShrink = 1;
            _logScroll.name = "log-list";
            _root.Add(_logScroll);
        }

        private void BuildHintList()
        {
            _hintScroll = new ScrollView();
            _hintScroll.style.maxHeight = 200;
            _hintScroll.style.flexShrink = 0;
            _hintScroll.style.marginTop = 4;
            _hintScroll.style.marginBottom = 4;
            _hintScroll.style.overflow = Overflow.Hidden;
            _hintScroll.name = "hint-list";
            _root.Add(_hintScroll);
        }

        private void BuildInputRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexShrink = 0;

            _inputField = new TextField();
            _inputField.style.flexGrow = 1;
            _inputField.RegisterValueChangedCallback(OnInputChanged);
            _inputField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    ExecuteCommand();
            });
            row.Add(_inputField);

            var sendBtn = new Button(ExecuteCommand);
            sendBtn.text = "Send";
            sendBtn.style.width = 60;
            sendBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(sendBtn);

            _keepBtn = new Button(ToggleKeep);
            _keepBtn.text = "◈";
            _keepBtn.style.width = 30;
            _keepBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _keepBtn.tooltip = "保留指令（开启后发送不清理输入框）";
            row.Add(_keepBtn);

            UpdateKeepButtonStyle();

            _root.Add(row);
        }

        private void OnInputChanged(ChangeEvent<string> evt)
        {
            RefreshHints();
        }

        private void RefreshHints()
        {
            var text = _inputField.value;
            if (text == _lastCommand) return;
            _lastCommand = text;

            ConsoleControl.CommandTipList(text, in _tips);
            _hintScroll.Clear();

            foreach (var tip in _tips)
            {
                var label = new Label(tip.ShowStr);
                label.enableRichText = true;
                label.style.fontSize = 12;
                label.style.paddingTop = 2;
                label.style.paddingBottom = 2;
                label.style.paddingLeft = 4;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.AddToClassList("console-hint-item");
                label.userData = tip;
                label.RegisterCallback<PointerDownEvent>(OnHintClicked);
                _hintScroll.Add(label);
            }
        }

        private void OnHintClicked(PointerDownEvent evt)
        {
            if (evt.target is Label label && label.userData is CommandTip tip)
            {
                switch (tip.TipType)
                {
                    case CommandTipType.ValueTip:
                        _inputField.value += tip.InputStr;
                        break;
                    default:
                        _inputField.value = tip.InputStr;
                        break;
                }
                _inputField.Focus();
            }
        }

        private void ToggleKeep()
        {
            _keepCommandOnSend = !_keepCommandOnSend;
            UpdateKeepButtonStyle();
        }

        private void UpdateKeepButtonStyle()
        {
            if (_keepBtn == null) return;
            if (_keepCommandOnSend)
            {
                _keepBtn.style.backgroundColor = new Color(0.3f, 0.6f, 1f, 0.6f);
                _keepBtn.style.color = Color.white;
            }
            else
            {
                _keepBtn.style.backgroundColor = StyleKeyword.Undefined;
                _keepBtn.style.color = new Color(0.7f, 0.7f, 0.7f);
            }
        }

        private void ExecuteCommand()
        {
            var text = _inputField.value;
            if (string.IsNullOrEmpty(text))
                return;

            ConsoleControl.ExecuteCommand(text);

            if (!_keepCommandOnSend)
            {
                _inputField.value = string.Empty;
                RefreshHints();
            }
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var prefix = type switch
            {
                LogType.Warning => "<color=#FFAA00>[Warn]</color>",
                LogType.Error or LogType.Exception or LogType.Assert => "<color=#FF4444>[Error]</color>",
                _ => "<color=#888888>[Log]</color>"
            };

            var label = new Label($"{prefix} <color=#aaaaaa>[{timestamp}]</color> {condition}");
            label.enableRichText = true;
            label.style.fontSize = 11;
            label.style.paddingTop = 1;
            label.style.paddingBottom = 1;
            label.style.paddingLeft = 4;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.AddToClassList("console-log-entry");

            switch (type)
            {
                case LogType.Warning:
                    label.AddToClassList("console-log-entry--warning");
                    break;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    label.AddToClassList("console-log-entry--error");
                    break;
                default:
                    label.AddToClassList("console-log-entry--log");
                    break;
            }

            _logScroll.Add(label);

            while (_logScroll.childCount > MaxLogEntries)
                _logScroll.RemoveAt(0);

            _logScroll.ScrollTo(label);
        }
    }
}
