using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests
{
    public class ItemTests
    {
        private static void Release(Object obj) => Object.DestroyImmediate(obj);

        [Test]
        public void CanSetItemStackLimit([Values(ItemStackType.NONE, ItemStackType.Single, ItemStackType.StackOfTen, ItemStackType.StackOfHundred)] ItemStackType amount)
        {
            var item = ScriptableObject.CreateInstance<Item>();

            item.SetStackLimit(amount);

            Assert.AreEqual(amount, item.StackLimit);

            Release(item);
        }
    }
}
