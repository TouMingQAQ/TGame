using UnityEngine;
using UnityEngine.SceneManagement;

namespace TGame.TCore.Runtime
{
    /// <summary>
    /// 初始场景引导器：完成 TGame 初始化后加载目标工作场景。
    /// 挂载到初始场景（如 Start.unity）中的任意 GameObject 上。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrapper : MonoBehaviour
    {
        private const string TargetSceneKey = "TGame_InitBoot_TargetScene";

        private void Awake()
        {
            if (Game.Instance == null)
            {
                Debug.LogError("[GameBootstrapper] 场景中缺少 Game 组件！请确保场景中存在 Game 单例。");
                return;
            }

            // 将初始场景中的 Manager 标记为跨场景持久化
            var managers = GetComponentsInChildren<BaseManager>(true);
            foreach (var manager in managers)
            {
                if (manager != null && manager.gameObject != null)
                    DontDestroyOnLoad(manager.gameObject);
            }
        }

        private IEnumerator Start()
        {
            // 等待一帧，确保所有 Manager 的 Awake/Start 已完成
            yield return null;

            string targetScene = PlayerPrefs.GetString(TargetSceneKey, "");
            if (!string.IsNullOrEmpty(targetScene))
            {
                PlayerPrefs.DeleteKey(TargetSceneKey);
                PlayerPrefs.Save();

                SceneManager.LoadScene(targetScene);
            }
            else
            {
                Debug.LogWarning("[GameBootstrapper] 未找到目标场景路径（PlayerPrefs），跳过场景跳转。");
            }
        }
    }
}
