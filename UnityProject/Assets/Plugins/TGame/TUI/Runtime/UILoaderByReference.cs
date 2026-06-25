using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TGame.TCore.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TGame.TUI
{
    public class UILoaderByReference : UILoader
    {
        [SerializeField] private List<AssetReferenceT<BaseUIPanel>> preloadUI = new();
        public  override async UniTask LoadAllAsync()
        {
            var uiMgr = Game.Instance.GetManager<UIManager>();
            //Todo:在UIManager预加载
            return;
        }

        public override async UniTask UnloadAllAsync()
        {
        }
    }
}