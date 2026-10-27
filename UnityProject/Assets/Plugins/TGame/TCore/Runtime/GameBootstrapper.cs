
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace TGame.TCore.Runtime
{
    /// <summary>
    /// 初始场景引导器：完成 TGame 初始化后加载目标工作场景。
    /// 挂载到初始场景（如 Start.unity）中的任意 GameObject 上。
    ///
    /// 场景来源优先级：
    ///   1. 运行时通过 <see cref="SetTargetScene"/> 指定（联机/回放等外部流程调用）
    ///   2. 序列化字段 <see cref="_targetScene"/>（打包后生效）
    ///   3. Editor 下从 EditorPrefs 读取（ToolBox 启动场景选择器写入）
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrapper : MonoBehaviour
    {
        /// <summary>
        /// Editor 下用于保存“启动时跳转的目标场景”的 EditorPrefs key。
        /// </summary>
        public const string TargetSceneKey = "TGame_InitBoot_TargetScene";

        [Tooltip("启动后要加载的目标场景名（相对 Build Settings 的场景名或 Assets 相对路径）。留空则停留在当前场景。")]
        [SerializeField]
        private string _targetScene = "";

        [Tooltip("勾选后由 GameBootstrapper 自己完成 Game 初始化前的等待，并在目标场景加载后销毁自身。")]
        [SerializeField]
        private bool _destroyOnBootstrapped = false;

        /// <summary>
        /// 运行时指定的目标场景，优先级最高。
        /// </summary>
        private static string _runtimeTargetScene;

        /// <summary>
        /// 防止目标场景中挂载了同样的脚本导致循环加载。
        /// </summary>
        private static bool _hasBootstrapped;

        /// <summary>
        /// 目标场景加载完成后触发，参数为实际加载的场景名。
        /// </summary>
        public static event System.Action<string> Bootstrapped;

        /// <summary>
        /// 运行时指定启动目标场景（在引导器运行前调用）。
        /// </summary>
        public static void SetTargetScene(string sceneName)
        {
            _runtimeTargetScene = sceneName;
        }

        /// <summary>
        /// 重置引导状态，便于回主菜单/重新开局时再次引导。
        /// </summary>
        public static void ResetBootstrapState()
        {
            _hasBootstrapped = false;
            _runtimeTargetScene = null;
        }

        private void Awake()
        {
            if (_hasBootstrapped)
            {
                // 防止目标场景中挂载了同样的脚本导致循环加载
                Destroy(this);
                return;
            }

            if (Game.Instance == null)
            {
                Debug.LogError("[GameBootstrapper] 场景中缺少 Game 组件！请确保场景中存在 Game 单例。");
            }
        }

        private IEnumerator Start()
        {
            if (_hasBootstrapped)
                yield break;

            // 等待一帧，确保所有 Manager 的 Awake/Start 已完成（Game 的 AddManager 在 Start 里）
            yield return null;

            string targetScene = ResolveTargetScene();
            if (string.IsNullOrEmpty(targetScene))
            {
                Debug.LogWarning("[GameBootstrapper] 未找到目标场景路径，跳过场景跳转。");
                yield break;
            }

            _hasBootstrapped = true;
            SceneManager.LoadScene(targetScene);

            // 等待加载完成后再广播，调用方可据此做后续初始化
            yield return null;

            Bootstrapped?.Invoke(targetScene);

            if (_destroyOnBootstrapped)
                Destroy(this);
        }

        /// <summary>
        /// 按优先级解析目标场景。
        /// </summary>
        private string ResolveTargetScene()
        {
            if (!string.IsNullOrEmpty(_runtimeTargetScene))
                return _runtimeTargetScene;

            if (!string.IsNullOrEmpty(_targetScene))
                return _targetScene;

#if UNITY_EDITOR
            string editorScene = EditorPrefs.GetString(TargetSceneKey, "");
            if (!string.IsNullOrEmpty(editorScene))
            {
                // 读取一次即清除，避免下次进入 Play 模式又跳同一个场景
                EditorPrefs.DeleteKey(TargetSceneKey);
                return editorScene;
            }
#endif
            return null;
        }
    }
}
