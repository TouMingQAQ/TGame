using TGame.GameSystem;
using TGame.TCore.Runtime;
using TGame.TUI.MVVM;
using TGame.TUI.MVVM.Model;
using UnityEngine;

namespace TGame.TUI
{

    public class GameTimeModel : BaseModel<GameTimeChangeEvent>
    {
        protected override bool NeedUpdateValue(GameTimeChangeEvent newValue, GameTimeChangeEvent oldValue)
        {
            return newValue.Time != oldValue.Time;
        }
    }
    public class TimePanel : BaseMVVMPanel<GameTimeChangeEvent>
    {
        [SerializeField]
        private TButton _resumeButton;
        [SerializeField]
        private TButton _pauseButton;
        [SerializeField]
        private TButton _timeToButton;

        private GameSystemManager _manager;
        private GameTimeSystem _system;
        private EventModule _eventModule;
        protected override void Awake()
        {
            base.Awake();
            _manager = Game.Instance.GetManager<GameSystemManager>();
            _system = _manager.GetGameSystem<GameTimeSystem>();
            _eventModule = Game.Instance.GetManager<GameSystemManager>().GetModule<EventModule>();

            var model = new GameTimeModel();
            model.SetModel(new GameTimeChangeEvent()
            {
                Time = _system.CurrentTime,
            });
            BindModel( new GameTimeModel());
            _resumeButton.onClick.AddListener(ResumeTime);
            _pauseButton.onClick.AddListener(PauseTime);
            _timeToButton.onClick.AddListener(TimeTo);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _eventModule.Register<GameTimeChangeEvent>(OnTimeChange);
            _eventModule.Register<GameTimePauseEvent>(OnTimePause);
            _eventModule.Register<GameTimeResumeEvent>(OnTimeResume);
        }

        private void OnDisable()
        {
            _eventModule.UnRegister<GameTimeChangeEvent>(OnTimeChange);
            _eventModule.UnRegister<GameTimePauseEvent>(OnTimePause);
            _eventModule.UnRegister<GameTimeResumeEvent>(OnTimeResume);
        }

        void ResumeTime()
        {
            _system.Resume();
        }

        void PauseTime()
        {
            _system.Pause();
        }

        void TimeTo()
        {
            var system =  Game.Instance.GetManager<GameSystemManager>().GetGameSystem<GameTimeSystem>();
            var currenTime = system.CurrentTime;
            currenTime = currenTime.AddDays(10);
            system.TimeTo(currenTime,100000);
        }
        
        void OnTimeChange(GameTimeChangeEvent evt)
        {
            var model = _model.Model;
            model.Time = evt.Time;
            _model.SetModel(model);
        }
        void OnTimePause(GameTimePauseEvent evt)
        {
            
        }

        void OnTimeResume(GameTimeResumeEvent evt)
        {
            
        }
    }
}
