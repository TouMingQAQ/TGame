using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TGame.TUI
{
    public class HelloPanel : BaseUIPanel
    {
        [SerializeField]
        private Button closeBtn;

        protected override void Awake()
        {
            base.Awake();
            closeBtn.onClick.AddListener(Hide);
        }

        protected override void BeforeShow()
        {
            TDebug.Log("HelloPanel",1,"BeforeShow");
        }

        protected override void AfterShow()
        {
            TDebug.Log("HelloPanel",1,"AfterShow");
        }

        protected override void AfterHide()
        {
            TDebug.Log("HelloPanel",1,"AfterHide");
        }

        protected override void BeforeHide()
        {
            TDebug.Log("HelloPanel",1,"BeforeHide");
        }
    }
}
