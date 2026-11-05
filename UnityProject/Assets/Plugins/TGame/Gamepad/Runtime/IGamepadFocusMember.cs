namespace TGame.Gamepad
{
    /// <summary>
    /// 可注册到 <see cref="GamepadFocus"/> 的成员抽象。
    /// GamepadFocus 只依赖该接口，不直接依赖具体的目标或列表实现。
    /// </summary>
    public interface IGamepadFocusMember
    {
        /// <summary>作为可导航目标时返回自身，否则返回 null。</summary>
        GamepadItem NavigableItem { get; }

        /// <summary>聚焦区退出时回调（例如让列表停用自身）。</summary>
        void OnFocusExit();
    }
}
