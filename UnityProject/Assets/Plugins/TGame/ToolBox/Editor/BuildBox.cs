using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.ToolBox
{
    public class BuildBox : IToolBoxContentVisualElement
    {
        public static BoxRegistration Registration => new()
        {
            Name = "构建打包",
            Group = "构建",
            Icon = "Prefab Icon",
            Factory = () => new BuildBox().CreateContent()
        };

        private const string ConfigPath = "Assets/Resources/BuildConfig.asset";
        private const string PrefOutputPath = "TGame.BuildBox.OutputPath";
        private const string PrefABOutputPath = "TGame.BuildBox.ABOutputPath";

        private const string PrefDevBuild = "TGame.BuildBox.DevBuild";
        private const string PrefProfiler = "TGame.BuildBox.Profiler";
        private const string PrefDeepProfiling = "TGame.BuildBox.DeepProfiling";
        private const string PrefScriptsOnly = "TGame.BuildBox.ScriptsOnly";
        private const string PrefCleanBuild = "TGame.BuildBox.CleanBuild";
        private const string PrefABFullRebuild = "TGame.BuildBox.ABFullRebuild";
        private const string PrefUseCustomPipeline = "TGame.BuildBox.UseCustomPipeline";
        private const string PrefUseCustomABPipeline = "TGame.BuildBox.UseCustomABPipeline";

        private const string PrefFoldoutVersion = "TGame.BuildBox.FoldoutVersion";
        private const string PrefFoldoutConfig  = "TGame.BuildBox.FoldoutConfig";
        private const string PrefFoldoutAB      = "TGame.BuildBox.FoldoutAB";
        private const string PrefFoldoutPlayer  = "TGame.BuildBox.FoldoutPlayer";

        // --- profile state ---
        private BuildConfig _config;
        private List<BuildProfile> _profiles = new();
        private BuildProfile _selectedProfile;

        // --- pipeline state ---
        private List<(string name, Type type)> _playerPipelines = new();
        private List<(string name, Type type)> _abPipelines = new();

        // --- UI element refs ---
        private Label _versionProduct, _versionCompany, _versionBundle, _versionUnity;
        private Label _versionAndroid, _versionIos;
        private VisualElement _versionOtherPlatforms;
        private DropdownField _profileDropdown;
        private Label _platformLabel;
        private TextField _outputPathField;
        private Toggle _toggleDev, _toggleProfiler, _toggleDeep, _toggleScriptsOnly, _toggleClean;
        private TextField _abOutputPathField;
        private Toggle _toggleABFullRebuild;
        private DropdownField _abPipelineDropdown;
        private DropdownField _playerPipelineDropdown;
        private HelpBox _statusHelp;
        private VisualElement _playerFoldoutContent;
        private VisualElement _profileCreatePanel;

        public VisualElement CreateContent()
        {
            LoadConfig();
            LoadProfiles();
            DiscoverPipelines();
            RefreshPlatformBuildNumberEntries();

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = 6;
            scrollView.style.paddingRight = 6;
            scrollView.style.paddingTop = 4;

            BuildVersionSection(scrollView);
            BuildConfigSection(scrollView);
            BuildAssetBundleSection(scrollView);
            BuildPlayerSection(scrollView);
            BuildStatusBar(scrollView);

            return scrollView;
        }

        // ============================================================
        // Version Info Section
        // ============================================================

        private void BuildVersionSection(VisualElement root)
        {
            var foldout = new Foldout();
            foldout.text = "版本信息";
            foldout.value = EditorPrefs.GetBool(PrefFoldoutVersion, true);
            root.Add(foldout);
            foldout.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(PrefFoldoutVersion, evt.newValue));

            var container = new VisualElement();
            container.style.marginLeft = 12;
            foldout.Add(container);

            _versionProduct = AddReadOnlyField(container, "Product Name");
            _versionCompany = AddReadOnlyField(container, "Company Name");
            _versionBundle  = AddReadOnlyField(container, "Bundle Version");
            _versionUnity   = AddReadOnlyField(container, "Unity Version");

            var sep = new Label();
            sep.AddToClassList("tbx-separator");
            container.Add(sep);

            _versionAndroid = AddReadOnlyField(container, "Android bundleVersionCode");
            _versionIos     = AddReadOnlyField(container, "iOS buildNumber");
            _versionOtherPlatforms = new VisualElement();
            container.Add(_versionOtherPlatforms);

            RefreshVersionInfo();

            // Register a refresh when the foldout is expanded
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) RefreshVersionInfo();
            });
        }

        private static Label AddReadOnlyField(VisualElement parent, string label)
        {
            var row = new VisualElement();
            row.AddToClassList("tbx-form-row");
            var lbl = new Label(label);
            lbl.AddToClassList("tbx-form-label-wide");
            row.Add(lbl);
            var value = new Label();
            value.AddToClassList("tbx-value");
            row.Add(value);
            parent.Add(row);
            return value;
        }

        private void RefreshVersionInfo()
        {
            if (_versionProduct == null) return;

            _versionProduct.text = PlayerSettings.productName;
            _versionCompany.text = PlayerSettings.companyName;
            _versionBundle.text  = PlayerSettings.bundleVersion;
            _versionUnity.text   = Application.unityVersion;

            _versionAndroid.text = PlayerSettings.Android.bundleVersionCode.ToString();
            _versionIos.text     = PlayerSettings.iOS.buildNumber;

            // Other platforms (from SO)
            _versionOtherPlatforms.Clear();
            foreach (var pnb in _config.platformBuildNumbers)
            {
                var label = AddReadOnlyField(_versionOtherPlatforms, pnb.platformName);
                label.text = pnb.buildNumber.ToString();
            }
        }

        // ============================================================
        // Build Config Section
        // ============================================================

        private void BuildConfigSection(VisualElement root)
        {
            var foldout = new Foldout();
            foldout.text = "构建配置";
            foldout.value = EditorPrefs.GetBool(PrefFoldoutConfig, true);
            root.Add(foldout);
            foldout.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(PrefFoldoutConfig, evt.newValue));

            var container = new VisualElement();
            container.style.marginLeft = 12;
            foldout.Add(container);

            // ---- Profile selector ----
            BuildProfileSelector(container);

            // ---- Platform display (read-only) ----
            var platformRow = new VisualElement();
            platformRow.style.flexDirection = FlexDirection.Row;
            platformRow.style.alignItems = Align.Center;
            platformRow.style.marginBottom = 4;
            container.Add(platformRow);

            var platformLabel = new Label("目标平台");
            platformLabel.style.width = 100;
            platformLabel.style.fontSize = 11;
            platformLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            platformRow.Add(platformLabel);

            _platformLabel = new Label("(未选择)");
            _platformLabel.style.flexGrow = 1;
            _platformLabel.style.fontSize = 11;
            platformRow.Add(_platformLabel);

            // ---- Output path ----
            var pathRow = new VisualElement();
            pathRow.style.flexDirection = FlexDirection.Row;
            pathRow.style.alignItems = Align.Center;
            pathRow.style.marginBottom = 4;
            container.Add(pathRow);

            var pathLabel = new Label("输出路径");
            pathLabel.style.width = 100;
            pathLabel.style.fontSize = 11;
            pathLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            pathRow.Add(pathLabel);

            _outputPathField = new TextField();
            _outputPathField.value = _config.outputPath;
            _outputPathField.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetString(PrefOutputPath, evt.newValue);
            });
            _outputPathField.style.flexGrow = 1;
            pathRow.Add(_outputPathField);

            var browseBtn = new Button(() =>
            {
                var path = EditorUtility.OpenFolderPanel("选择输出路径", _outputPathField.value, "");
                if (!string.IsNullOrEmpty(path))
                {
                    _outputPathField.value = MakeRelativePath(path);
                }
            });
            browseBtn.text = "浏览...";
            browseBtn.style.width = 60;
            pathRow.Add(browseBtn);

            // ---- Build option toggles ----
            _toggleDev = AddToggle(container, "Development Build", _config.developmentBuild);
            _toggleDev.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(PrefDevBuild, evt.newValue));
            _toggleProfiler = AddToggle(container, "Autoconnect Profiler", _config.autoconnectProfiler);
            _toggleProfiler.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(PrefProfiler, evt.newValue));
            _toggleDeep = AddToggle(container, "Deep Profiling", _config.deepProfiling);
            _toggleDeep.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(PrefDeepProfiling, evt.newValue));
            _toggleScriptsOnly = AddToggle(container, "Scripts Only Build", _config.buildScriptsOnly);
            _toggleScriptsOnly.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(PrefScriptsOnly, evt.newValue));
            _toggleClean = AddToggle(container, "Clean Build", _config.cleanBuild);
            _toggleClean.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(PrefCleanBuild, evt.newValue));
        }

        /// <summary> 构建 Profile 下拉选择器 + 管理按钮 </summary>
        private void BuildProfileSelector(VisualElement parent)
        {
            // ---- 选择行 ----
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            parent.Add(row);

            var profileLabel = new Label("构建档案");
            profileLabel.style.width = 100;
            profileLabel.style.fontSize = 11;
            profileLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            row.Add(profileLabel);

            RefreshProfileList();

            _profileDropdown = new DropdownField(_profiles.Select(p => p.name).ToList(), 0);
            _profileDropdown.style.flexGrow = 1;
            _profileDropdown.RegisterValueChangedCallback(OnProfileSelected);
            row.Add(_profileDropdown);

            // ---- 管理按钮 ----
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginLeft = 100;
            btnRow.style.marginBottom = 6;
            parent.Add(btnRow);

            var createBtn = new Button(() => ToggleCreatePanel(true)) { text = "新建" };
            createBtn.style.width = 60;
            btnRow.Add(createBtn);

            var renameBtn = new Button(RenameSelectedProfile) { text = "重命名" };
            renameBtn.style.width = 60;
            btnRow.Add(renameBtn);

            var deleteBtn = new Button(DeleteSelectedProfile) { text = "删除" };
            deleteBtn.style.width = 60;
            deleteBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btnRow.Add(deleteBtn);

            // ---- 新建 Profile 面板 (默认隐藏) ----
            _profileCreatePanel = new VisualElement();
            _profileCreatePanel.style.display = DisplayStyle.None;
            _profileCreatePanel.style.marginLeft = 100;
            _profileCreatePanel.style.marginBottom = 6;
            _profileCreatePanel.style.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
            _profileCreatePanel.style.paddingLeft = 6;
            _profileCreatePanel.style.paddingRight = 6;
            _profileCreatePanel.style.paddingTop = 6;
            _profileCreatePanel.style.paddingBottom = 6;
            parent.Add(_profileCreatePanel);

            var createTitle = new Label("新建 BuildProfile");
            createTitle.style.fontSize = 12;
            createTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            createTitle.style.marginBottom = 4;
            _profileCreatePanel.Add(createTitle);

            var createNameField = new TextField("名称");
            createNameField.value = "NewProfile";
            _profileCreatePanel.Add(createNameField);

            var createPlatformDropdown = new DropdownField("平台", BuildProfileManager.PlatformNames.ToList(), 0);
            _profileCreatePanel.Add(createPlatformDropdown);

            var createBtnRow = new VisualElement();
            createBtnRow.style.flexDirection = FlexDirection.Row;
            createBtnRow.style.marginTop = 4;
            _profileCreatePanel.Add(createBtnRow);

            var confirmCreateBtn = new Button(() =>
            {
                var name = createNameField.value;

                if (_renameMode)
                {
                    // ---- 重命名模式 ----
                    if (_selectedProfile != null && !string.IsNullOrEmpty(name) && name != _selectedProfile.name)
                    {
                        BuildProfileManager.RenameProfile(_selectedProfile, name);
                        RefreshProfileList();
                        ShowStatus($"已重命名为: {name}", HelpBoxMessageType.Info);
                    }
                    _renameMode = false;
                }
                else
                {
                    // ---- 创建模式 ----
                    var platform = BuildProfileManager.PlatformNames[createPlatformDropdown.index];
                    var profile = BuildProfileManager.CreateProfile(name);
                    if (profile != null)
                    {
                        LoadProfiles();
                        RefreshProfileList();
                        SelectProfile(profile);
                    }
                }

                ToggleCreatePanel(false);
                createNameField.value = "NewProfile";
                createPlatformDropdown.index = 0;
                createPlatformDropdown.style.display = DisplayStyle.Flex;
            })
            { text = "确认" };
            confirmCreateBtn.style.width = 80;
            createBtnRow.Add(confirmCreateBtn);

            var cancelCreateBtn = new Button(() => ToggleCreatePanel(false)) { text = "取消" };
            cancelCreateBtn.style.width = 60;
            cancelCreateBtn.style.marginLeft = 4;
            createBtnRow.Add(cancelCreateBtn);
        }

        private void ToggleCreatePanel(bool show)
        {
            _profileCreatePanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnProfileSelected(ChangeEvent<string> evt)
        {
            var idx = _profileDropdown.index;
            if (idx >= 0 && idx < _profiles.Count)
                SelectProfile(_profiles[idx]);
        }

        private void SelectProfile(BuildProfile profile)
        {
            _selectedProfile = profile;

            if (profile != null)
            {
                _platformLabel.text = "平台由 BuildProfile 组件决定";

                // 持久化 GUID
                var guid = BuildProfileManager.GetProfileGUID(profile);
                _config.lastProfileGUID = guid;
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssetIfDirty(_config);
            }
            else
            {
                _platformLabel.text = "(未选择)";
            }
        }

        private void RefreshProfileList()
        {
            var selectedName = _selectedProfile?.name;
            _profiles = BuildProfileManager.GetAllProfiles();

            if (_profileDropdown != null)
            {
                _profileDropdown.choices = _profiles.Select(p => p.name).ToList();
                if (!string.IsNullOrEmpty(selectedName))
                {
                    var restoreIdx = _profiles.FindIndex(p => p.name == selectedName);
                    if (restoreIdx >= 0)
                        _profileDropdown.index = restoreIdx;
                }
            }

            // 恢复上次选中的 Profile
            if (_selectedProfile == null && !string.IsNullOrEmpty(_config.lastProfileGUID))
            {
                var lastProfile = BuildProfileManager.FindByGUID(_config.lastProfileGUID);
                if (lastProfile != null)
                    SelectProfile(lastProfile);
            }

            // 默认选中第一个
            if (_selectedProfile == null && _profiles.Count > 0 && _profileDropdown != null)
            {
                _profileDropdown.index = 0;
                SelectProfile(_profiles[0]);
            }
        }

        private void LoadProfiles()
        {
            _profiles = BuildProfileManager.GetAllProfiles();

            // 恢复上次选中的 Profile
            if (!string.IsNullOrEmpty(_config.lastProfileGUID))
            {
                _selectedProfile = BuildProfileManager.FindByGUID(_config.lastProfileGUID);
            }
        }

        private bool _renameMode; // 标记当前是创建模式还是重命名模式

        private void RenameSelectedProfile()
        {
            if (_selectedProfile == null)
            {
                ShowStatus("请先选择一个 Profile", HelpBoxMessageType.Warning);
                return;
            }

            // 将面板切换为重命名模式
            _renameMode = true;
            var nameField = _profileCreatePanel.Q<TextField>();
            if (nameField != null)
                nameField.value = _selectedProfile.name;

            // 隐藏平台选择（重命名不需要改平台）
            var platformField = _profileCreatePanel.Q<DropdownField>();
            if (platformField != null)
                platformField.style.display = DisplayStyle.None;

            ToggleCreatePanel(true);
        }

        private void DeleteSelectedProfile()
        {
            if (_selectedProfile == null)
            {
                ShowStatus("请先选择一个 Profile", HelpBoxMessageType.Warning);
                return;
            }

            var profileName = _selectedProfile.name;
            if (!EditorUtility.DisplayDialog("确认删除", $"确定要删除 Profile \"{profileName}\" 吗？\n此操作不可恢复。", "删除", "取消"))
                return;

            BuildProfileManager.DeleteProfile(_selectedProfile);
            _selectedProfile = null;
            _config.lastProfileGUID = "";
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssetIfDirty(_config);

            LoadProfiles();
            RefreshProfileList();

            ShowStatus($"已删除 Profile: {profileName}", HelpBoxMessageType.Info);
        }

        private static Toggle AddToggle(VisualElement parent, string label, bool defaultValue)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            parent.Add(row);

            var toggle = new Toggle(label);
            toggle.value = defaultValue;
            toggle.style.flexGrow = 1;
            row.Add(toggle);
            return toggle;
        }

        // ============================================================
        // AssetBundle Build Section
        // ============================================================

        private void BuildAssetBundleSection(VisualElement root)
        {
            var foldout = new Foldout();
            foldout.text = "AssetBundle 构建";
            foldout.value = EditorPrefs.GetBool(PrefFoldoutAB, false);
            root.Add(foldout);
            foldout.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(PrefFoldoutAB, evt.newValue));

            var container = new VisualElement();
            container.style.marginLeft = 12;
            foldout.Add(container);

            // AB output path
            var abPathRow = new VisualElement();
            abPathRow.style.flexDirection = FlexDirection.Row;
            abPathRow.style.alignItems = Align.Center;
            abPathRow.style.marginBottom = 4;
            container.Add(abPathRow);

            var abPathLabel = new Label("AB 输出路径");
            abPathLabel.style.width = 100;
            abPathLabel.style.fontSize = 11;
            abPathLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            abPathRow.Add(abPathLabel);

            _abOutputPathField = new TextField();
            _abOutputPathField.value = _config.abOutputPath;
            _abOutputPathField.RegisterValueChangedCallback(evt => EditorPrefs.SetString(PrefABOutputPath, evt.newValue));
            _abOutputPathField.style.flexGrow = 1;
            abPathRow.Add(_abOutputPathField);

            var abBrowseBtn = new Button(() =>
            {
                var path = EditorUtility.OpenFolderPanel("选择 AB 输出路径", _abOutputPathField.value, "");
                if (!string.IsNullOrEmpty(path))
                    _abOutputPathField.value = MakeRelativePath(path);
            });
            abBrowseBtn.text = "浏览...";
            abBrowseBtn.style.width = 60;
            abPathRow.Add(abBrowseBtn);

            // Incremental / Full rebuild
            _toggleABFullRebuild = AddToggle(container, "全量构建 (Force Rebuild)", _config.abFullRebuild);
            _toggleABFullRebuild.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(PrefABFullRebuild, evt.newValue));

            // AB pipeline selection
            BuildABPipelineSelector(container);

            // Build button
            var buildABBtn = new Button(BuildAssetBundles);
            buildABBtn.text = "构建 AssetBundle";
            buildABBtn.style.width = 200;
            buildABBtn.style.height = 28;
            buildABBtn.style.marginTop = 6;
            buildABBtn.style.fontSize = 13;
            buildABBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(buildABBtn);
        }

        private void BuildABPipelineSelector(VisualElement parent)
        {
            var pipelineLabel = new Label("AB 流水线");
            pipelineLabel.AddToClassList("tbx-label");
            pipelineLabel.style.marginTop = 4;
            pipelineLabel.style.marginBottom = 2;
            parent.Add(pipelineLabel);

            _abPipelineDropdown = new DropdownField();
            _abPipelineDropdown.AddToClassList("tbx-form-field");
            parent.Add(_abPipelineDropdown);

            // Build choices: "默认" + custom pipeline names
            var choices = new List<string> { "默认" };
            choices.AddRange(_abPipelines.Select(p => p.name));
            _abPipelineDropdown.choices = choices;

            // Restore saved selection
            if (_config.useCustomABPipeline && !string.IsNullOrEmpty(_config.selectedABPipelineTypeName))
            {
                var savedIdx = _abPipelines.FindIndex(p => p.type.FullName == _config.selectedABPipelineTypeName);
                _abPipelineDropdown.index = savedIdx >= 0 ? savedIdx + 1 : 0;
            }
            else
            {
                _abPipelineDropdown.index = 0;
            }

            _abPipelineDropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = _abPipelineDropdown.index;
                if (idx <= 0)
                {
                    _config.useCustomABPipeline = false;
                    _config.selectedABPipelineTypeName = "";
                    EditorPrefs.SetBool(PrefUseCustomABPipeline, false);
                }
                else
                {
                    var pipeIdx = idx - 1;
                    if (pipeIdx < _abPipelines.Count)
                    {
                        _config.useCustomABPipeline = true;
                        _config.selectedABPipelineTypeName = _abPipelines[pipeIdx].type.FullName;
                        EditorPrefs.SetBool(PrefUseCustomABPipeline, true);
                    }
                }
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssetIfDirty(_config);
            });
        }

        // ============================================================
        // Player Build Section
        // ============================================================

        private void BuildPlayerSection(VisualElement root)
        {
            var foldout = new Foldout();
            foldout.text = "Player 构建";
            foldout.value = EditorPrefs.GetBool(PrefFoldoutPlayer, true);
            root.Add(foldout);
            foldout.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(PrefFoldoutPlayer, evt.newValue));

            _playerFoldoutContent = new VisualElement();
            _playerFoldoutContent.style.marginLeft = 12;
            foldout.Add(_playerFoldoutContent);

            // Pipeline selection
            var pipelineLabel = new Label("构建流水线");
            pipelineLabel.style.fontSize = 11;
            pipelineLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            pipelineLabel.style.marginBottom = 2;
            _playerFoldoutContent.Add(pipelineLabel);

            _playerPipelineDropdown = new DropdownField();
            _playerPipelineDropdown.AddToClassList("tbx-form-field");
            _playerFoldoutContent.Add(_playerPipelineDropdown);

            // Build choices: "默认 (BuildPipeline.BuildPlayer)" + custom pipeline names
            var choices = new List<string> { "默认 (BuildPipeline.BuildPlayer)" };
            choices.AddRange(_playerPipelines.Select(p => p.name));
            _playerPipelineDropdown.choices = choices;

            // Restore saved selection
            if (_config.useCustomPipeline && !string.IsNullOrEmpty(_config.selectedPipelineTypeName))
            {
                var savedIdx = _playerPipelines.FindIndex(p => p.type.FullName == _config.selectedPipelineTypeName);
                _playerPipelineDropdown.index = savedIdx >= 0 ? savedIdx + 1 : 0;
            }
            else
            {
                _playerPipelineDropdown.index = 0;
            }

            _playerPipelineDropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = _playerPipelineDropdown.index;
                if (idx <= 0)
                {
                    _config.useCustomPipeline = false;
                    _config.selectedPipelineTypeName = "";
                    EditorPrefs.SetBool(PrefUseCustomPipeline, false);
                }
                else
                {
                    var pipeIdx = idx - 1;
                    if (pipeIdx < _playerPipelines.Count)
                    {
                        _config.useCustomPipeline = true;
                        _config.selectedPipelineTypeName = _playerPipelines[pipeIdx].type.FullName;
                        EditorPrefs.SetBool(PrefUseCustomPipeline, true);
                    }
                }
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssetIfDirty(_config);
            });

            // Warning text
            var warning = new Label("提示：每次构建前将自动递增对应平台的构建号码");
            warning.style.fontSize = 11;
            warning.style.color = new Color(0.8f, 0.7f, 0.2f);
            warning.style.marginBottom = 6;
            warning.style.marginTop = 4;
            _playerFoldoutContent.Add(warning);

            // Build button
            var buildBtn = new Button(BuildPlayer);
            buildBtn.text = "构建 Player";
            buildBtn.style.width = 200;
            buildBtn.style.height = 32;
            buildBtn.style.fontSize = 14;
            buildBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            buildBtn.style.marginBottom = 4;
            _playerFoldoutContent.Add(buildBtn);

            // Copy CLI command button
            var copyCliBtn = new Button(CopyCLICommand);
            copyCliBtn.text = "复制 CLI 指令";
            copyCliBtn.style.width = 200;
            copyCliBtn.style.marginBottom = 4;
            _playerFoldoutContent.Add(copyCliBtn);

            // Open output button
            var openBtn = new Button(OpenOutputFolder);
            openBtn.text = "打开输出目录";
            openBtn.style.width = 200;
            _playerFoldoutContent.Add(openBtn);
        }

        // ============================================================
        // Status Bar
        // ============================================================

        private void BuildStatusBar(VisualElement root)
        {
            _statusHelp = new HelpBox("", HelpBoxMessageType.Info);
            _statusHelp.style.marginTop = 8;
            _statusHelp.style.marginBottom = 4;
            _statusHelp.style.display = DisplayStyle.None;
            root.Add(_statusHelp);
        }

        private void ShowStatus(string message, HelpBoxMessageType type)
        {
            _statusHelp.text = message;
            _statusHelp.messageType = type;
            _statusHelp.style.display = DisplayStyle.Flex;

            // Auto-hide after 8 seconds
            _statusHelp.schedule.Execute(() =>
            {
                _statusHelp.style.display = DisplayStyle.None;
            }).StartingIn(8000);
        }

        // ============================================================
        // Config & Pipeline Loading
        // ============================================================

        private void LoadConfig()
        {
            _config = AssetDatabase.LoadAssetAtPath<BuildConfig>(ConfigPath);
            if (_config != null)
            {
                _config.outputPath = EditorPrefs.GetString(PrefOutputPath, _config.outputPath);
                _config.abOutputPath = EditorPrefs.GetString(PrefABOutputPath, _config.abOutputPath);
                _config.developmentBuild = EditorPrefs.GetBool(PrefDevBuild, _config.developmentBuild);
                _config.autoconnectProfiler = EditorPrefs.GetBool(PrefProfiler, _config.autoconnectProfiler);
                _config.deepProfiling = EditorPrefs.GetBool(PrefDeepProfiling, _config.deepProfiling);
                _config.buildScriptsOnly = EditorPrefs.GetBool(PrefScriptsOnly, _config.buildScriptsOnly);
                _config.cleanBuild = EditorPrefs.GetBool(PrefCleanBuild, _config.cleanBuild);
                _config.abFullRebuild = EditorPrefs.GetBool(PrefABFullRebuild, _config.abFullRebuild);
                _config.useCustomPipeline = EditorPrefs.GetBool(PrefUseCustomPipeline, _config.useCustomPipeline);
                _config.useCustomABPipeline = EditorPrefs.GetBool(PrefUseCustomABPipeline, _config.useCustomABPipeline);
                return;
            }

            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _config = ScriptableObject.CreateInstance<BuildConfig>();
            AssetDatabase.CreateAsset(_config, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#66ccff>[BuildBox]</color> 已创建BuildConfig 资产: {ConfigPath}");
        }

        private void DiscoverPipelines()
        {
            // Player pipelines
            _playerPipelines.Clear();
            foreach (var type in TypeCache.GetTypesWithAttribute<BuildPipelineAttribute>())
            {
                if (typeof(IBuildPipeline).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var attr = type.GetCustomAttributes(typeof(BuildPipelineAttribute), false)[0] as BuildPipelineAttribute;
                    _playerPipelines.Add((attr?.Name ?? type.Name, type));
                }
            }

            // AB pipelines
            _abPipelines.Clear();
            foreach (var type in TypeCache.GetTypesWithAttribute<ABBuildPipelineAttribute>())
            {
                if (typeof(IABBuildPipeline).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var attr = type.GetCustomAttributes(typeof(ABBuildPipelineAttribute), false)[0] as ABBuildPipelineAttribute;
                    _abPipelines.Add((attr?.Name ?? type.Name, type));
                }
            }
        }

        private void RefreshPlatformBuildNumberEntries()
        {
            // Ensure all platforms have entries
            foreach (var name in BuildProfileManager.PlatformNames)
            {
                if (name == "Android" || name == "iOS") continue;
                if (!_config.platformBuildNumbers.Any(p => p.platformName == name))
                {
                    _config.platformBuildNumbers.Add(new PlatformBuildNumber
                    {
                        platformName = name,
                        buildNumber = 0
                    });
                }
            }
            EditorUtility.SetDirty(_config);
        }

        // ============================================================
        // UI <> Config Sync
        // ============================================================

        private void SyncUIToConfig()
        {
            _config.outputPath = _outputPathField.value;
            EditorPrefs.SetString(PrefOutputPath, _config.outputPath);
            _config.developmentBuild = _toggleDev.value;
            EditorPrefs.SetBool(PrefDevBuild, _config.developmentBuild);
            _config.autoconnectProfiler = _toggleProfiler.value;
            EditorPrefs.SetBool(PrefProfiler, _config.autoconnectProfiler);
            _config.deepProfiling = _toggleDeep.value;
            EditorPrefs.SetBool(PrefDeepProfiling, _config.deepProfiling);
            _config.buildScriptsOnly = _toggleScriptsOnly.value;
            EditorPrefs.SetBool(PrefScriptsOnly, _config.buildScriptsOnly);
            _config.cleanBuild = _toggleClean.value;
            EditorPrefs.SetBool(PrefCleanBuild, _config.cleanBuild);

            _config.abOutputPath = _abOutputPathField.value;
            EditorPrefs.SetString(PrefABOutputPath, _config.abOutputPath);
            _config.abFullRebuild = _toggleABFullRebuild.value;
            EditorPrefs.SetBool(PrefABFullRebuild, _config.abFullRebuild);

            _config.useCustomPipeline = _playerPipelineDropdown.index > 0;
            EditorPrefs.SetBool(PrefUseCustomPipeline, _config.useCustomPipeline);
            if (_config.useCustomPipeline && _playerPipelineDropdown.index > 0)
            {
                var pipeIdx = _playerPipelineDropdown.index - 1;
                if (pipeIdx < _playerPipelines.Count)
                    _config.selectedPipelineTypeName = _playerPipelines[pipeIdx].type.FullName;
            }

            _config.useCustomABPipeline = _abPipelineDropdown.index > 0;
            EditorPrefs.SetBool(PrefUseCustomABPipeline, _config.useCustomABPipeline);
            if (_config.useCustomABPipeline && _abPipelineDropdown.index > 0)
            {
                var pipeIdx = _abPipelineDropdown.index - 1;
                if (pipeIdx < _abPipelines.Count)
                    _config.selectedABPipelineTypeName = _abPipelines[pipeIdx].type.FullName;
            }

            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssetIfDirty(_config);
        }

        // ============================================================
        // Auto-Increment Build Number
        // ============================================================

        private void AutoIncrementBuildNumber()
        {
            if (_selectedProfile == null)
            {
                Debug.LogWarning("[BuildBox] 未选择 Profile，跳过版本号自增");
                return;
            }

            AutoIncrementBuildNumberForPlatform(EditorUserBuildSettings.activeBuildTarget, _config);
        }

        /// <summary> 根据平台自增版本号（供 BuildCLI 复用） </summary>
        public static void AutoIncrementBuildNumberForPlatform(BuildTarget target, BuildConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[BuildBox] BuildConfig 为 null，跳过版本号自增");
                return;
            }

            if (target == BuildTarget.Android)
            {
                PlayerSettings.Android.bundleVersionCode++;
                Debug.Log($"[BuildBox] Android bundleVersionCode -> {PlayerSettings.Android.bundleVersionCode}");
            }
            else if (target == BuildTarget.iOS)
            {
                if (int.TryParse(PlayerSettings.iOS.buildNumber, out var n))
                    PlayerSettings.iOS.buildNumber = (n + 1).ToString();
                else
                    PlayerSettings.iOS.buildNumber = "1";
                Debug.Log($"[BuildBox] iOS buildNumber -> {PlayerSettings.iOS.buildNumber}");
            }
            else
            {
                var platformName = target switch
                {
                    BuildTarget.StandaloneWindows64 => "Windows",
                    BuildTarget.StandaloneWindows  => "Windows",
                    BuildTarget.StandaloneOSX      => "macOS",
                    BuildTarget.StandaloneLinux64  => "Linux",
                    BuildTarget.WebGL              => "WebGL",
                    _ => target.ToString()
                };

                var entry = config.platformBuildNumbers.FirstOrDefault(p => p.platformName == platformName);
                if (entry == null)
                {
                    entry = new PlatformBuildNumber { platformName = platformName, buildNumber = 0 };
                    config.platformBuildNumbers.Add(entry);
                }
                entry.buildNumber++;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
                Debug.Log($"[BuildBox] {platformName} buildNumber -> {entry.buildNumber}");
            }
        }

        // ============================================================
        // AssetBundle Build
        // ============================================================

        private void BuildAssetBundles()
        {
            SyncUIToConfig();

            if (_config.useCustomABPipeline && _abPipelines.Count > 0)
            {
                BuildABCustom();
            }
            else
            {
                BuildABDefault();
            }

            RefreshVersionInfo();
            AssetDatabase.Refresh();
        }

        private void BuildABDefault()
        {
            var path = _config.abOutputPath;
            if (string.IsNullOrEmpty(path))
            {
                ShowStatus("AB 构建失败: 输出路径为空", HelpBoxMessageType.Error);
                return;
            }
            var options = BuildAssetBundleOptions.None;
            if (_config.abFullRebuild)
                options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;

            try
            {
                var manifest = BuildPipeline.BuildAssetBundles(path, options, GetCurrentBuildTarget());
                ShowStatus($"AB 构建完成: {path} （平台：{GetCurrentPlatformName()}）", HelpBoxMessageType.Info);
            }
            catch (Exception e)
            {
                ShowStatus($"AB 构建失败: {e.Message}", HelpBoxMessageType.Error);
                Debug.LogError($"[BuildBox] AB 构建异常: {e}");
            }
        }

        private void BuildABCustom()
        {
            var typeName = _config.selectedABPipelineTypeName;
            var type = _abPipelines.FirstOrDefault(p => p.type.FullName == typeName).type;
            if (type == null && _abPipelines.Count > 0)
                type = _abPipelines[0].type;
            if (type == null)
            {
                ShowStatus("未找到自定义 AB 流水线，使用默认流水线", HelpBoxMessageType.Warning);
                BuildABDefault();
                return;
            }

            try
            {
                var pipeline = Activator.CreateInstance(type) as IABBuildPipeline;
                if (pipeline == null)
                {
                    ShowStatus("自定义 AB 流水线实例化失败", HelpBoxMessageType.Error);
                    return;
                }

                var ctx = new ABBuildPipelineContext
                {
                    outputPath = _config.abOutputPath,
                    buildTarget = GetCurrentBuildTarget(),
                    fullRebuild = _config.abFullRebuild,
                };

                var success = pipeline.Execute(ctx);
                ShowStatus(success
                    ? $"自定义 AB 流水线完成 {_abPipelines.FirstOrDefault(p => p.type == type).name}"
                    : "自定义 AB 流水线失败",
                    success ? HelpBoxMessageType.Info : HelpBoxMessageType.Error);
            }
            catch (Exception e)
            {
                ShowStatus($"自定义 AB 流水线异常 {e.Message}", HelpBoxMessageType.Error);
                Debug.LogError($"[BuildBox] 自定义 AB 流水线异常 {e}");
            }
        }

        private BuildTarget GetCurrentBuildTarget()
        {
            return EditorUserBuildSettings.activeBuildTarget;
        }

        private string GetCurrentPlatformName()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            return target switch
            {
                BuildTarget.StandaloneWindows64 => "Windows",
                BuildTarget.StandaloneWindows  => "Windows",
                BuildTarget.StandaloneOSX      => "macOS",
                BuildTarget.StandaloneLinux64  => "Linux",
                BuildTarget.Android            => "Android",
                BuildTarget.iOS                => "iOS",
                BuildTarget.WebGL              => "WebGL",
                _ => target.ToString()
            };
        }


        // ============================================================
        // Player Build
        // ============================================================

        private void BuildPlayer()
        {
            // Check scenes
            var enabledScenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (enabledScenes.Length == 0)
            {
                ShowStatus("构建失败: Build Settings 中没有已启用的场景", HelpBoxMessageType.Error);
                return;
            }

            SyncUIToConfig();
            AutoIncrementBuildNumber();

            if (_config.useCustomPipeline && _playerPipelines.Count > 0)
            {
                BuildPlayerCustom(enabledScenes);
            }
            else
            {
                BuildPlayerDefault(enabledScenes);
            }

            RefreshVersionInfo();
        }

        private void BuildPlayerDefault(string[] scenes)
        {
            var profile = _selectedProfile;
            if (profile == null)
            {
                ShowStatus("构建失败: 未选择 Profile", HelpBoxMessageType.Error);
                return;
            }

            // 将场景写入 Profile
            profile.scenes = scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();
            profile.overrideGlobalScenes = true;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);

            var outputPath = _outputPathField.value;

            // 确保输出目录存在
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (string.IsNullOrEmpty(outputPath))
            {
                ShowStatus("构建失败: 输出路径为空", HelpBoxMessageType.Error);
                return;
            }

            // 构建选项
            var options = BuildOptions.None;
            if (_config.developmentBuild)    options |= BuildOptions.Development;
            if (_config.autoconnectProfiler) options |= BuildOptions.ConnectWithProfiler;
            if (_config.deepProfiling)       options |= BuildOptions.EnableDeepProfilingSupport;
            if (_config.buildScriptsOnly)    options |= BuildOptions.BuildScriptsOnly;

            // CleanBuild: 在构建前手动删除输出目录（BuildOptions.CleanBuild 在 Unity 6 中已不存在）
            if (_config.cleanBuild)
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                else if (Directory.Exists(outputPath))
                    Directory.Delete(outputPath, true);
            }

            try
            {
                var buildProfileOptions = new BuildPlayerWithProfileOptions
                {
                    buildProfile = profile,
                    locationPathName = outputPath,
                    options = options,
                };
                var report = BuildPipeline.BuildPlayer(buildProfileOptions);
                var summary = report.summary;

                switch (summary.result)
                {
                    case BuildResult.Succeeded:
                        ShowStatus($"构建成功: {outputPath}", HelpBoxMessageType.Info);
                        break;
                    case BuildResult.Failed:
                        ShowStatus($"构建失败: {summary.totalErrors} 个错误", HelpBoxMessageType.Error);
                        break;
                    default:
                        ShowStatus($"构建结果: {summary.result}", HelpBoxMessageType.Warning);
                        break;
                }
            }
            catch (Exception e)
            {
                ShowStatus($"构建异常: {e.Message}", HelpBoxMessageType.Error);
                Debug.LogError($"[BuildBox] 构建异常: {e}");
            }
        }

        private void BuildPlayerCustom(string[] scenes)
        {
            var typeName = _config.selectedPipelineTypeName;
            var type = _playerPipelines.FirstOrDefault(p => p.type.FullName == typeName).type;
            if (type == null && _playerPipelines.Count > 0)
                type = _playerPipelines[0].type;
            if (type == null)
            {
                ShowStatus("未找到自定义流水线，使用默认流水线", HelpBoxMessageType.Warning);
                BuildPlayerDefault(scenes);
                return;
            }

            try
            {
                var pipeline = Activator.CreateInstance(type) as IBuildPipeline;
                if (pipeline == null)
                {
                    ShowStatus("自定义流水线实例化失败", HelpBoxMessageType.Error);
                    return;
                }

                var profile = _selectedProfile;
                var target = EditorUserBuildSettings.activeBuildTarget;
                var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
                var productName = PlayerSettings.productName;

                var ctx = new BuildPipelineContext
                {
                    buildTarget = target,
                    buildTargetGroup = targetGroup,
                    outputPath = _outputPathField.value,
                    productName = productName,
                    developmentBuild = _config.developmentBuild,
                    autoconnectProfiler = _config.autoconnectProfiler,
                    deepProfiling = _config.deepProfiling,
                    buildScriptsOnly = _config.buildScriptsOnly,
                    cleanBuild = _config.cleanBuild,
                    scenes = scenes,
                    profile = profile,
                };

                var success = pipeline.Execute(ctx);
                ShowStatus(success
                    ? $"自定义流水线完成: {_playerPipelines.FirstOrDefault(p => p.type == type).name}"
                    : "自定义流水线失败",
                    success ? HelpBoxMessageType.Info : HelpBoxMessageType.Error);
            }
            catch (Exception e)
            {
                ShowStatus($"自定义流水线异常: {e.Message}", HelpBoxMessageType.Error);
                Debug.LogError($"[BuildBox] 自定义流水线异常: {e}");
            }
        }


        private void CopyCLICommand()
        {
            if (_selectedProfile == null)
            {
                ShowStatus("请先选择一个 Profile", HelpBoxMessageType.Warning);
                return;
            }

            var profileGuid = BuildProfileManager.GetProfileGUID(_selectedProfile);
            if (string.IsNullOrEmpty(profileGuid))
            {
                ShowStatus("无法获取 Profile GUID", HelpBoxMessageType.Error);
                return;
            }

            var sb = new StringBuilder();
            sb.Append("Unity.exe -batchmode -quit ");
            sb.Append("-executeMethod TGame.ToolBox.BuildCLI.Build ");
            sb.Append($"-profile \"{profileGuid}\"");

            // 覆盖输出路径（与默认不同时才添加）
            if (_outputPathField.value != _config.outputPath)
                sb.Append($" -outputPath \"{_outputPathField.value}\"");

            // 追踪 BuildOptions
            var options = new List<string>();
            if (_toggleDev.value)       options.Add("Development");
            if (_toggleProfiler.value)  options.Add("Profiler");
            if (_toggleDeep.value)      options.Add("DeepProfiling");
            if (_toggleScriptsOnly.value) options.Add("ScriptsOnly");
            if (_toggleClean.value)     options.Add("CleanBuild");
            if (options.Count > 0)
                sb.Append($" -buildOptions \"{string.Join(",", options)}\"");

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            ShowStatus("CLI 指令已复制到剪贴板", HelpBoxMessageType.Info);
            Debug.Log($"[BuildBox] CLI 命令:\n{sb}");
        }


        // ============================================================
        // Path utility
        // ============================================================

        private static string MakeRelativePath(string absolutePath)
        {
            var projectPath = System.IO.Path.GetDirectoryName(Application.dataPath);
            if (!string.IsNullOrEmpty(projectPath) && absolutePath.StartsWith(projectPath, System.StringComparison.OrdinalIgnoreCase))
            {
                if (absolutePath.Length == projectPath.Length)
                    return "";
                // +1 for the directory separator
                return absolutePath.Substring(projectPath.Length + 1);
            }
            return absolutePath;
        }

        private void OpenOutputFolder()
        {
            var outputPath = _outputPathField.value;
            if (!string.IsNullOrEmpty(outputPath))
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    EditorUtility.RevealInFinder(dir);
                    return;
                }
            }

            // Fallback: 打开基础输出目录
            if (Directory.Exists(_config.outputPath))
            {
                EditorUtility.RevealInFinder(_config.outputPath);
            }
            else
            {
                ShowStatus("输出目录不存在，请先构建", HelpBoxMessageType.Warning);
            }
        }
    }
}








