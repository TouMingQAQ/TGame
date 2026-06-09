using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace TGame.TUI
{
    public class TButtonHold : TButtonExtension,IPointerDownHandler,IPointerUpHandler
    {
        public TButtonHold(){}
        #region Event
        [HideInInspector]
        public UnityEvent onStartHold;
        [HideInInspector]
        public UnityEvent onEndHold;
        /// <summary>
        /// 有最大时间时，返回的是进度，没有则返回当前Hold时间
        /// </summary>
        [HideInInspector]
        public UnityEvent<float> onHoldProgress;

        #endregion
        /// <summary>
        /// 是否自动结束Hold
        /// </summary>
        public bool IsAutoHoldEnd = true;
        /// <summary>
        /// 是否Hold
        /// </summary>
        [SerializeField]
        private bool isHoldOn = false;
        /// <summary>
        /// 是否Hold
        /// </summary>
        public bool IsHoldOn => isHoldOn;
        /// <summary>
        /// 开始统计的时间
        /// </summary>
        [SerializeField,Range(0,1)]
        private float startHoldTime = 0.2f;
        /// <summary>
        /// 结束统计的冗余时间
        /// </summary>
        [SerializeField,Range(0,1)]
        private float endHoldTime = 0.2f;
        /// <summary>
        /// 最大时间，如果为-1，则无限
        /// </summary>
        [SerializeField]
        private float maxTime = -1;

        public float MaxTime
        {
            get => maxTime;
            set => maxTime = value;
        }
 
        private float timer = 0;
        private void OnEnable()
        {
            timer = 0;
            isHoldOn = false;
        }
        private void Update()
        {
            startHoldTime = MathF.Abs(startHoldTime);//避免负数
            if(!isHoldOn)
                return;
            if (!button.CanUse())
            {
                isHoldOn = false;
                onEndHold?.Invoke();
                return;
            }

            timer += Time.unscaledDeltaTime;
            if(timer <= startHoldTime)
                return;//还没到触发Hold的时间
            if (maxTime > 0)
            {
                //Hold进度，自动触发HoldEnd
                var progress = (timer - startHoldTime) / maxTime;
                progress = Mathf.Clamp01(progress);
                onHoldProgress?.Invoke(progress);
                
                if (timer >= maxTime+endHoldTime && IsAutoHoldEnd)
                {
                    if (IsAutoHoldEnd)
                    {
                        EndHold();
                    }
                }
            }
            else
            {
                onHoldProgress?.Invoke(timer - startHoldTime);
            }
            
        }

        void StartHold()
        {
            isHoldOn = true;
            onStartHold?.Invoke();
            timer = 0;
        }

        void EndHold()
        {
            isHoldOn = false;
            onEndHold?.Invoke();
            timer = 0;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!button.CanUse() && !isHoldOn)
                return;
            StartHold();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!button.CanUse() && isHoldOn)
                return;
            EndHold();
        }
    }
}
