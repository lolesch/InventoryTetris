using TeppichsTools.Logging;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    public class EquipmentContainerDisplay : AbstractContainerDisplay
    {
        protected override void SetupSlotDisplays()
        {
            foreach (var slotDisplay in containerSlotDisplays)
                slotDisplay.container = Container;

            if (containerSlotDisplays.Count != Container.Capacity)
                EditorDebug.LogError($"equipmentSlotDisplays {containerSlotDisplays.Count} of {Container.Capacity}");
        }
    }
}