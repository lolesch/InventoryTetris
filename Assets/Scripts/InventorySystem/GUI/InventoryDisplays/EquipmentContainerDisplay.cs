using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [System.Serializable]
    public class EquipmentContainerDisplay : AbstractContainerDisplay
    {
        protected override void SetupSlotDisplays()
        {
            for (var i = 0; i < containerSlotDisplays.Count; i++)
            {
                //var position = InventoryProvider.Instance.PlayerEquipment.GetTypeSpecificPositions((containerSlotDisplays[i] as EquipmentSlotDisplay).allowedEquipmentTypes[0]);
                containerSlotDisplays[i].SetupSlot(Container, new(i, 0));
            }

            if (containerSlotDisplays.Count != Container.Capacity)
                Debug.LogError($"equipmentSlotDisplays {containerSlotDisplays.Count} of {Container.Capacity}");
        }

        protected override void Refresh(Dictionary<Vector2Int, Package> storedPackages)
        {
            var current = 0;
            for (var x = 0; x < Container?.Dimensions.x; x++)
                for (var y = 0; y < Container?.Dimensions.y; y++)
                {
                    _ = storedPackages.TryGetValue(new(x, y), out var package);

                    containerSlotDisplays[current].RefreshSlotDisplay(package);

                    // Hacking in the preview of 2H in offhand slot
                    if (current == 12 && package.Item != null && CharacterEquipment.IsTwoHandedWeapon((package.Item as EquipmentItem).EquipmentType))
                    {
                        (containerSlotDisplays[13] as EquipmentSlotDisplay).Refresh2HandSlotDisplay(package);
                        return;
                    }

                    current++;
                }

            //Icon.color = InventoryProvider.Instance.ContainerToAddTo == Container
            //    ? new Color(1, .84f, 0, 1)
            //    : Color.white;
        }
    }
}