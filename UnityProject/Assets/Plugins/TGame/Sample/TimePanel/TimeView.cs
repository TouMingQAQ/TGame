using System;
using TGame.GameSystem;
using TGame.TUI.MVVM.View;
using TMPro;
using UnityEngine;

namespace TGame.TUI
{
    public class TimeView : BaseView<GameTimeChangeEvent>
    {
        [SerializeField]
        private TMP_Text timeText;

        public override void OnRefreshView(GameTimeChangeEvent newModel, GameTimeChangeEvent oldModel)
        {
            timeText.SetText(newModel.Time.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        public override bool NeedRefreshView()
        {
            return true;
        }
    }
}