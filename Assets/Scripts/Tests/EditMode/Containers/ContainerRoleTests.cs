using System.Collections.Generic;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

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
            Assert.AreEqual(5, (int)ContainerRole.HealerSupply);
            Assert.AreEqual(6, (int)ContainerRole.Sold);
        }

        // ── Each role resolves to its container (the provider binds displays through this) ──

        private static readonly IItemCatalog catalog = new TestCatalog();

        private AbstractDimensionalContainer NewContainer() => new CharacterInventory(new Vector2Int(2, 2), catalog);

        [TestCase(ContainerRole.Equipment)]
        [TestCase(ContainerRole.Inventory)]
        [TestCase(ContainerRole.Stash)]
        [TestCase(ContainerRole.Store)]
        [TestCase(ContainerRole.HealerSupply)]
        [TestCase(ContainerRole.Sold)]
        public void EveryRoleResolvesToItsOwnContainer(ContainerRole role)
        {
            var all = new Dictionary<ContainerRole, AbstractDimensionalContainer>();
            foreach (var r in new[] { ContainerRole.Equipment, ContainerRole.Inventory, ContainerRole.Stash,
                                      ContainerRole.Store, ContainerRole.HealerSupply, ContainerRole.Sold })
                all[r] = NewContainer();

            var resolved = ContainerRoleResolver.Resolve(role, all[ContainerRole.Equipment], all[ContainerRole.Inventory],
                all[ContainerRole.Stash], all[ContainerRole.Store], all[ContainerRole.HealerSupply],
                all[ContainerRole.Sold]);

            Assert.That(resolved, Is.SameAs(all[role]));
        }

        [Test]
        public void AnUnassignedRoleResolvesToNothing()
        {
            var c = NewContainer();

            Assert.That(ContainerRoleResolver.Resolve(ContainerRole.Unassigned, c, c, c, c, c, c), Is.Null);
        }
    }
}
