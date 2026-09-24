using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    [DisallowMultipleComponent]
    public sealed class ToFieldsButton : PanelButton
    {
        protected override void OnClick()
        {
            base.OnClick();
            InventoryProvider.Instance.SyncContextToPhase(true);
        }
    }
}
