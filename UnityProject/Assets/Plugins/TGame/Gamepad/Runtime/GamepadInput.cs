using System;
using System.Threading;
using UnityEngine;

namespace TGame.Gamepad
{
    public enum GamepadPhase { Pressed, Repeated, Released, Canceled }

    public readonly struct GamepadInput
    {
        public readonly string Operation;
        public readonly GamepadPhase Phase;
        public readonly Vector2 Value;

        public GamepadInput(string operation, GamepadPhase phase, Vector2 value = default)
        {
            Operation = operation;
            Phase = phase;
            Value = value;
        }
    }

    public static class GamepadCommands
    {
        public const string Navigate = "Navigate";
        public const string Submit = "Submit";
        public const string Cancel = "Cancel";
        public const string Previous = "Previous";
        public const string Next = "Next";
        public const string Scroll = "Scroll";
    }

    public enum GamepadListStatus { Found, Boundary, Failed }
    public enum GamepadListView { Visible, Margin, Center }

    public readonly struct GamepadListResult
    {
        public readonly GamepadListStatus Status;
        public readonly string Key;

        public GamepadListResult(GamepadListStatus status, string key = null)
        {
            Status = status;
            Key = key;
        }
    }

    // Implement outside this module when integrating a third-party list.
    // All methods and callbacks run on Unity's main thread. Observe cancellation
    // before changing scroll position or recycling a view for an obsolete request.
    public interface IGamepadList
    {
        event Action Changed;
        bool IsValid(string key);
        bool TryGetInitial(out string key);
        bool TryGetItem(string key, out GamepadItem item);
        void FindNeighbor(string key, Vector2 direction, CancellationToken cancellation,
            Action<GamepadListResult> completed);
        void Show(string key, GamepadListView view, CancellationToken cancellation,
            Action<bool> completed);
    }
}
