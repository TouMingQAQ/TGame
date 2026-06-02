using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using TGame;

namespace TGame.ToolBox
{
    [ToolBox("Debug")]
    public class DebugBox : IToolBoxContentVisualElement
    {
        private TDebugSettings _settings;
        private ObjectField _settingsField;
        private Label _statusLabel;

        public VisualElement CreateContent()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;

            // ── Header ──
            var header = new Label("TDebug 控制面板");
            header.style.fontSize = 18;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 12;
            root.Add(header);

            // ── 配置 SO ──
            root.Add(new Label("配置 SO") { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2 } });

            _settingsField = new ObjectField("Settings Asset")
            {
                objectType = typeof(TDebugSettings),
                value = FindOrCreateSettings()
            };
            _settingsField.RegisterValueChangedCallback(OnSettingsChanged);
            root.Add(_settingsField);

            var applyBtn = new Button(ApplySettings) { text = "应用到运行时" };
            applyBtn.style.marginTop = 4;
            root.Add(applyBtn);

            root.Add(new VisualElement { style = { height = 6 } });

            // ── 状态信息 ──
            _statusLabel = new Label(GetStatusText());
            _statusLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.marginBottom = 8;
            root.Add(_statusLabel);

            // ── 分隔线 ──
            root.Add(MakeSeparator());

            // ── 运行时控制 ──
            root.Add(new Label("运行时控制") { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6, marginTop = 6 } });

            var enableToggle = new Toggle("启用日志") { value = TDebug.IsEnabled };
            enableToggle.RegisterValueChangedCallback(evt => { TDebug.SetEnable(evt.newValue); RefreshStatus(); });
            root.Add(enableToggle);

            var minLevelField = new IntegerField("最低 Level") { value = TDebug.GetMinLevel() };
            minLevelField.RegisterValueChangedCallback(evt => { TDebug.SetMinLevel(evt.newValue); RefreshStatus(); });
            root.Add(minLevelField);

            root.Add(MakeSeparator());

            // ── 上下文控制 ──
            root.Add(new Label("上下文") { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });

            var contextTagField = new TextField("Tag") { value = TDebug.ContextTag ?? "" };
            contextTagField.RegisterValueChangedCallback(evt => TDebug.SetTag(evt.newValue));
            root.Add(contextTagField);

            var clearTagBtn = new Button(() =>
            {
                TDebug.ClearTag();
                contextTagField.value = "";
                RefreshStatus();
            })
            { text = "清空 Tag" };
            clearTagBtn.style.marginTop = 2;
            root.Add(clearTagBtn);

            var levelField = new IntegerField("Level") { value = TDebug.ContextLevel >= 0 ? TDebug.ContextLevel : 0 };
            levelField.RegisterValueChangedCallback(evt => TDebug.SetLevel(evt.newValue));
            root.Add(levelField);

            var resetLevelBtn = new Button(() =>
            {
                TDebug.ResetLevel();
                levelField.value = 0;
                RefreshStatus();
            })
            { text = "重置 Level" };
            resetLevelBtn.style.marginTop = 2;
            root.Add(resetLevelBtn);

            root.Add(MakeSeparator());

            // ── 文件日志 ──
            root.Add(new Label("文件日志") { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });

            var fileLogToggle = new Toggle("写入文件") { value = TDebug.FileLoggingEnabled };
            fileLogToggle.RegisterValueChangedCallback(evt => { TDebug.FileLoggingEnabled = evt.newValue; RefreshStatus(); });
            root.Add(fileLogToggle);

            var openLogBtn = new Button(OpenLogFile) { text = "打开日志文件夹" };
            openLogBtn.style.marginTop = 4;
            root.Add(openLogBtn);

            return root;
        }

        // ──────────────────────────────────────────
        //  内部
        // ──────────────────────────────────────────

        private void ApplySettings()
        {
            if (_settingsField.value is TDebugSettings so)
            {
                _settings = so;
                TDebug.Initialize(so);
                RefreshStatus();
            }
        }

        private void OnSettingsChanged(ChangeEvent<Object> evt)
        {
            _settings = evt.newValue as TDebugSettings;
        }

        private void RefreshStatus()
        {
            if (_statusLabel != null)
                _statusLabel.text = GetStatusText();
        }

        private string GetStatusText()
        {
            return $"Enable: {TDebug.IsEnabled}  |  MinLevel: {TDebug.GetMinLevel()}" +
                   $"\nContext Tag: {TDebug.ContextTag ?? "(none)"}  |  Context Level: {(TDebug.ContextLevel >= 0 ? TDebug.ContextLevel.ToString() : "(none)")}" +
                   $"\nFile Log: {(TDebug.FileLoggingEnabled ? "ON" : "OFF")}";
        }

        private static void OpenLogFile()
        {
            string dir = Path.Combine(Application.persistentDataPath, "Logs");
            if (Directory.Exists(dir))
            {
                EditorUtility.RevealInFinder(dir);
            }
            else
            {
                EditorUtility.RevealInFinder(Application.persistentDataPath);
            }
        }

        private static TDebugSettings FindOrCreateSettings()
        {
            var guids = AssetDatabase.FindAssets("t:TDebugSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<TDebugSettings>(path);
            }

            var settings = ScriptableObject.CreateInstance<TDebugSettings>();
            AssetDatabase.CreateAsset(settings, "Assets/Settings/TDebugSettings.asset");
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static VisualElement MakeSeparator()
        {
            return new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = new Color(0.3f, 0.3f, 0.3f),
                    marginTop = 6,
                    marginBottom = 6,
                    marginLeft = 0,
                    marginRight = 0
                }
            };
        }
    }
}
