using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// 浮窗翻转方向(对应屏幕的"浮窗在鼠标哪一侧")。
    /// 默认 BottomRight:浮窗在鼠标右侧+下方。越界时按 preferred → BR → BL → TR → TL 优先级尝试。
    /// </summary>
    public enum PopupFlipDirection
    {
        BottomRight,
        BottomLeft,
        TopRight,
        TopLeft,
    }

    /// <summary>
    /// 浮窗布局静态算法。
    /// 输入: 目标点/目标 RectTransform + 浮窗尺寸 + Popup Root RT + 边界 RT(可空 = Popup Root) + 偏好方向 + 偏移。
    /// 输出: (anchoredPosition, pivot) — 写到 _content RectTransform 上。
    /// 翻转语义: 4 个候选方向,按 preferred 优先,选第一个"浮窗 4 边都在 areaRect 内"的。
    /// </summary>
    internal static class PopupLayoutHelper
    {
        public static (Vector2 anchoredPosition, Vector2 pivot) Solve(
            Vector2 screenAnchor,
            Vector2 contentSize,
            RectTransform popupRoot,
            RectTransform boundsArea,
            PopupFlipDirection preferred,
            Vector2 offset)
        {
            Vector2 anchorPoint = ScreenToRootPoint(popupRoot, screenAnchor);
            Rect targetRect = new Rect(anchorPoint, Vector2.zero);
            return SolveTargetRect(targetRect, contentSize, popupRoot, boundsArea, preferred, offset);
        }

        public static (Vector2 anchoredPosition, Vector2 pivot) Solve(
            RectTransform target,
            Vector2 contentSize,
            RectTransform popupRoot,
            RectTransform boundsArea,
            PopupFlipDirection preferred,
            Vector2 offset)
        {
            Rect targetRect = GetRectInRoot(popupRoot, target);
            return SolveTargetRect(targetRect, contentSize, popupRoot, boundsArea, preferred, offset);
        }

        private static (Vector2 anchoredPosition, Vector2 pivot) SolveTargetRect(
            Rect targetRect,
            Vector2 contentSize,
            RectTransform popupRoot,
            RectTransform boundsArea,
            PopupFlipDirection preferred,
            Vector2 offset)
        {
            Rect areaRect = GetAreaRect(popupRoot, boundsArea);

            // 4 个候选: pivot 是浮窗贴近鼠标的角。
            var candidates = new (Vector2 pivot, PopupFlipDirection dir)[]
            {
                (new Vector2(0f, 1f), PopupFlipDirection.BottomRight), // 浮窗在鼠标右下
                (new Vector2(1f, 1f), PopupFlipDirection.BottomLeft),  // 浮窗在鼠标左下
                (new Vector2(0f, 0f), PopupFlipDirection.TopRight),    // 浮窗在鼠标右上
                (new Vector2(1f, 0f), PopupFlipDirection.TopLeft),     // 浮窗在鼠标左上
            };
            SortCandidatesByPreferred(candidates, preferred);

            int bestIndex = 0;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < candidates.Length; i++)
            {
                var c = candidates[i];
                Vector2 pivotPos = GetPivotPosition(targetRect, c.dir, offset);
                Vector2 bl = pivotPos - c.pivot * contentSize;
                Vector2 tr = bl + contentSize;

                bool inside = bl.x >= areaRect.xMin && tr.x <= areaRect.xMax
                           && bl.y >= areaRect.yMin && tr.y <= areaRect.yMax;
                if (inside)
                {
                    return (pivotPos, c.pivot);
                }

                float score = ComputeOverlap(bl, tr, areaRect);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            var best = candidates[bestIndex];
            Vector2 bestPivotPos = GetPivotPosition(targetRect, best.dir, offset);
            return (ClampPivotPosition(bestPivotPos, best.pivot, contentSize, areaRect), best.pivot);
        }

        private static Rect GetRectInRoot(RectTransform popupRoot, RectTransform target)
        {
            if (target == null)
                return new Rect(Vector2.zero, Vector2.zero);

            var worldCorners = new Vector3[4];
            target.GetWorldCorners(worldCorners);

            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            var targetCamera = GetCanvasCamera(target);
            for (int i = 0; i < worldCorners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCamera, worldCorners[i]);
                Vector2 point = ScreenToRootPoint(popupRoot, screen);
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Rect GetAreaRect(RectTransform popupRoot, RectTransform boundsArea)
        {
            if (popupRoot == null)
            {
                return new Rect(0f, 0f, Screen.width, Screen.height);
            }

            if (boundsArea == null)
            {
                return new Rect(0f, 0f, popupRoot.rect.width, popupRoot.rect.height);
            }

            var worldCorners = new Vector3[4];
            boundsArea.GetWorldCorners(worldCorners);

            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            var boundsCamera = GetCanvasCamera(boundsArea);
            for (int i = 0; i < worldCorners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(boundsCamera, worldCorners[i]);
                Vector2 point = ScreenToRootPoint(popupRoot, screen);
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Vector2 ScreenToRootPoint(RectTransform popupRoot, Vector2 screenPoint)
        {
            if (popupRoot == null)
                return screenPoint;

            Camera camera = GetCanvasCamera(popupRoot);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(popupRoot, screenPoint, camera, out var local))
            {
                return local - popupRoot.rect.min;
            }

            return screenPoint;
        }

        private static Camera GetCanvasCamera(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return null;

            var canvas = rectTransform.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                return canvas.worldCamera;

            return null;
        }

        private static void SortCandidatesByPreferred(
            (Vector2 pivot, PopupFlipDirection dir)[] candidates, PopupFlipDirection preferred)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].dir == preferred)
                {
                    if (i != 0)
                    {
                        var tmp = candidates[0];
                        candidates[0] = candidates[i];
                        candidates[i] = tmp;
                    }
                    return;
                }
            }
        }

        /// <summary>根据方向选目标矩形对应边角,再叠加用户定义的目标偏移。</summary>
        private static Vector2 GetPivotPosition(Rect targetRect, PopupFlipDirection dir, Vector2 offset)
        {
            return dir switch
            {
                PopupFlipDirection.BottomRight => new Vector2(targetRect.xMax + offset.x, targetRect.yMin - offset.y),
                PopupFlipDirection.BottomLeft => new Vector2(targetRect.xMin - offset.x, targetRect.yMin - offset.y),
                PopupFlipDirection.TopRight => new Vector2(targetRect.xMax + offset.x, targetRect.yMax + offset.y),
                PopupFlipDirection.TopLeft => new Vector2(targetRect.xMin - offset.x, targetRect.yMax + offset.y),
                _ => Vector2.zero,
            };
        }

        private static float ComputeOverlap(Vector2 bl, Vector2 tr, Rect areaRect)
        {
            float w = Mathf.Max(0f, Mathf.Min(tr.x, areaRect.xMax) - Mathf.Max(bl.x, areaRect.xMin));
            float h = Mathf.Max(0f, Mathf.Min(tr.y, areaRect.yMax) - Mathf.Max(bl.y, areaRect.yMin));
            return w * h;
        }

        private static Vector2 ClampPivotPosition(Vector2 pivotPos, Vector2 pivot, Vector2 size, Rect areaRect)
        {
            Vector2 bl = pivotPos - pivot * size;
            if (size.x <= areaRect.width)
            {
                bl.x = Mathf.Clamp(bl.x, areaRect.xMin, areaRect.xMax - size.x);
            }
            else
            {
                bl.x = areaRect.xMin;
            }

            if (size.y <= areaRect.height)
            {
                bl.y = Mathf.Clamp(bl.y, areaRect.yMin, areaRect.yMax - size.y);
            }
            else
            {
                bl.y = areaRect.yMin;
            }

            return bl + pivot * size;
        }
    }
}
