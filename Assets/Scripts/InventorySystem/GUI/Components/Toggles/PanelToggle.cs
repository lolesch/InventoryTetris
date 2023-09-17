using ToolSmiths.InventorySystem.GUI.Panels;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    public class PanelToggle : AbstractToggle
    {
        [SerializeField] protected AbstractPanel panel;

        public override void SetToggle(bool isOn)
        {
            base.SetToggle(isOn);

            if (panel)
                if (isOn)
                    panel.FadeIn();
                else
                    panel.FadeOut();
        }
    }
}
