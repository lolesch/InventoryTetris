using System.Collections.Generic;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [System.Serializable]
    internal sealed class EquipmentContainerDisplay : AbstractContainerDisplay
    {
        protected override void SetupSlotDisplays()
        {
            for (var i = 0; i < containerSlotDisplays.Count; i++)
                containerSlotDisplays[i].SetupSlot(this, Container, new(i, 0));
            
            if (containerSlotDisplays.Count != Container.Capacity)
                Debug.LogError($"equipmentSlotDisplays {containerSlotDisplays.Count} of {Container.Capacity}");
        }

        protected override void Refresh(Dictionary<Vector2Int, Package> storedPackages)
        {
            var current = 0;
            var offHandGhosted = false;
            for (var x = 0; x < Container?.Dimensions.x; x++)
                for (var y = 0; y < Container?.Dimensions.y; y++)
                {
                    _ = storedPackages.TryGetValue(new(x, y), out var package);

                    // The off-hand already shows the ghosted 2H weapon; refreshing it from its own
                    // (empty) package would wipe the ghost.
                    if (current == CharacterEquipment.OffHandSlot && offHandGhosted)
                    {
                        current++;
                        continue;
                    }

                    containerSlotDisplays[current].RefreshSlotDisplay(package);

                    // Hacking in the preview of 2H in offhand slot
                    if (current == CharacterEquipment.MainHandSlot && package.Item != null && CharacterEquipment.IsTwoHandedWeapon(ItemService.Instance.View(package.Item).Definition.EquipmentType))
                    {
                        (containerSlotDisplays[CharacterEquipment.OffHandSlot] as EquipmentSlotDisplay).Refresh2HandSlotDisplay(package);
                        offHandGhosted = true;
                    }

                    current++;
                }
        }
    }
}
