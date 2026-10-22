using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>The selling panels' tab pair, peeked past while <see cref="PeekTabKey"/> is held. Not <see cref="GameAction.ShowDetails"/>:
    /// that one expands the tooltip of the item under the pointer, which the peek would swap away.</summary>
    public sealed class SellingPanelPeekToggle : PanelPeekToggle
    {
        private readonly PeekTabKey key = new();

        protected override bool PeekKeyPressed => key.IsHeld();
    }
}
