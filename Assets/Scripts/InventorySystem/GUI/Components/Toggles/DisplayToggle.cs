using ToolSmiths.InventorySystem.GUI.Panels;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    public class DisplayToggle : AbstractToggle
    {
        [SerializeField] protected AbstractPanel display;

        public override void SetToggle(bool isOn)
        {
            base.SetToggle(isOn);

            if (display)
                if (isOn)
                    display.FadeIn();
                else
                    display.FadeOut();
        }
    }
}
