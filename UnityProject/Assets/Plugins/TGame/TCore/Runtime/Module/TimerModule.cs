using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace TGame.TCore.Runtime
{
    /// <summary>
    /// 计时器模块。
    /// 每个 TimerModule 实例拥有独立的对象池，多个 Manager 各挂一个 TimerModule 时不会互相串号。
    /// </summary>
    public sealed class TimerModule : BaseModule
    {
        private readonly Dictionary<string, TimerEvent> _timerEventMap = new();

        /// <summary>
        /// 实例级对象池（原实现为 static，跨模块共享，会导致 A 模块回收的 TimerEvent 被 B 模块取到）。
        /// </summary>
        private readonly ObjectPool<TimerEvent> _timerEventPool = new ObjectPool<TimerEvent>(
            createFunc: () => new TimerEvent(),
            actionOnRelease: ResetTimerEvent,
            actionOnDestroy: null,
            collectionCheck: true,
            defaultCapacity: 8,
            maxSize: 64);

        /// <summary>
        /// Tick 过程中需要回收的 TimerEvent，延迟到遍历结束后统一 Release，
        /// 避免在 foreach 期间改字典或把仍在使用的实例提前入池。
        /// </summary>
        private readonly List<TimerEvent> _releaseCache = new();
        private readonly List<string> _removeCache = new();

        public override void Destroy()
        {
            Clear();
        }

        public override void Tick(float deltaTime)
        {
            if (_timerEventMap.Count == 0)
                return;

            // 遍历所有 TimerEvent 并进行计时，达到目标时间则执行回调
            foreach (var pair in _timerEventMap)
            {
                var timerEvent = pair.Value;
                if (timerEvent == null || timerEvent.Timer < 0)
                    continue;

                timerEvent.Timer += deltaTime;
                if (timerEvent.Timer < timerEvent.TargetTime)
                    continue;

                if (timerEvent.RepeatCount == TimerEvent.InfiniteRepeat)
                {
                    // 无限循环：按周期推进，避免长时间运行后 Timer 累加过大
                    timerEvent.Timer -= timerEvent.TargetTime;
                    timerEvent.CallBack?.Invoke();
                }
                else if (timerEvent.RepeatCount > 0)
                {
                    // 有限循环：本帧内可能跨过多个周期
                    timerEvent.Timer -= timerEvent.TargetTime;
                    timerEvent.RepeatCount--;
                    timerEvent.TotalRepeatTriggered++;
                    if (timerEvent.RepeatCount == 0)
                        timerEvent.Timer = -1;
                    timerEvent.CallBack?.Invoke();
                    if (timerEvent.RepeatCount == 0)
                    {
                        _removeCache.Add(pair.Key);
                        _releaseCache.Add(timerEvent);
                    }
                }
                else
                {
                    // 一次性定时器：触发后标记为 -1，等待本帧结束后回收槽位
                    timerEvent.Timer = -1;
                    timerEvent.CallBack?.Invoke();

                    _removeCache.Add(pair.Key);
                    _releaseCache.Add(timerEvent);
                }
            }

            // 统一回收，避免回调里再次 Register 同名定时器时被这里覆盖
            for (int i = 0; i < _removeCache.Count; i++)
            {
                var name = _removeCache[i];
                if (_timerEventMap.TryGetValue(name, out var current) && current == _releaseCache[i])
                    _timerEventMap.Remove(name);
            }
            for (int i = 0; i < _releaseCache.Count; i++)
                ReleaseInternal(_releaseCache[i]);

            _removeCache.Clear();
            _releaseCache.Clear();
        }

        /// <summary>
        /// 注册 TimerEvent 到字典。
        /// </summary>
        /// <param name="timerEvent">定时器实例，可通过 <see cref="Create"/> 创建</param>
        /// <param name="cover">同名时是否覆盖未触发的旧定时器</param>
        public void Register(TimerEvent timerEvent, bool cover = false)
        {
            if (timerEvent == null)
                return;
            if (string.IsNullOrEmpty(timerEvent.Name))
            {
                ReleaseInternal(timerEvent);
                Debug.LogWarning("[TimerModule] TimerEvent Name is null or empty");
                return;
            }
            if (!_timerEventMap.TryAdd(timerEvent.Name, timerEvent))
            {
                var lastTimerEvent = _timerEventMap[timerEvent.Name];
                //当覆盖或者Event的Timer标记为-1时，直接替换
                if (cover || lastTimerEvent == null || lastTimerEvent.Timer < 0)
                {
                    ReleaseInternal(lastTimerEvent);
                    _timerEventMap[timerEvent.Name] = timerEvent;
                }
                else
                {
                    ReleaseInternal(timerEvent);
                    Debug.LogWarning($"[TimerModule] TimerEvent Name:{timerEvent.Name} is exist");
                }
            }
        }
        /// <summary>
        /// 注销TimerEvent
        /// </summary>
        /// <param name="name"></param>
        public void UnRegister(string name)
        {
            if (name != null && _timerEventMap.Remove(name, out var timerEvent))
                ReleaseInternal(timerEvent);
        }
        /// <summary>
        /// 清理所有TimerEvent
        /// </summary>
        public void Clear()
        {
            foreach (var value in _timerEventMap.Values)
                ReleaseInternal(value);

            _timerEventMap.Clear();
            _releaseCache.Clear();
            _removeCache.Clear();
        }
        /// <summary>
        /// 指定名字的定时器是否仍在计时
        /// </summary>
        public bool IsRunning(string name)
        {
            return name != null
                   && _timerEventMap.TryGetValue(name, out var timerEvent)
                   && timerEvent != null
                   && timerEvent.Timer >= 0;
        }
        /// <summary>
        /// 剩余时间，未注册或已触发时返回 0
        /// </summary>
        public float GetRemaining(string name)
        {
            if (!IsRunning(name))
                return 0f;
            var timerEvent = _timerEventMap[name];
            return Mathf.Max(0f, timerEvent.TargetTime - timerEvent.Timer);
        }

        /// <summary>
        /// 创建 TimerEvent（从本模块的实例池中取）。
        /// </summary>
        /// <param name="name">定时器名字，同名字重复注册需 cover</param>
        /// <param name="time">间隔秒数，需大于 0</param>
        /// <param name="callBack">触发回调</param>
        /// <param name="repeatCount">循环次数：默认 0 表示一次性；<see cref="TimerEvent.InfiniteRepeat"/> 表示无限循环</param>
        /// <returns></returns>
        public TimerEvent Create(string name, float time, Action callBack, int repeatCount = 0)
        {
            var timerEvent = _timerEventPool.Get();
            timerEvent.Name = name;
            timerEvent.TargetTime = time;
            timerEvent.CallBack = callBack;
            timerEvent.RepeatCount = repeatCount;
            timerEvent.TotalRepeatTriggered = 0;
            return timerEvent;
        }

        /// <summary>
        /// 快捷注册一个一次性定时器。
        /// </summary>
        public TimerEvent RegisterOnce(string name, float time, Action callBack, bool cover = false)
        {
            var timerEvent = Create(name, time, callBack);
            Register(timerEvent, cover);
            return timerEvent;
        }

        /// <summary>
        /// 快捷注册一个每 <paramref name="interval"/> 秒触发一次的循环定时器。
        /// </summary>
        /// <param name="interval">间隔秒数，需大于 0</param>
        /// <param name="repeatCount">循环次数，默认无限</param>
        public TimerEvent RegisterInterval(string name, float interval, Action callBack,
            int repeatCount = TimerEvent.InfiniteRepeat, bool cover = false)
        {
            var timerEvent = Create(name, interval, callBack, repeatCount);
            Register(timerEvent, cover);
            return timerEvent;
        }

        /// <summary>
        /// 回收 TimerEvent（归还到本模块的实例池）
        /// </summary>
        /// <param name="timerEvent"></param>
        public void Release(TimerEvent timerEvent)
        {
            ReleaseInternal(timerEvent);
        }

        /// <summary>
        /// 回收并重置实例，内部统一走这里，保证下次取出时是干净状态。
        /// </summary>
        private void ReleaseInternal(TimerEvent timerEvent)
        {
            if (timerEvent == null)
                return;
            timerEvent.CallBack = null;
            timerEvent.Timer = -1;
            timerEvent.RepeatCount = 0;
            _timerEventPool.Release(timerEvent);
        }

        private static void ResetTimerEvent(TimerEvent timerEvent)
        {
            timerEvent.Name = string.Empty;
            timerEvent.Timer = -1;
            timerEvent.TargetTime = 0;
            timerEvent.CallBack = null;
            timerEvent.RepeatCount = 0;
            timerEvent.TotalRepeatTriggered = 0;
        }

        [Serializable]
        public class TimerEvent
        {
            /// <summary>
            /// 无限循环标记
            /// </summary>
            public const int InfiniteRepeat = -1;

            public string Name;
            public float Timer;
            public float TargetTime;
            public Action CallBack;

            /// <summary>
            /// 剩余循环次数。0 = 一次性；&gt;0 = 剩余触发次数；-1 = 无限循环。
            /// </summary>
            public int RepeatCount;

            /// <summary>
            /// 已触发的循环次数（含一次性定时器的 1 次）。
            /// </summary>
            public int TotalRepeatTriggered;
        }
    }
}
