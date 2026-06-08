using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using TGame;

namespace TGame.ToolBox
{
    public class DebugBox : IToolBoxContentVisualElement
    {
        private TDebugSettings _settings;
        private ObjectField _settingsField;
        private Label _statusLabel;

        public VisualElement CreateContent()
        {
            var root = new VisualElement();

            // ── Header ──
            var header = new Label("TDebug 控制面板");
            header.AddToClassList("tbx-section-title");
            root.Add(header);

            // ── 配置 SO ──
            var configLabel = new Label("配置 SO");
            configLabel.AddToClassList("tbx-subtitle");
            root.Add(configLabel);

            _settingsField = new ObjectField("Settings Asset")
            {
                objectType = typeof(TDebugSettings),
                value = FindOrCreateSettings()
            };
            _settingsField.RegisterValueChangedCallback(OnSettingsChanged);
            root.Add(_settingsField);

            var applyBtn = new Button(ApplySettings) { text = "应用到运行时" };
            applyBtn.AddToClassList("tbx-btn-secondary");
            applyBtn.style.marginTop = 4;
            root.Add(applyBtn);

            // ── 状态信息 card ──
            var statusCard = new VisualElement();
            statusCard.AddToClassList("tbx-card");
            root.Add(statusCard);

            _statusLabel = new Label(GetStatusText());
            _statusLabel.AddToClassList("tbx-label");
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            statusCard.Add(_statusLabel);

            var sep1 = new VisualElement();
            sep1.AddToClassList("tbx-separator");
            root.Add(sep1);

            // ── 运行时控制 ──
            var rtLabel = new Label("运行时控制");
            rtLabel.AddToClassList("tbx-subtitle");
            root.Add(rtLabel);

            var rtCard = new VisualElement();
            rtCard.AddToClassList("tbx-card-tight");
            root.Add(rtCard);

            var enableToggle = new Toggle("启用日志") { value = TDebug.IsEnabled };
            enableToggle.RegisterValueChangedCallback(evt => { TDebug.SetEnable(evt.newValue); RefreshStatus(); });
            rtCard.Add(enableToggle);

            var minLevelField = new IntegerField("最低 Level") { value = TDebug.GetMinLevel() };
            minLevelField.RegisterValueChangedCallback(evt => { TDebug.SetMinLevel(evt.newValue); RefreshStatus(); });
            rtCard.Add(minLevelField);

            var sep2 = new VisualElement();
            sep2.AddToClassList("tbx-separator");
            root.Add(sep2);

            // ── 上下文控制 ──
            var ctxLabel = new Label("上下文控制");
            ctxLabel.AddToClassList("tbx-subtitle");
            root.Add(ctxLabel);

            var ctxCard = new VisualElement();
            ctxCard.AddToClassList("tbx-card-tight");
            root.Add(ctxCard);

            var contextTagField = new TextField("Tag") { value = TDebug.ContextTag ?? "" };
            contextTagField.RegisterValueChangedCallback(evt => TDebug.SetTag(evt.newValue));
            ctxCard.Add(contextTagField);

            var clearTagBtn = new Button(() =>
            {
                TDebug.ClearTag();
                contextTagField.value = "";
                RefreshStatus();
            })
            { text = "清空 Tag" };
            clearTagBtn.AddToClassList("tbx-btn-secondary");
            clearTagBtn.style.marginTop = 2;
            ctxCard.Add(clearTagBtn);

            var levelField = new IntegerField("Level") { value = TDebug.ContextLevel >= 0 ? TDebug.ContextLevel : 0 };
            levelField.RegisterValueChangedCallback(evt => TDebug.SetLevel(evt.newValue));
            ctxCard.Add(levelField);

            var resetLevelBtn = new Button(() =>
            {
                TDebug.ResetLevel();
                levelField.value = 0;
                RefreshStatus();
            })
            { text = "重置 Level" };
            resetLevelBtn.AddToClassList("tbx-btn-secondary");
            resetLevelBtn.style.marginTop = 2;
            ctxCard.Add(resetLevelBtn);

            var sep3 = new VisualElement();
            sep3.AddToClassList("tbx-separator");
            root.Add(sep3);

            // ── 文件日志 ──
            var fileLabel = new Label("文件日志");
            fileLabel.AddToClassList("tbx-subtitle");
            root.Add(fileLabel);

            var fileCard = new VisualElement();
            fileCard.AddToClassList("tbx-card-tight");
            root.Add(fileCard);

            var fileLogToggle = new Toggle("写入文件") { value = TDebug.FileLoggingEnabled };
            fileLogToggle.RegisterValueChangedCallback(evt => { TDebug.FileLoggingEnabled = evt.newValue; RefreshStatus(); });
            fileCard.Add(fileLogToggle);

            var openLogBtn = new Button(OpenLogFile) { text = "打开日志文件夹" };
            openLogBtn.AddToClassList("tbx-btn-secondary");
            openLogBtn.style.marginTop = 4;
            fileCard.Add(openLogBtn);

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
    }
}
