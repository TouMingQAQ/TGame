using System.Linq;
using TGame.ToolBox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TGame.SceneNavigator
{
    [ToolBox("场景导航器", Order = 0)]
    public class SceneNavigatorBox : IToolBoxContent
    {
        private SceneNavigatorProfile _profile;
        private Vector2 _scrollPos;

        private string _searchText = "";
        private SceneEntry[] _filtered = { };
        private int _selectedIndex;

        private static string _prePlayScenePath;

        private const string ResFileName = "SceneNavigatorProfile";
        private const string ResFolder = "Assets/Plugins/TGame/SceneNavigator/Resources";

        public void DrawContent()
        {
            EnsureProfile();
            if (_profile == null)
            {
                EditorGUILayout.HelpBox("无法加载场景导航配置文件。", MessageType.Error);
                if (GUILayout.Button("创建配置文件"))
                    EnsureProfileExists();
                return;
            }

            DrawSearchAndSelect();
            EditorGUILayout.Space();
            DrawRunButton();
        }

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

        private void DrawSearchAndSelect()
        {
            BuildFilteredList();

            EditorGUILayout.BeginHorizontal();

            // Search field — flexible width
            GUILayout.Label("搜索", GUILayout.Width(30));
            _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField);

            // Separator
            GUILayout.Space(4);

            // Dropdown button — fixed width
            if (_filtered.Length > 0 && _selectedIndex >= 0)
            {
                var entry = _filtered[_selectedIndex];
                var displayName = !string.IsNullOrEmpty(entry.alias)
                    ? entry.alias
                    : System.IO.Path.GetFileNameWithoutExtension(entry.scenePath);

                var dropRect = EditorGUILayout.GetControlRect(GUILayout.Width(160));
                if (EditorGUI.DropdownButton(dropRect, new GUIContent(displayName), FocusType.Keyboard))
                {
                    var menu = new GenericMenu();
                    for (var i = 0; i < _filtered.Length; i++)
                    {
                        var e = _filtered[i];
                        var name = !string.IsNullOrEmpty(e.alias)
                            ? e.alias
                            : System.IO.Path.GetFileNameWithoutExtension(e.scenePath);
                        var idx = i;
                        menu.AddItem(new GUIContent(name), i == _selectedIndex, () => _selectedIndex = idx);
                    }
                    menu.DropDown(dropRect);
                }
            }
            else
            {
                GUILayout.Label(
                    string.IsNullOrWhiteSpace(_searchText) ? "暂无场景" : "无匹配",
                    GUILayout.Width(160));
            }

            EditorGUILayout.EndHorizontal();
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

        private void DrawRunButton()
        {
            var canRun = _filtered.Length > 0 && _selectedIndex >= 0;

            if (!canRun)
            {
                EditorGUILayout.HelpBox("请先在配置文件中添加场景条目。", MessageType.Info);
            }

            EditorGUI.BeginDisabledGroup(!canRun);
            var btnRect = EditorGUILayout.GetControlRect(GUILayout.Height(36));
            if (GUI.Button(btnRect, "▶ 运行此场景"))
            {
                RunSelectedScene();
            }
            EditorGUI.EndDisabledGroup();

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("游戏运行中，停止后将回到运行前的场景。", MessageType.Info);
                var stopRect = EditorGUILayout.GetControlRect(GUILayout.Height(30));
                if (GUI.Button(stopRect, "■ 停止运行"))
                {
                    EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
                }
            }
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
