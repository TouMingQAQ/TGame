using UnityEngine;

namespace TGame.SeeThrough
{
    /// <summary>
    /// 挂载在需要产生透视效果的各个目标物体（如玩家角色、队友、NPC等）上。
    /// 自动注册到 OcclusionManager，支持为每个目标独立配置透视镂空半径、羽化程度与边缘发光颜色。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class OcclusionTarget : MonoBehaviour
    {
        [Header("透视参数配置")]
        [Tooltip("该目标的透视镂空半径")]
        [Range(0.1f, 10f)]
        public float cutoutRadius = 1.6f;

        [Tooltip("边缘羽化柔和度")]
        [Range(0.01f, 2f)]
        public float softness = 0.4f;

        [Tooltip("镂空中心最小 Alpha (0 为完全透光，0.2 为半透明幽灵效果)")]
        [Range(0f, 1f)]
        public float minAlpha = 0f;

        [Tooltip("该目标透视边缘发光颜色")]
        public Color edgeColor = new Color(0.2f, 0.85f, 1f, 1f);

        [Tooltip("边缘发光宽度")]
        [Range(0.01f, 1f)]
        public float edgeWidth = 0.15f;

        [Header("运动演示")]
        [Tooltip("是否自动移动演示")]
        public bool autoMove = true;

        [Tooltip("移动速度")]
        public float moveSpeed = 1.5f;

        [Tooltip("移动幅度")]
        public float moveDistance = 3.5f;

        [Tooltip("移动轴向 (X/Z)")]
        public Vector3 moveAxis = new Vector3(0f, 0f, 1f);

        [Tooltip("运动时间相位偏移")]
        public float timeOffset = 0f;

        [Tooltip("基准中心坐标")]
        public Vector3 basePosition;

        private void Awake()
        {
            if (basePosition == Vector3.zero && transform.position != Vector3.zero)
            {
                basePosition = transform.position;
            }
        }

        private void OnEnable()
        {
            if (basePosition == Vector3.zero)
            {
                basePosition = transform.position;
            }
            OcclusionManager.Register(this);
        }

        private void OnDisable()
        {
            OcclusionManager.Unregister(this);
        }

        private void Update()
        {
            if (Application.isPlaying && autoMove)
            {
                float offset = Mathf.Sin((Time.time + timeOffset) * moveSpeed) * moveDistance;
                transform.position = basePosition + moveAxis.normalized * offset;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0.6f);
            Gizmos.DrawWireSphere(transform.position, cutoutRadius);
        }
    }
}
