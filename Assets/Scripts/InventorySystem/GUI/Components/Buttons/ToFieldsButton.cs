using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    [DisallowMultipleComponent]
    public sealed class ToFieldsButton : PanelButton
    {
        protected override void OnClick()
        {
            base.OnClick();
            InventoryService.Instance.SyncContextToPhase(true);
        }
    }
}
