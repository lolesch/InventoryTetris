using System.Collections.Generic;
using ToolSmiths.InventorySystem.GUI.Panels;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    public class MultiplePanelToggle : AbstractToggle
    {
        [SerializeField] protected List<AbstractPanel> panelsToTurnOn;
        [SerializeField] protected List<AbstractPanel> panelsToTurnOff;

        public override void SetToggle(bool isOn)
        {
            base.SetToggle(isOn);

            foreach (var panel in panelsToTurnOn)
                if (isOn)
                    panel.FadeIn();
                else
                    panel.FadeOut();

            foreach (var panel in panelsToTurnOff)
                if (isOn)
                    panel.FadeOut();
                else
                    panel.FadeIn();
        }
    }
}
