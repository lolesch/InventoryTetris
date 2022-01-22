using NUnit.Framework;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests
{
    public class ItemTests
    {
        static void Release(Object obj) => Object.DestroyImmediate(obj);

        [Test]
        public void CanSetItemStackLimit([Values(0u, 1u, 99u)] uint amount)
        {
            Item item = ScriptableObject.CreateInstance<Item>();

            item.SetStackLimit(amount);

            Assert.AreEqual(amount, item.StackLimit);

            Release(item);
        }
    }
}
