using System;
using Cysharp.Threading.Tasks;
using TGame.GameSystem;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    public class SampleUIRoot : UIRoot
    {
        
        protected override void Start()
        {
            StartAsync().Forget();
        }

        private async UniTask StartAsync()
        {
            await Initialize();
            await ShowPanelAsync<SamplePanel>();
            Game.Instance.GetManager<GameSystemManager>().GetGameSystem<GameTimeSystem>().SetCurrentTime(DateTime.Now);
        }

        public override Type RootType()
        {
            return typeof(SampleUIRoot);
        }
    }
}
