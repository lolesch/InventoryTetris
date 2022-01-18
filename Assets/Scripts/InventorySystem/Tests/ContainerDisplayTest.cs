using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Displays;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests
{
    public class ContainerDisplayTest
    {
        protected static void Release(Object obj) => Object.DestroyImmediate(obj);

        private static void SetupDisplay(out InventoryDisplay display, out Inventory container, out Package item, int dimensionX = 5, int dimensionY = 3)
        {
            var inventoryDisplay = new GameObject().AddComponent<InventoryDisplay>();
            display = inventoryDisplay;
            container = new Inventory(new(dimensionX, dimensionY));
            item = new Package(ScriptableObject.CreateInstance<Item>(), 1u);
        }

        //assemble, act, assert, release

        [Test]
        public void ShouldRecieveAnInventoryToDisplay()
        {
            SetupDisplay(out InventoryDisplay display, out Inventory container, out Package item);
            display.SetInventory(container);

            Assert.AreEqual(container, display.Container);
        }

        // [Test]
        // public void ShouldHaveSlotsEqualToTheContainersDimensions()
        // {
        //     SetupDisplay(out InventoryDisplay display, out Inventory container, out Package item);
        //     display.SetInventory(container);
        // 
        //     var slotDisplay = new GameObject().AddComponent<InventorySlotDisplay>();
        //     display.InstantiateNewSlots(slotDisplay);
        // 
        //     Assert.AreEqual(container.Capacity, display.slotDisplays.Count);
        // 
        //     foreach (var slot in display.slotDisplays.Values)
        //         Release(slot.gameObject);
        //     Release(item.Item);
        // }

        // TODO: 
        // ShouldContainACollectionOfSlots
        // Should adjustSlotsToTheCurrentDimensions
        // ShouldRefreshWhenNeeded
        // Should
    }
}
