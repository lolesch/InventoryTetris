using NUnit.Framework;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// <see cref="ContainerRole"/> is serialized by value in every scene and prefab that holds a
    /// container display (issue #127): inserting or deleting a member shifts every later one and
    /// silently rebinds those displays to the wrong container. The values are pinned here so
    /// such an edit fails a test instead of a scene.
    /// </summary>
    [TestFixture]
    public sealed class ContainerRoleTests
    {
        [Test]
        public void TheSerializedValuesAreStable()
        {
            Assert.AreEqual(0, (int)ContainerRole.Unassigned);
            Assert.AreEqual(1, (int)ContainerRole.Equipment);
            Assert.AreEqual(2, (int)ContainerRole.Inventory);
            Assert.AreEqual(3, (int)ContainerRole.Stash);
            Assert.AreEqual(4, (int)ContainerRole.Store);
            Assert.AreEqual(5, (int)ContainerRole.Basket);
            Assert.AreEqual(6, (int)ContainerRole.HealerSupply);
        }

        [Test]
        public void SoldIsAppendedAfterTheHealerSupply() =>
            Assert.AreEqual((int)ContainerRole.HealerSupply + 1, (int)ContainerRole.Sold);
    }
}
