using System.Collections.Generic;
using DG.Tweening;
using TGame.Gamepad;
using UnityEngine;

namespace TGame.Sample
{
    /// <summary>
    /// 四角方框游标（示例）。四角用 DOTween 缓慢向四个角外扩再回缩，循环表现。
    /// 实现 IGamepadCursor：管理器只调用 Show/Hide/Move(target)，移动与表现由本组件负责。
    /// </summary>
    public sealed class GamepadCornerCursor : MonoBehaviour, IGamepadCursor
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform[] corners = new RectTransform[4];
        [SerializeField] private float followSpeed = 20f;
        [SerializeField] private float pulseDistance = 10f;
        [SerializeField] private float pulseDuration = 1.1f;

        private RectTransform self;
        private RectTransform target;
        private readonly List<Tween> tweens = new List<Tween>();
        private Vector2[] basePositions;

        private void Awake()
        {
            self = transform as RectTransform;
            EnsureGroup();
            SetAlpha(0f);
            CacheBasePositions();
        }

        private void OnDisable()
        {
            KillPulse();
            SetAlpha(0f);
        }

        public void Show()
        {
            EnsureGroup();
            SetAlpha(1f);
            PlayPulse();
        }

        public void Hide()
        {
            KillPulse();
            SetAlpha(0f);
        }

        public void Move(GameObject targetObject)
        {
            target = targetObject ? targetObject.transform as RectTransform : null;
            if (target && self)
            {
                self.position = target.position;
                self.sizeDelta = target.rect.size;
            }
        }

        private void LateUpdate()
        {
            if (!target || !self) return;
            var t = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            self.position = Vector3.Lerp(self.position, target.position, t);
            self.sizeDelta = Vector2.Lerp(self.sizeDelta, target.rect.size, t);
        }

        private void CacheBasePositions()
        {
            if (corners == null) { basePositions = new Vector2[0]; return; }
            basePositions = new Vector2[corners.Length];
            for (var i = 0; i < corners.Length; i++)
                if (corners[i]) basePositions[i] = corners[i].anchoredPosition;
        }

        private static Vector2 Outward(int index)
        {
            switch (index)
            {
                case 0: return new Vector2(-1f, 1f).normalized;
                case 1: return new Vector2(1f, 1f).normalized;
                case 2: return new Vector2(-1f, -1f).normalized;
                case 3: return new Vector2(1f, -1f).normalized;
                default: return Vector2.zero;
            }
        }

        private void PlayPulse()
        {
            KillPulse();
            if (corners == null) return;
            if (basePositions == null || basePositions.Length != corners.Length) CacheBasePositions();
            for (var i = 0; i < corners.Length; i++)
            {
                var corner = corners[i];
                if (!corner) continue;
                var to = basePositions[i] + Outward(i) * pulseDistance;
                var tween = corner.DOAnchorPos(to, pulseDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
                tweens.Add(tween);
            }
        }

        private void KillPulse()
        {
            for (var i = 0; i < tweens.Count; i++) tweens[i]?.Kill();
            tweens.Clear();
            if (corners == null || basePositions == null) return;
            for (var i = 0; i < corners.Length && i < basePositions.Length; i++)
                if (corners[i]) corners[i].anchoredPosition = basePositions[i];
        }

        private void EnsureGroup()
        {
            if (!group) group = GetComponent<CanvasGroup>();
            if (!group) group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private void SetAlpha(float alpha)
        {
            EnsureGroup();
            if (group) group.alpha = alpha;
        }
    }
}
