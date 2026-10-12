using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>The selling panels' tab pair, peeked past while <see cref="GameAction.Peek"/> is held: the same action the tooltip reads for
    /// "show me more", so the two agree on what peeking is.</summary>
    public sealed class AltPanelPeekToggle : PanelPeekToggle
    {
        protected override bool PeekKeyPressed => GameInput.IsHeld(GameAction.Peek);
    }
}
