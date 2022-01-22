using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    public class EquipmentContainerDisplay : AbstractContainerDisplay
    {
        protected override void SetupSlotDisplays(AbstractDimensionalContainer container)
        {
            foreach (var slotDisplay in containerSlotDisplays)
                slotDisplay.container = Container;
        }
    }
}