using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Inventories
{
    /// <summary>
    /// The container core (<see cref="AbstractDimensionalContainer"/> and subclasses,
    /// <see cref="Package"/>, <see cref="AbstractItem"/>) still lives in the predefined
    /// <c>Assembly-CSharp</c>, which an asmdef test assembly cannot reference. This file
    /// sits in a plain <c>Editor/</c> folder with no asmdef, so it compiles into the
    /// predefined <c>Assembly-CSharp-Editor</c> - which auto-references
    /// <c>Assembly-CSharp</c> and is scanned by the EditMode Test Runner. It is the
    /// Phase 0 test seam (issue #4): reachable with zero extraction and zero behaviour
    /// change. When <c>InventorySystem.Containers</c> is extracted (ADR-0007), these
    /// move into an <c>InventorySystem.Containers.Tests</c> asmdef.
    /// </summary>
    [TestFixture]
    public sealed class ContainerCoreTests
    {
        /// <summary>
        /// <see cref="AbstractItem"/> has only field initializers on its own construction
        /// path - no singleton (only the concrete subtypes reach one). The footprint /
        /// stack / rarity setters are <c>protected</c>, so a test double is an empty ctor
        /// plus the abstract <see cref="object.ToString"/> override.
        /// </summary>
        private sealed class FakeItem : AbstractItem
        {
            public FakeItem(uint stackLimit = 1u) => StackLimit = stackLimit;
            public override string ToString() => "fake";
        }

        [Test]
        public void CharacterInventory_NewlyConstructed_IsEmptyWithTheGivenCapacity()
        {
            var inventory = new CharacterInventory(new Vector2Int(4, 4));

            Assert.That(inventory.StoredPackages, Is.Empty);
            Assert.That(inventory.Capacity, Is.EqualTo(16));
        }

        [Test]
        public void CharacterInventory_AfterAddingAPackage_HoldsExactlyThatItem()
        {
            var inventory = new CharacterInventory(new Vector2Int(4, 4));
            var package = new Package(inventory, new FakeItem(stackLimit: 1u), 1u);

            var accepted = inventory.TryAddToContainer(ref package);

            Assert.That(accepted, Is.True);
            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(package.Amount, Is.EqualTo(0u));
        }
    }
}
