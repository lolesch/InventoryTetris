using TeppichsTools.Logging;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    public class EquipmentContainerDisplay : AbstractContainerDisplay
    {
        protected override void SetupSlotDisplays()
        {
            for (var i = 0; i < containerSlotDisplays.Count; i++)
            {
                var position = InventoryProvider.Instance.PlayerEquipment.GetEquipmentTypePosition((containerSlotDisplays[i] as EquipmentSlotDisplay).allowedEquipmentTypes[0]);
                containerSlotDisplays[i].SetupSlot(Container, position);
            }

            if (containerSlotDisplays.Count != Container.Capacity)
                EditorDebug.LogError($"equipmentSlotDisplays {containerSlotDisplays.Count} of {Container.Capacity}");
        }
    }
}