using System.Linq;
using TGame.ToolBox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TGame.SceneNavigator
{
    [ToolBox("快捷启动", Order = 0)]
    public class SceneNavigatorBox : IToolBoxContentVisualElement
    {
        public static BoxRegistration Registration => new()
        {
            Name = "快捷启动",
            Group = "程序",
            Icon = "SceneAsset Icon",
            Factory = () => new SceneNavigatorBox().CreateContent()
        };
        private SceneNavigatorProfile _profile;
        private string _searchText = "";
        private SceneEntry[] _filtered = { };
        private int _selectedIndex;

        private static string _prePlayScenePath;

        private const string ResFileName = "SceneNavigatorProfile";
        private const string ResFolder = "Assets/Plugins/TGame/SceneNavigator/Resources";

        // Visual elements
        private VisualElement _root;
        private TextField _searchField;
        private Button _dropdownButton;
        private Button _runButton;
        private HelpBox _noSceneHelp;
        private VisualElement _helpBoxContainer;

        // ── 初始化启动 ──
        private const string InitScenePrefKey = "SceneNavigator.InitScenePath";
        private SceneAsset _initSceneAsset;
        private ObjectField _initSceneField;
        private Button _initBootButton;
        private HelpBox _initBootHelpBox;
        private bool _initSceneValid;

        public VisualElement CreateContent()
        {
            _root = new VisualElement();
            _root.style.flexGrow = 1;
            _root.style.paddingLeft = 6;
            _root.style.paddingRight = 6;
            _root.style.paddingTop = 4;

            _root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
            });

            EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;

            RebuildContent();
            return _root;
        }

        private void RebuildContent()
        {
            _root.Clear();
            _searchField = null;
            _dropdownButton = null;
            _runButton = null;
            _noSceneHelp = null;
            _helpBoxContainer = null;
            _initSceneField = null;
            _initBootButton = null;
            _initBootHelpBox = null;
            _initSceneValid = false;

            EnsureProfile();
            if (_profile == null)
            {
                _root.Add(new HelpBox("无法加载场景导航配置文件。", HelpBoxMessageType.Error));
                var createBtn = new Button(() =>
                {
                    EnsureProfileExists();
                    EnsureProfile();
                    RebuildContent();
                });
                createBtn.text = "创建配置文件";
                _root.Add(createBtn);
                return;
            }

            BuildSearchAndSelect();
            _root.Add(new VisualElement { style = { height = 4 } });
            BuildRunButton();

            _root.Add(BuildSeparator());
            BuildInitBootSection();
        }

        private void BuildSearchAndSelect()
        {
            BuildFilteredList();

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            _root.Add(row);

            var searchLabel = new Label("搜索");
            searchLabel.style.width = 30;
            row.Add(searchLabel);

            _searchField = new TextField();
            _searchField.value = _searchText;
            _searchField.style.flexGrow = 1;
            _searchField.RegisterValueChangedCallback(OnSearchChanged);
            row.Add(_searchField);

            row.Add(new VisualElement { style = { width = 4 } });

            _dropdownButton = new Button(OnDropdownClicked);
            _dropdownButton.style.width = 160;
            _dropdownButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(_dropdownButton);

            UpdateDropdownState();
        }

        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            _searchText = evt.newValue;
            BuildFilteredList();
            UpdateDropdownState();
            UpdateRunButtonState();
        }

        private void OnDropdownClicked()
        {
            if (_filtered.Length == 0) return;

            var menu = new GenericMenu();
            for (int i = 0; i < _filtered.Length; i++)
            {
                var idx = i;
                var name = GetEntryDisplayName(_filtered[i]);
                menu.AddItem(new GUIContent(name), i == _selectedIndex, () =>
                {
                    _selectedIndex = idx;
                    _dropdownButton.text = name;
                    UpdateRunButtonState();
                });
            }
            menu.DropDown(_dropdownButton.worldBound);
        }

        private void UpdateDropdownState()
        {
            if (_filtered.Length > 0)
            {
                if (_selectedIndex < 0 || _selectedIndex >= _filtered.Length)
                    _selectedIndex = 0;
                _dropdownButton.text = GetEntryDisplayName(_filtered[_selectedIndex]);
            }
            else
            {
                _dropdownButton.text = string.IsNullOrWhiteSpace(_searchText) ? "暂无场景" : "无匹配";
                _selectedIndex = -1;
            }
        }

        private void BuildRunButton()
        {
            _noSceneHelp = new HelpBox("请先在配置文件中添加场景条目。", HelpBoxMessageType.Info);
            _noSceneHelp.name = "no-scene-help";
            _root.Add(_noSceneHelp);

            _runButton = new Button(RunSelectedScene);
            _runButton.text = "▶ 运行此场景";
            _runButton.style.height = 36;
            _root.Add(_runButton);

            _helpBoxContainer = new VisualElement();
            _root.Add(_helpBoxContainer);

            UpdateRunButtonState();
            UpdatePlayModeHelp();
        }

        private void UpdateRunButtonState()
        {
            var canRun = _filtered.Length > 0 && _selectedIndex >= 0;
            _runButton?.SetEnabled(canRun);
            if (_noSceneHelp != null)
                _noSceneHelp.style.display = canRun ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void UpdatePlayModeHelp()
        {
            if (_helpBoxContainer == null) return;
            _helpBoxContainer.Clear();

            if (EditorApplication.isPlaying)
            {
                _helpBoxContainer.Add(new HelpBox("游戏运行中，停止后将回到运行前的场景。", HelpBoxMessageType.Info));
                var stopBtn = new Button(() => EditorApplication.delayCall += () => EditorApplication.isPlaying = false);
                stopBtn.text = "■ 停止运行";
                stopBtn.style.height = 30;
                _helpBoxContainer.Add(stopBtn);
            }
        }

        private void OnEditorPlayModeStateChanged(PlayModeStateChange state)
        {
            UpdatePlayModeHelp();
            if (state == PlayModeStateChange.EnteredEditMode)
                UpdateRunButtonState();
        }

        private static string GetEntryDisplayName(SceneEntry entry)
        {
            return !string.IsNullOrEmpty(entry.alias)
                ? entry.alias
                : System.IO.Path.GetFileNameWithoutExtension(entry.scenePath);
        }

        // ──────────────────────────────────────────
        //  初始化启动
        // ──────────────────────────────────────────

        private void BuildInitBootSection()
        {
            var sectionTitle = new Label("初始化启动");
            sectionTitle.AddToClassList("tbx-section-title");
            _root.Add(sectionTitle);

            // 加载已保存的初始场景路径
            var savedPath = EditorPrefs.GetString(InitScenePrefKey, "");
            if (!string.IsNullOrEmpty(savedPath))
                _initSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(savedPath);

            // 场景选择行
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var label = new Label("初始场景");
            label.style.width = 60;
            row.Add(label);

            _initSceneField = new ObjectField();
            _initSceneField.objectType = typeof(SceneAsset);
            _initSceneField.value = _initSceneAsset;
            _initSceneField.style.flexGrow = 1;
            _initSceneField.RegisterValueChangedCallback(OnInitSceneChanged);
            row.Add(_initSceneField);

            _root.Add(row);
            _root.Add(new VisualElement { style = { height = 4 } });

            // 按钮
            _initBootButton = new Button(RunInitScene);
            _initBootButton.text = "▶ 初始化并启动";
            _initBootButton.style.height = 36;
            _root.Add(_initBootButton);

            // 提示
            _initBootHelpBox = new HelpBox("请选择初始场景。", HelpBoxMessageType.Info);
            _root.Add(_initBootHelpBox);

            ValidateInitScene();
        }

        private void OnInitSceneChanged(ChangeEvent<Object> evt)
        {
            _initSceneAsset = evt.newValue as SceneAsset;
            if (_initSceneAsset != null)
            {
                var path = AssetDatabase.GetAssetPath(_initSceneAsset);
                EditorPrefs.SetString(InitScenePrefKey, path);
            }
            else
            {
                EditorPrefs.DeleteKey(InitScenePrefKey);
            }

            ValidateInitScene();
        }

        private void RunInitScene()
        {
            if (_initSceneAsset == null)
            {
                Debug.LogWarning("[SceneNavigator] 请先选择初始场景。");
                return;
            }

            var initScenePath = AssetDatabase.GetAssetPath(_initSceneAsset);
            if (string.IsNullOrEmpty(initScenePath))
            {
                Debug.LogError("[SceneNavigator] 初始场景路径无效。");
                return;
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(initScenePath);
            if (sceneAsset == null)
            {
                Debug.LogError($"[SceneNavigator] 场景文件不存在: {initScenePath}");
                return;
            }

            // 保存当前场景路径，用于退出播放后恢复
            _prePlayScenePath = SceneManager.GetActiveScene().path;

            // 通过 PlayerPrefs 将目标场景路径传递给运行时 GameBootstrapper
            PlayerPrefs.SetString("TGame_InitBoot_TargetScene", _prePlayScenePath);
            PlayerPrefs.Save();

            EditorSceneManager.playModeStartScene = sceneAsset;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        private static VisualElement BuildSeparator()
        {
            var sep = new VisualElement();
            sep.AddToClassList("tbx-separator");
            sep.style.marginTop = 8;
            sep.style.marginBottom = 8;
            return sep;
        }

        private void ValidateInitScene()
        {
            _initSceneValid = false;

            if (_initSceneAsset == null)
            {
                _initBootButton?.SetEnabled(false);
                if (_initBootHelpBox != null)
                {
                    _initBootHelpBox.messageType = HelpBoxMessageType.Info;
                    _initBootHelpBox.text = "请选择初始场景。";
                }
                return;
            }

            var path = AssetDatabase.GetAssetPath(_initSceneAsset);
            var hasBootstrapper = SceneHasBootstrapper(path);
            _initSceneValid = hasBootstrapper;

            _initBootButton?.SetEnabled(hasBootstrapper);

            if (_initBootHelpBox == null) return;

            if (hasBootstrapper)
            {
                _initBootHelpBox.messageType = HelpBoxMessageType.Info;
                _initBootHelpBox.text = "已将当前场景路径写入 PlayerPrefs，初始化完成后自动跳回。";
            }
            else
            {
                _initBootHelpBox.messageType = HelpBoxMessageType.Warning;
                _initBootHelpBox.text = "场景中未检测到 GameBootstrapper 组件，将不会执行初始化跳转。";
            }
        }

        private static bool SceneHasBootstrapper(string scenePath)
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(scenePath);
            foreach (var asset in allAssets)
            {
                if (asset is TCore.Runtime.GameBootstrapper)
                    return true;

                if (asset is GameObject go && go.GetComponent<TCore.Runtime.GameBootstrapper>() != null)
                    return true;
            }
            return false;
        }

        // --- unchanged business logic ---

        private void EnsureProfile()
        {
            if (_profile != null)
                return;
            _profile = Resources.Load<SceneNavigatorProfile>(ResFileName);
            if (_profile == null)
            {
                EnsureProfileExists();
                _profile = Resources.Load<SceneNavigatorProfile>(ResFileName);
            }
        }

        private void BuildFilteredList()
        {
            if (_profile == null || _profile.scenes.Count == 0)
            {
                _filtered = System.Array.Empty<SceneEntry>();
                _selectedIndex = -1;
                return;
            }

            if (string.IsNullOrWhiteSpace(_searchText))
            {
                _filtered = _profile.scenes.ToArray();
            }
            else
            {
                var kw = _searchText.ToLower();
                _filtered = _profile.scenes
                    .Where(s => (!string.IsNullOrEmpty(s.alias) && s.alias.ToLower().Contains(kw))
                             || (!string.IsNullOrEmpty(s.scenePath) && s.scenePath.ToLower().Contains(kw)))
                    .ToArray();
                if (_filtered.Length == 0)
                {
                    _selectedIndex = -1;
                    return;
                }
            }

            if (_selectedIndex >= _filtered.Length)
                _selectedIndex = _filtered.Length - 1;
            if (_selectedIndex < 0 && _filtered.Length > 0)
                _selectedIndex = 0;
        }

        private void RunSelectedScene()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _filtered.Length)
                return;

            var entry = _filtered[_selectedIndex];
            if (string.IsNullOrEmpty(entry.scenePath))
                return;

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.scenePath);
            if (sceneAsset == null)
            {
                Debug.LogError($"[SceneNavigator] 场景文件不存在: {entry.scenePath}");
                return;
            }

            _prePlayScenePath = SceneManager.GetActiveScene().path;

            EditorSceneManager.playModeStartScene = sceneAsset;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

                // 清理初始化引导 PlayerPrefs 标记（如有残留）
                PlayerPrefs.DeleteKey("TGame_InitBoot_TargetScene");
                PlayerPrefs.Save();

                var savedPath = _prePlayScenePath;
                _prePlayScenePath = null;
                EditorSceneManager.playModeStartScene = null;

                if (!string.IsNullOrEmpty(savedPath))
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (SceneManager.GetActiveScene().path != savedPath)
                            EditorSceneManager.OpenScene(savedPath);
                    };
                }
            }
        }

        private static void EnsureProfileExists()
        {
            var fullPath = $"{ResFolder}/{ResFileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<SceneNavigatorProfile>(fullPath) != null)
                return;

            if (!AssetDatabase.IsValidFolder(ResFolder))
            {
                var parent = "Assets/Plugins/TGame/SceneNavigator";
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder("Assets/Plugins/TGame", "SceneNavigator");
                AssetDatabase.CreateFolder(parent, "Resources");
            }

            var profile = ScriptableObject.CreateInstance<SceneNavigatorProfile>();
            AssetDatabase.CreateAsset(profile, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
