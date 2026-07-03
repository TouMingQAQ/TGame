using TGame.TUI.MVVM.View;
using TMPro;
using UnityEngine;

namespace TGame.TUI
{
    public class NumberView : BaseView<NumberValue>
    {
        [SerializeField]
        private TMP_Text numberText;
        public override void OnRefreshView(NumberValue newModel, NumberValue oldModel)
        {
            numberText.SetText(newModel.Value.ToString());
        }

        public override bool NeedRefreshView()
        {
            return true;
        }
    }
}
