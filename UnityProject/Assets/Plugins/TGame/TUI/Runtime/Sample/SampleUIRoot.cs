using System;
using UnityEngine;

namespace TGame.TUI
{
    public class SampleUIRoot : UIRoot
    {
        protected override async void Start()
        {
            await Initialize();
            await ShowPanelAsync<SamplePanel>();
        }

        public override Type RootType()
        {
            return typeof(SampleUIRoot);
        }
    }
}
