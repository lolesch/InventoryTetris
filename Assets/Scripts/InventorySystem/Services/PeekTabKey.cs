namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Whether the tab peek key counts as held: <see cref="GameAction.PeekTab"/>, unless
    /// <see cref="GameAction.ShowDetails"/> was already down when it went down. Alt then Tab is the OS window
    /// switch and must not flick a tab; Tab then Alt keeps the peek, so an item's details can be read on the
    /// peeked tab. A blocked press stays blocked until Tab is let go.
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
                phase = GameInput.IsHeld(GameAction.ShowDetails) ? Phase.Blocked : Phase.Peeking;

            return phase == Phase.Peeking;
        }
    }
}
