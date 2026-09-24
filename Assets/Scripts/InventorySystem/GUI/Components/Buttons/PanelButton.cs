using Submodules.Utility.UI;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    public class PanelButton : AbstractButton
    {
        [SerializeField] protected SimplePanel panel;
        protected override void OnClick() => panel.Toggle(true);
    }
}