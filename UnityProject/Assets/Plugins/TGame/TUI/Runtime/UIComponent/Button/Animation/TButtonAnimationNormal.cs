using UnityEngine;
using UnityEngine.EventSystems;

namespace TGame.TUI
{
    public class TButtonAnimationNormal : TButtonAnimation
    {
        public Vector3 normalScale = Vector3.one;
        [SerializeField]
        private Vector3 downScale = new Vector3(0.95f,0.95f,0);
        [SerializeField]
        private Vector3 enterScale = new Vector3(1.05f,1.05f,0);
        
        public override void OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = enterScale;
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = normalScale;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            transform.localScale = downScale;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = normalScale;
        }
    }
}
