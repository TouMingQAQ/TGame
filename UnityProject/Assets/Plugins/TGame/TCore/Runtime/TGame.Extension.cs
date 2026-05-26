using UnityEngine;

namespace TGame.TCore.Runtime
{
    public static class TransformExtension
    {
        public static void ClearChild(this Transform transform)
        {
            for (int i = 0,count = transform.childCount; i < count; i++)
            {
                var child = transform.GetChild(0);
                Object.Destroy(child.gameObject);
            }
        }
        public static void ClearChildImmediate(this Transform transform)
        {
            for (int i = 0,count = transform.childCount; i < count; i++)
            {
                var child = transform.GetChild(0);
                Object.DestroyImmediate(child.gameObject);
            }
        }
        /// <summary>
        /// 交换两者的下标
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="target"></param>
        public static void ExChangeSibling(this Transform transform, Transform target)
        {
            if(target == null) return;
            if(target.parent != transform.parent) return;
            var siblingIndex = transform.GetSiblingIndex();
            transform.SetSiblingIndex(target.GetSiblingIndex());
            target.SetSiblingIndex(siblingIndex);
        }
    }
}