using UnityEngine.InputSystem;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Whether the tab peek key counts as held: <see cref="GameAction.PeekTab"/>, unless it went down as the
    /// operating system's window switch, which is the physical Tab with a physical Alt already down. That is a
    /// rule about the keys, not the actions: rebinding <see cref="GameAction.ShowDetails"/> or the peek moves
    /// neither the chord nor what must not flick a tab. Alt then Tab is blocked; Tab then Alt keeps the peek,
    /// so an item's details can be read on the peeked tab. A blocked press stays blocked until the peek is let go.
    /// </summary>
    public sealed class PeekTabKey
    {
        private enum Phase { Up, Peeking, Blocked }

        private Phase phase;

        /// <summary>Read once a frame: the answer depends on which key went down first.</summary>
        public bool IsHeld()
        {
            if (!GameInput.IsHeld(GameAction.PeekTab))
                phase = Phase.Up;
            else if (phase == Phase.Up)
                phase = IsWindowSwitch ? Phase.Blocked : Phase.Peeking;

            return phase == Phase.Peeking;
        }

        private static bool IsWindowSwitch =>
            GameInput.IsKeyHeld(Key.Tab) && (GameInput.IsKeyHeld(Key.LeftAlt) || GameInput.IsKeyHeld(Key.RightAlt));
    }
}
