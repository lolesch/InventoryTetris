using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    public class TestButton : AbstractButton
    {
        protected override void OnClick() => Debug.Log($"BUTTON:\t{name.ColoredComponent()} was {"clicked".Colored(ColorExtensions.Orange)}", this);
    }
}
