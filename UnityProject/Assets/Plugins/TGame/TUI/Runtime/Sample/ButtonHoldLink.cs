using System;
using UnityEngine;
using UnityEngine.UI;

namespace TGame.TUI
{
    public class ButtonHoldLink : MonoBehaviour
    {
        [SerializeField] private TButtonHold buttonHold;
        [SerializeField] private Image progressImage;

        private void Awake()
        {
            buttonHold.onEndHold.AddListener(() =>
            {
                TDebug.Log("ButtonHold",0,"End");
                SetProgress(0);
            });
            buttonHold.onStartHold.AddListener(() =>
            {
                TDebug.Log("ButtonHold",0,"Start");
                SetProgress(0);
            });
            buttonHold.onHoldProgress.AddListener(SetProgress);
        }

        void SetProgress(float value)
        {
            progressImage.fillAmount = value;
        }
    }
}
