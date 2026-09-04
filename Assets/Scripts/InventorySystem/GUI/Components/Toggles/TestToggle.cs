using Submodules.Utility.Extensions;
using Submodules.Utility.UI;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    public class TestToggle : AbstractToggle
    {
        public override void SetToggle(bool isOn)
        {
            base.SetToggle(isOn);

            Debug.Log($"TOGGLE:\t{name.ColoredComponent()} was toggled {(isOn ? "on" : "off").Colored(ColorExtensions.Orange)}", this);
        }
    }
}
