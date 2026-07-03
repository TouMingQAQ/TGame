using System;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.GameSystem
{
    public struct GameTimeChangeEvent
    {
        public DateTime Time;
    }

    public struct GameTimePauseEvent
    {
        public DateTime Time;
    }

    public struct GameTimeResumeEvent
    {
        public DateTime Time;
    }

    public enum GameTimeState
    {
        /// <summary>
        /// 正常运行
        /// </summary>
        Normal,
        /// <summary>
        /// 加速时间
        /// </summary>
        TimeTo,
        /// <summary>
        /// 时间暂停
        /// </summary>
        Pause,
    }
    /// <summary>
    /// 时间系统
    /// </summary>
    public sealed class GameTimeSystem : GameSystem
    {
        private DateTime _currentTime;
        public DateTime CurrentTime => _currentTime;

        private GameTimeState  _gameTimeState;
        /// <summary>
        /// 时间状态
        /// </summary>
        public GameTimeState State{
            get
            {
                return _gameTimeState;
            }
            private set
            {
                _gameTimeState = value;
            }
        }
        /// <summary>
        /// 是否触发时间加速
        /// </summary>
        private DateTime _targetTime;
        /// <summary>
        /// 加速时间时的时间缩放
        /// 小于等于0时表示直接修改到目标时间
        /// </summary>
        [Header("跳转时间的缩放时间")]
        [SerializeField]
        private float _timeToScale = -1;
        [Header("正常运行时的缩放时间")]
        [SerializeField]
        private float _timeScale = 10f;
        public float TimeScale => _timeScale;


        private EventModule eventModule;
        public override void OnInit(GameSystemManager manager)
        {
            base.OnInit(manager);
            eventModule = manager.GetModule<EventModule>();
            _currentTime = DateTime.Now;
        }

        private void Update()
        {
            switch (State)
            {
                case GameTimeState.Normal:
                    NormalUpdate();
                    break;
                case GameTimeState.TimeTo:
                    TimeToUpdate();
                    break;
                case GameTimeState.Pause:
                    break;
            }
        }

        void NormalUpdate()
        {
            var deltaSeconds = _timeScale * Time.deltaTime;
            _currentTime = _currentTime.AddSeconds(deltaSeconds);
            eventModule.Call(new GameTimeChangeEvent { Time = _currentTime });
        }

        private GameTimeState _timeToStateCache;
        void TimeToUpdate()
        {
            if (_timeToScale <= 0)
            {
                _currentTime = _targetTime;
            }
            else
            {
                var deltaSeconds = _timeToScale * Time.deltaTime;
                _currentTime = _currentTime.AddSeconds(deltaSeconds);
                if (_currentTime < _targetTime)
                {
                    eventModule.Call(new GameTimeChangeEvent { Time = _currentTime });
                    return;
                }
                _currentTime = _targetTime;
            }
            State = _timeToStateCache;
            eventModule.Call(new GameTimeChangeEvent { Time = _currentTime });
        }

        private GameTimeState stateCache;
        
        public void Pause()
        {
            if(State==GameTimeState.Pause || State == GameTimeState.TimeTo)
                return;
            stateCache = State;
            eventModule.Call(new GameTimePauseEvent()
            {
                Time = CurrentTime
            });
            State = GameTimeState.Pause;
        }

        public void Resume()
        {
            if(State != GameTimeState.Pause)
                return;
            eventModule.Call(new GameTimeResumeEvent()
            {
                Time = CurrentTime
            });
            State = stateCache;
        }
        /// <summary>
        /// 加速时间
        /// </summary>
        /// <param name="targetTime">目标时间</param>
        /// <param name="timeScale">加速速度 x/s</param>
        public void TimeTo(DateTime targetTime, float timeScale = -1)
        {
            if (targetTime <= _currentTime)
                return;
            _timeToScale = timeScale;
            _timeToStateCache = State;
            State = GameTimeState.TimeTo;
            _targetTime = targetTime;
        }

        public void SetTimeScale(float scale)
        {
            if (scale <= 0)
            {
                Debug.LogWarning("[GameTimeSystem] timeScale must be > 0");
                return;
            }
            _timeScale = scale;
        }

        public void SetCurrentTime(DateTime time)
        {
            _currentTime = time;
            eventModule.Call(new GameTimeChangeEvent { Time = _currentTime });
        }
    }
}