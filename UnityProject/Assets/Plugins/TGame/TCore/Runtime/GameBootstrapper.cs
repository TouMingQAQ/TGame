
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
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrapper : MonoBehaviour
    {
#if UNITY_EDITOR

        public const string TargetSceneKey = "TGame_InitBoot_TargetScene";

        private static bool _hasBootstrapped;

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
                return;
            }
        }

        private IEnumerator Start()
        {
            if (_hasBootstrapped) yield break;

            // 等待一帧，确保所有 Manager 的 Awake/Start 已完成
            yield return null;

            string targetScene = EditorPrefs.GetString(TargetSceneKey, "");
            if (!string.IsNullOrEmpty(targetScene))
            {
                EditorPrefs.DeleteKey(TargetSceneKey);

                // 加载前标记，防止目标场景中的 GameBootstrapper 再次触发
                _hasBootstrapped = true;
                SceneManager.LoadScene(targetScene);
            }
            else
            {
                Debug.LogWarning("[GameBootstrapper] 未找到目标场景路径（PlayerPrefs），跳过场景跳转。");
            }
        }
#endif
    }
}
