using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TGame.TUI
{
    public class AnimationCurveTest : MonoBehaviour
    {
        [SerializeField]
        private AnimationCurve curve;
        [SerializeField]
        private Button button;
        [SerializeField]
        private Transform root;
        private void Awake()
        {
            button.onClick.AddListener(Play);
        }

        void Play()
        {
            root.DOKill();
            root.localScale = Vector3.zero;
            root.DOScale(Vector3.one, 0.2f).SetLoops(1).SetAutoKill(true).SetEase(curve).Play();
        }
    }
}
