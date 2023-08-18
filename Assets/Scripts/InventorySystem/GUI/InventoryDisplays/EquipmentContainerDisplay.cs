﻿using ToolSmiths.InventorySystem.Inventories;
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
                var position = InventoryProvider.Instance.PlayerEquipment.GetEquipmentTypePosition((containerSlotDisplays[i] as EquipmentSlotDisplay).allowedEquipmentTypes[0]);
                containerSlotDisplays[i].SetupSlot(Container, position);
            }

            if (containerSlotDisplays.Count != Container.Capacity)
                Debug.LogError($"equipmentSlotDisplays {containerSlotDisplays.Count} of {Container.Capacity}");
        }
    }
}