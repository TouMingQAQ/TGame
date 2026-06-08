using System;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    public class TUISample : MonoBehaviour
    {
        [SerializeField]
        private HelloPanel panelPrefab;

        private void Awake()
        {
            var uimgr = Game.Instance.GetManager<UIManager>();
            uimgr.RegisterPanel(panelPrefab);
        }

        private void OnEnable()
        {
            Game.Instance.GetManager<UIManager>().ShowPanel<HelloPanel>();
        }

        private void OnDisable()
        {
            Game.Instance.GetManager<UIManager>().HidePanel<HelloPanel>();
        }
    }
}
