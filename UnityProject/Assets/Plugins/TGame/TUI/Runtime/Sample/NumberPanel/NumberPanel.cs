using TGame.TUI.MVVM;
using TGame.TUI.MVVM.Model;
using UnityEngine;
using UnityEngine.UI;

namespace TGame.TUI
{
    public struct NumberValue
    {
        public int Value;
    }

    public class NumberModel : BaseModel<NumberValue>
    {
        protected override bool NeedUpdateValue(NumberValue newValue, NumberValue oldValue)
        {
            return newValue.Value != oldValue.Value;
        }
    }
    public class NumberPanel : BaseMVVMPanel<NumberValue>
    {
        [SerializeField]
        private Button addBtn;
        [SerializeField]
        private Button reduceBtn;


        public void Add()
        {
            var model = _model.Model;
            model.Value++;
            _model.SetModel(model);
        }

        public void Reduce()
        {
            var model = _model.Model;
            model.Value--;
            _model.SetModel(model);
        }
        protected override void Awake()
        {
            base.Awake();
            _model = new NumberModel();
            BindModel(_model);
            addBtn.onClick.AddListener(Add);
            reduceBtn.onClick.AddListener(Reduce);
        }
    }
}
