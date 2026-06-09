using UnityEngine;
using UnityEngine.EventSystems;

namespace TGame.TUI
{
    /// <summary>
    /// TButton的动画
    /// </summary>
    public abstract class TButtonAnimation : TButtonExtension,
        IPointerEnterHandler,IPointerExitHandler,
        IPointerDownHandler,IPointerUpHandler
    {
        public abstract void OnPointerEnter(PointerEventData eventData);

        public abstract void OnPointerExit(PointerEventData eventData);

        public abstract void OnPointerDown(PointerEventData eventData);

        public abstract void OnPointerUp(PointerEventData eventData);
    }
}
