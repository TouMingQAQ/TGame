using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TGame.TUI
{
    public abstract class UILoader : MonoBehaviour
    {
        public void LoadAll() => LoadAllAsync().Forget();
        public void UnLoadAll() => UnloadAllAsync().Forget();
        public abstract UniTask LoadAllAsync();
        public abstract UniTask UnloadAllAsync();
    }
}