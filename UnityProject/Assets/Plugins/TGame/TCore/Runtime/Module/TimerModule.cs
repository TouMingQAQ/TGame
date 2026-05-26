using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace TGame.TCore.Runtime
{
    /// <summary>
    /// 计时器模块
    /// </summary>
    public sealed class TimerModule : BaseModule
    {
        private Dictionary<string,TimerEvent> _timerEventMap = new();
        
        public override void Destroy()
        {
            Clear();
        }

        public override void Tick(float deltaTime)
        {
            // 遍历所有TimerEvent并进行计时，如果时间达到要求，执行回调并删除
            foreach (var timerEvent in _timerEventMap.Values)
            {
                if(timerEvent.Timer < 0) continue;
                timerEvent.Timer += deltaTime;
                if (timerEvent.Timer >= timerEvent.TargetTime)
                {
                    timerEvent.CallBack?.Invoke();
                    timerEvent.Timer = -1;
                }
            }
        }
       
        /// <summary>
        /// 注册TimerEvent到字典
        /// </summary>
        /// <param name="timerEvent"></param>
        /// <param name="cover"></param>
        /// <exception cref="Exception"></exception>
        public void Register(TimerEvent timerEvent, bool cover = false)
        {
            if(timerEvent == null)
                return;
            if (timerEvent.Name == null)
            {
                Release(timerEvent);
                Debug.LogWarning("TimerEvent Name is null");
                return;
            }
            if (!_timerEventMap.TryAdd(timerEvent.Name, timerEvent))
            {
                var lastTimerEvent = _timerEventMap[timerEvent.Name];
                //当覆盖或者Event的Timer标记为-1时，直接替换
                if (cover || lastTimerEvent.Timer < 0)
                {
                    Release(lastTimerEvent);
                    _timerEventMap[timerEvent.Name] = timerEvent;
                }
                else
                {
                    Release(timerEvent);
                    Debug.LogWarning($"TimerEvent Name:{timerEvent.Name} is exist");
                }
            }
        }
        /// <summary>
        /// 注销TimerEvent
        /// </summary>
        /// <param name="name"></param>
        public void UnRegister(string name)
        {
            if (_timerEventMap.Remove(name, out var timerEvent))
            {
                if(timerEvent != null)
                    Release(timerEvent);
            }
        }
        /// <summary>
        /// 清理所有TimerEvent
        /// </summary>
        public void Clear()
        {
            foreach (var value in _timerEventMap.Values)
            {
                if(value != null)
                    Release(value);
            }
            _timerEventMap.Clear();
        }
        private static ObjectPool<TimerEvent> _timerEventPool = new ObjectPool<TimerEvent>(() => new TimerEvent(),
            (e) =>
            {
                e.TargetTime = 0;
                e.Name = string.Empty;
                e.Timer = 0;
                e.CallBack = null;
            });
        /// <summary>
        /// 创建TimerEvent
        /// </summary>
        /// <param name="name"></param>
        /// <param name="time"></param>
        /// <param name="callBack"></param>
        /// <returns></returns>
        public static TimerEvent Create(string name, float time, Action callBack)
        {
            _timerEventPool.Get(out var timerEvent);
            timerEvent.Name = name;
            timerEvent.TargetTime = time;
            timerEvent.CallBack = callBack;
            return timerEvent;
        }
        /// <summary>
        /// 回收
        /// </summary>
        /// <param name="timerEvent"></param>
        public static void Release(TimerEvent timerEvent)
        {
            if(timerEvent == null)
                return;
            _timerEventPool.Release(timerEvent);
        }
        
        [Serializable]
        public class TimerEvent
        {
            public string Name;
            public float Timer;
            public float TargetTime;
            public Action CallBack;
        }
    }
}