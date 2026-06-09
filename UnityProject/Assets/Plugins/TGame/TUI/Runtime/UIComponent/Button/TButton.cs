using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TGame.TUI
{
    /// <summary>
    /// 集中的Button，其他Button扩展都需要通过这个
    /// </summary>
    public class TButton : Selectable
        ,IPointerClickHandler
    {

        #region Event

        /// <summary>
        /// 基础的点击事件
        /// </summary>
        public UnityEvent onClick;
        /// <summary>
        /// 左键点击
        /// </summary>
        [HideInInspector]
        public UnityEvent onLeftClick;
        /// <summary>
        /// 右键点击
        /// </summary>
        [HideInInspector]
        public UnityEvent onRightClick;
        /// <summary>
        /// 中建点击
        /// </summary>
        [HideInInspector]
        public UnityEvent onMiddleClick;

        #endregion

        public virtual bool CanUse()
        {
            return IsActive() && IsInteractable();
        }
        
        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if(!CanUse())
                return;
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    onLeftClick?.Invoke();
                    break;
                case PointerEventData.InputButton.Right:
                    onRightClick?.Invoke();
                    break;
                case PointerEventData.InputButton.Middle:
                    onMiddleClick?.Invoke();
                    break;
                default:
                    break;
            }
            onClick?.Invoke();
        }

        #region 主动触发事件

        public void PointClick()
        {
            ExecuteEvents.Execute(this.gameObject, CreateEvt(),ExecuteEvents.pointerClickHandler);
        }

        public void PointDown()
        {
            ExecuteEvents.Execute(this.gameObject, CreateEvt(),ExecuteEvents.pointerDownHandler);
        }

        public void PointUp()
        {
            ExecuteEvents.Execute(this.gameObject, CreateEvt(),ExecuteEvents.pointerUpHandler);
        }
        protected PointerEventData CreateEvt()
        {
            return new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
        }
        #endregion
        

        protected override void Reset()
        {
            base.Reset();
            transition = Transition.None;
        }
    }
    /// <summary>
    /// TButton拓展
    /// </summary>
    [RequireComponent(typeof(TButton))]
    public abstract class TButtonExtension : MonoBehaviour
    {   
        [SerializeField]
        protected TButton button;
        
        protected virtual void Reset()
        {
            TryGetComponent(out button);
        }
    }
}
