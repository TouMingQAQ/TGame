using System;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    public class TUISample : MonoBehaviour
    {
        private void OnEnable()
        {
            Game.Instance.GetManager<UIManager>().ShowPanel<HelloPanel>();
        }
    }
}
