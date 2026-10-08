using Submodules.Utility.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>The selling panels' tab pair, peeked past with Alt: the same key the tooltip reads for
    /// "show me more", through the same helper, so the two agree on what Alt is.</summary>
    public sealed class AltPanelPeekToggle : PanelPeekToggle
    {
        protected override bool PeekKeyPressed => ModifierKeys.Alt;
    }
}
