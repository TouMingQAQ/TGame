using System;

namespace TGame.FSM
{
    public interface IFSMState
    {
        public IFSMControl Control { get; }

        public void OnAdd(IFSMControl control);

        public void OnRemove(IFSMControl control);

        public void OnEnter(IFSMControl control);

        public void OnTick(IFSMControl control, float deltaTime);

        public void OnExit(IFSMControl control);

    }

    internal interface IFSMStateControl
    {
        void ClearControl();
    }

    /// <summary>
    /// FSM基础状态
    /// </summary>
    public class FSMState<T> : IFSMState, IFSMStateControl where T : FSMState<T>, new()
    {
        public IFSMControl Control { get; internal set; }

        public virtual void OnAdd(IFSMControl control) {}

        public virtual void OnRemove(IFSMControl control) {}

        public virtual void OnEnter(IFSMControl control) { }

        public virtual void OnTick(IFSMControl control,float deltaTime) { }

        public virtual void OnExit(IFSMControl control) { }

        protected bool ChangeState<TState>() where TState : FSMState<TState>, new()
        {
            return Control != null && Control.ChangeState<TState>();
        }

        void IFSMStateControl.ClearControl()
        {
            Control = null;
        }
    }
}
