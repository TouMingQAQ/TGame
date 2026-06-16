using System;
using System.Collections.Generic;
namespace TGame.FSM
{
    public readonly struct FSMStateHandle<T>  where T : FSMState<T>, IFSMState, new()
    {
        public IFSMControl Control { get; }
        public IFSMState State { get; }

        public FSMStateHandle(IFSMControl control, IFSMState state)
        {
            Control = control;
            State = state;
        }

        /// <summary>
        /// 从 Control Remove
        /// </summary>
        public FSMStateHandle<T> Remove()
        {
            if (State == null)
                return this;
            if (Control != null)
                Control.RemoveState<T>();
            return this;
        }
    }

    public interface IFSMControl
    {
        /// <summary>
        /// 初始化
        /// </summary>
        public void Init();
        /// <summary>
        /// 切换状态
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public bool ChangeState<T>() where T : FSMState<T>, new();

        /// <summary>
        /// 添加状态
        /// </summary>
        /// <param name="state"></param>
        /// <typeparam name="T"></typeparam>
        public FSMStateHandle<T> AddState<T>(FSMState<T> state) where T : FSMState<T>, new();


        /// <summary>
        /// 移除State
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public FSMStateHandle<T> RemoveState<T>() where T : FSMState<T>, new();

        /// <summary>
        /// 推进状态机:驱动当前 state 的 OnTick
        /// </summary>
        /// <param name="deltaTime">距上一次调用的时间间隔(秒)</param>
        public void Update(float deltaTime);
    }
    /// <summary>
    /// FSMControl
    /// </summary>
    public abstract class FSMControl<TDefault>:IFSMControl where TDefault : FSMState<TDefault>, new()
    {
        public FSMControl(TDefault defaultState)
        {
            DefaultState = defaultState;
        }

        public void Init()
        {
            // 重置到默认状态:清空当前 state 与 states 字典,重新注册 DefaultState,并切到 DefaultState
            // 注意:这会丢弃之前 AddState 注册的所有非默认 state;若需保留,请在调用 Init 前自行管理
            currentState = null;
            states.Clear();
            AddState(DefaultState);
            ChangeState<TDefault>();
        }
        /// <summary>
        /// 默认状态
        /// </summary>
        protected TDefault DefaultState { get;private set; }
        
        /// <summary>
        /// 当前状态
        /// </summary>
        protected IFSMState currentState = null;

        /// <summary>
        /// State字典
        /// </summary>
        protected Dictionary<Type, IFSMState> states = new Dictionary<Type, IFSMState>();

   
        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="deltaTime"></param>
        public void Update(float deltaTime)
        {
            if(currentState != null)
                currentState.OnTick(this,deltaTime);
        }

        #region ChangeState

        /// <summary>
        /// 控制标签，避免死循环
        /// </summary>
        private bool changeStateFlg = false;
        /// <summary>
        /// 重入 ChangeState 时挂起的目标 state 类型队列,出栈后按序消费(支持任意深度 chain)
        /// </summary>
        private readonly Queue<Type> pendingStateQueue = new Queue<Type>();
        /// <summary>
        /// 当前 transition 链上已经访问过的 state 类型,防止 A↔B 互切导致死循环
        /// (stack-local 语义:每次最外层 ChangeState 起点清空,内部链上累计)
        /// </summary>
        private readonly HashSet<Type> visitedThisTransition = new HashSet<Type>();
        /// <summary>
        /// 切换状态
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public bool ChangeState<T>() where T : FSMState<T>, new()
        {
            var typ = typeof(T);

            // 重入:把目标 state 入队(基于当前 transition 链去重,防止 A↔B 互切死循环)
            if (changeStateFlg)
            {
                if (visitedThisTransition.Contains(typ))
                    return true;
                visitedThisTransition.Add(typ);
                pendingStateQueue.Enqueue(typ);
                return true;
            }

            // 同状态短路:已经在该状态,直接返回(避免 OnExit/OnEnter 重复触发)
            if (currentState != null && currentState.GetType() == typ)
                return true;

            // 启动新 transition:清空 visited,起点入集合
            visitedThisTransition.Clear();
            visitedThisTransition.Add(typ);
            return ChangeStateInternal(typ);
        }

        /// <summary>
        /// 实际执行切换:同状态短路 + OnExit/OnEnter 配对 + 出栈消费 pending queue
        /// </summary>
        private bool ChangeStateInternal(Type typ)
        {
            if (!states.TryGetValue(typ, out var state))
                return false;

            // 二次短路:防止 OnExit/OnEnter 中通过 queue 切到当前 state
            if (currentState != null && currentState.GetType() == typ)
                return true;

            changeStateFlg = true;
            try
            {
                currentState?.OnExit(this);
                currentState = state;
                state.OnEnter(this);
            }
            finally
            {
                changeStateFlg = false;
            }

            // 反复消费,支持任意深度 chain
            // 队列中若有未注册的 state,跳过继续处理下一个,避免整条 chain 断在中间
            while (pendingStateQueue.Count > 0)
            {
                var queued = pendingStateQueue.Dequeue();
                if (visitedThisTransition.Contains(queued))
                    continue;
                visitedThisTransition.Add(queued);
                ChangeStateInternal(queued);
            }
            return true;
        }

        #endregion
        
    
        /// <summary>
        /// 添加状态
        /// </summary>
        /// <param name="state"></param>
        /// <typeparam name="T"></typeparam>
        public FSMStateHandle<T> AddState<T>(FSMState<T> state) where T : FSMState<T>, new()
        {
            var type = typeof(T);
            if (state == null)
                return new FSMStateHandle<T>(this, null);
            if (states.TryGetValue(typeof(T), out var oldRegistered))
            {
                // 同一个实例重复 Add,直接返回
                if (ReferenceEquals(oldRegistered, state))
                    return new FSMStateHandle<T>(this, oldRegistered);
                // 不同实例:旧实例收 OnRemove,新实例接管
                oldRegistered.OnRemove(this);
                // 接口里 Control 是 read-only,通过 cast 调实现类的 internal setter
                if (oldRegistered is FSMState<T> oldState)
                    oldState.Control = null;
                state.Control = this;
                state.OnAdd(this);
                states[type] = state;
                return new FSMStateHandle<T>(this, state);
            }
            else
            {
                state.Control = this;
                state.OnAdd(this);
                states[type] = state;
                return new FSMStateHandle<T>(this, state);
            }
        }


        /// <summary>
        /// 移除State
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public FSMStateHandle<T> RemoveState<T>() where T : FSMState<T>, new()
        {
            var type = typeof(T);
            if (!states.TryGetValue(typeof(T), out var state))
                return new FSMStateHandle<T>(this, null);
            // 若是当前 state,先 OnExit 再清引用,避免 Update 空跑或下次 ChangeState 失败
            if (currentState == state)
            {
                currentState.OnExit(this);
                currentState = null;
            }
            states.Remove(type);
            state.OnRemove(this);
            return new FSMStateHandle<T>(this, state);
        }
        
    }
}