using UnityEngine;

namespace TGame.Gamepad
{
    /// <summary>
    /// UI 手柄游标抽象。实现方决定游标如何移动与表现。
    /// 管理器只负责在选中目标变化时调用 Show/Hide/Move。
    /// </summary>
    public interface IGamepadCursor
    {
        void Show();
        void Hide();
        /// <summary>移动到目标；游标自行决定跟随、包围或其它表现。</summary>
        void Move(GameObject target);
    }
}
