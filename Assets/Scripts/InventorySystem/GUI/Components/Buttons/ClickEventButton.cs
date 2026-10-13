using Submodules.Utility.UI;
using System;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    /// <summary>A button that only reports its click, for a panel that owns what the click means.</summary>
    public sealed class ClickEventButton : AbstractButton
    {
        public event Action Clicked;

        protected override void OnClick() => Clicked?.Invoke();
    }
}
