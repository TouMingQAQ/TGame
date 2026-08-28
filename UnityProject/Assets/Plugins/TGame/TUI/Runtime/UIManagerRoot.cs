using System;

namespace TGame.TUI
{
    public class UIManagerRoot : UIRoot
    {
        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
        }

        public override Type RootType()
        {
            return typeof(UIManagerRoot);
        }
    }
}