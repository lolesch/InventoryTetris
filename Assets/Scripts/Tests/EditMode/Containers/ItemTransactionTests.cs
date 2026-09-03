using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The <see cref="ItemTransaction"/> primitive (issue #9): a move mutates working
    /// copies, an observer sees nothing until commit, commit folds the working state back
    /// into the live dictionary instance and flushes queued effects once, and a dispose
    /// without a commit - an exception included - rolls everything back. Every assertion
    /// is on external state, not on how many times an event fired (except where "fires
    /// once" is itself the contract).
    /// </summary>
    [TestFixture]
    public sealed class ItemTransactionTests
    {
        private const string SwordId = "test.sword";
        private const string ArrowId = "test.arrow";
        private const string HelmId = "test.helm";

        [SetUp]
        public void SetCatalog() => ItemView.Catalog = new TestCatalog()
            .With(new TestDefinition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = ArrowId, Category = ItemCategory.Consumable, ConsumableType = ConsumableType.Arrow, Footprint = ItemSize.OneByOne, BaseStackLimit = 10u })
            .With(new TestDefinition { Id = HelmId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Helm, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u });

        [TearDown]
        public void ClearCatalog() => ItemView.Catalog = null;

        private static CharacterStatModifier Affix(StatName stat, float value) =>
            new(stat, new StatModifier(new Vector2Int(0, 100), value, StatModifierType.FlatAdd));

        private static ItemInstance Sword() => new(SwordId, ItemRarity.Rare, 7, new[] { Affix(StatName.PhysicalDamage, 6f) });
        private static ItemInstance Arrows() => new(ArrowId, ItemRarity.Common, 1, null);
        private static ItemInstance Helm(float armor) => new(HelmId, ItemRarity.Rare, 5, new[] { Affix(StatName.Armor, armor) });

        private static CharacterInventory Inventory(int width = 4, int height = 4) => new(new Vector2Int(width, height));

        // ── Working copy vs. live state ─────────────────────────────────────

        [Test]
        public void DuringATransaction_AMutationIsInvisibleThroughAReferenceHeldBeforeItOpened()
        {
            var inventory = Inventory();
            var liveReference = inventory.StoredPackages;

            using var transaction = new ItemTransaction(inventory);

            var package = new Package(inventory, Sword(), 1u);
            _ = inventory.TryAddToContainer(ref package);

            Assert.That(liveReference, Is.Empty, "the move mutates a working copy, not the live dictionary");
            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1), "the container itself sees its own working copy");
        }

        [Test]
        public void ACommittedMove_IsVisibleThroughAReferenceToStoredPackagesHeldBeforeTheTransactionOpened()
        {
            var inventory = Inventory();
            var liveReference = inventory.StoredPackages;

            using (var transaction = new ItemTransaction(inventory))
            {
                var package = new Package(inventory, Sword(), 1u);
                _ = inventory.TryAddToContainer(ref package);
                transaction.Commit();
            }

            Assert.That(liveReference, Has.Count.EqualTo(1), "commit wrote back into the same instance");
            Assert.That(inventory.StoredPackages, Is.SameAs(liveReference));
        }

        [Test]
        public void Commit_MakesTheWholeWorkingState_RemoveAndAddTogether_Real()
        {
            var inventory = Inventory();
            var sword = new Package(inventory, Sword(), 1u);
            _ = inventory.TryAddToContainer(ref sword);
            var swordPosition = inventory.StoredPackages.Keys.Single();

            using (var transaction = new ItemTransaction(inventory))
            {
                _ = inventory.RemoveAtPosition(swordPosition, inventory.StoredPackages[swordPosition]);
                var arrows = new Package(inventory, Arrows(), 6u);
                _ = inventory.TryAddToContainer(ref arrows);
                transaction.Commit();
            }

            Assert.That(inventory.StoredPackages.Values.Select(p => p.Item.DefinitionId), Is.EquivalentTo(new[] { ArrowId }));
            Assert.That(inventory.StoredPackages.Values.Single().Amount, Is.EqualTo(6u));
        }

        // ── Rollback ────────────────────────────────────────────────────────

        [Test]
        public void DisposeWithoutCommit_RestoresEveryContainerToItsSnapshot()
        {
            var inventory = Inventory();
            var equipment = new CharacterEquipment(new Vector2Int(14, 1));

            var original = new Package(inventory, Sword(), 1u);
            _ = inventory.TryAddToContainer(ref original);
            var originalPosition = inventory.StoredPackages.Keys.Single();

            using (new ItemTransaction(inventory, equipment))
            {
                _ = inventory.RemoveAtPosition(originalPosition, inventory.StoredPackages[originalPosition]);
                var extra = new Package(inventory, Arrows(), 5u);
                _ = inventory.TryAddToContainer(ref extra);

                var helm = new Package(inventory, Helm(3f), 1u);
                _ = equipment.TryAddToContainer(ref helm);
                // no Commit
            }

            Assert.That(inventory.StoredPackages.Keys, Is.EquivalentTo(new[] { originalPosition }));
            Assert.That(inventory.StoredPackages[originalPosition].Item.DefinitionId, Is.EqualTo(SwordId));
            Assert.That(equipment.StoredPackages, Is.Empty);
        }

        [Test]
        public void AnExceptionThrownMidMove_RollsTheWholeMoveBack()
        {
            var inventory = Inventory();
            var refreshes = 0;
            inventory.OnContentChanged += _ => refreshes++;

            Assert.That(() =>
            {
                using var transaction = new ItemTransaction(inventory);
                var package = new Package(inventory, Sword(), 1u);
                _ = inventory.TryAddToContainer(ref package);
                throw new InvalidOperationException("mid-move failure");
            }, Throws.InvalidOperationException);

            Assert.That(inventory.StoredPackages, Is.Empty);
            Assert.That(refreshes, Is.Zero, "a rolled-back move never fires OnContentChanged");
        }

        [Test]
        public void AMoveThatCannotReHomeADisplacedItem_LeavesEveryContainerTheCharacterSheetAndTheCursorUnchanged()
        {
            var stats = new FakeStatReceiver();
            var equipment = new CharacterEquipment(new Vector2Int(14, 1), stats);
            var inventory = Inventory(1, 1);

            var wornHelm = new Package(inventory, Helm(4f), 1u);
            _ = equipment.TryAddToContainer(ref wornHelm);
            stats.Added.Clear(); // the equip above is set-up, not part of the assertion window

            var filler = new Package(inventory, Arrows(), 1u);
            _ = inventory.TryAddToContainer(ref filler);

            var helmPosition = equipment.StoredPackages.Keys.Single();
            var storedHelm = equipment.StoredPackages[helmPosition];

            using (new ItemTransaction(equipment, inventory))
            {
                _ = equipment.RemoveAtPosition(helmPosition, storedHelm);

                // RemoveAtPosition returns the caller's package reduced by what it took, not
                // the item it took - the displaced item is rebuilt from the snapshot.
                var displaced = new Package(inventory, storedHelm.Item, storedHelm.Amount);
                var placed = inventory.TryAddToContainer(ref displaced);

                Assert.That(placed, Is.False, "the full inventory cannot take the displaced helm");
                // the caller sees an unplaced item, so it never commits
            }

            Assert.That(equipment.StoredPackages.Keys, Is.EquivalentTo(new[] { helmPosition }));
            Assert.That(equipment.StoredPackages[helmPosition].Item, Is.EqualTo(storedHelm.Item));
            Assert.That(inventory.StoredPackages.Values.Select(p => p.Item.DefinitionId), Is.EquivalentTo(new[] { ArrowId }));
            Assert.That(stats.Added, Is.Empty);
            Assert.That(stats.Removed, Is.Empty, "the unequip's stat lift was queued and dropped");
        }

        // ── OnContentChanged: deferred, then once per touched container ──────

        [Test]
        public void DuringATransaction_OnContentChangedDoesNotFire()
        {
            var inventory = Inventory();
            var refreshes = 0;
            inventory.OnContentChanged += _ => refreshes++;

            using var transaction = new ItemTransaction(inventory);

            var first = new Package(inventory, Sword(), 1u);
            _ = inventory.TryAddToContainer(ref first);
            var second = new Package(inventory, Arrows(), 2u);
            _ = inventory.TryAddToContainer(ref second);

            Assert.That(refreshes, Is.Zero);
        }

        [Test]
        public void ACommittedMove_FiresOnContentChangedOncePerAffectedContainer()
        {
            var inventory = Inventory();
            var equipment = new CharacterEquipment(new Vector2Int(14, 1));

            var inventoryRefreshes = 0;
            var equipmentRefreshes = 0;
            inventory.OnContentChanged += _ => inventoryRefreshes++;
            equipment.OnContentChanged += _ => equipmentRefreshes++;

            using (var transaction = new ItemTransaction(inventory, equipment))
            {
                var a = new Package(inventory, Sword(), 1u);
                _ = inventory.TryAddToContainer(ref a);
                var b = new Package(inventory, Arrows(), 3u);
                _ = inventory.TryAddToContainer(ref b);
                var c = new Package(inventory, Arrows(), 2u);
                _ = inventory.TryAddToContainer(ref c);
                transaction.Commit();
            }

            Assert.That(inventoryRefreshes, Is.EqualTo(1), "many mutations, one refresh");
            Assert.That(equipmentRefreshes, Is.Zero, "an untouched container does not refresh");
        }

        [Test]
        public void DisposeAfterCommit_IsANoOp()
        {
            var inventory = Inventory();
            var refreshes = 0;
            inventory.OnContentChanged += _ => refreshes++;

            using (var transaction = new ItemTransaction(inventory))
            {
                var package = new Package(inventory, Sword(), 1u);
                _ = inventory.TryAddToContainer(ref package);
                transaction.Commit();
            } // Dispose runs here

            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(refreshes, Is.EqualTo(1));
        }

        // ── Queued effects ─────────────────────────────────────────────────

        [Test]
        public void QueuedEffects_RunInOrder_OnlyAfterCommit()
        {
            var inventory = Inventory();
            var log = new List<int>();

            using (var transaction = new ItemTransaction(inventory))
            {
                transaction.QueueEffect(() => log.Add(1));
                transaction.QueueEffect(() => log.Add(2));
                transaction.QueueEffect(() => log.Add(3));

                Assert.That(log, Is.Empty, "nothing runs before commit");
                transaction.Commit();
            }

            Assert.That(log, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void QueuedEffects_AreDroppedOnRollback()
        {
            var inventory = Inventory();
            var ran = false;

            using (var transaction = new ItemTransaction(inventory))
                transaction.QueueEffect(() => ran = true); // no commit

            Assert.That(ran, Is.False);
        }

        [Test]
        public void EquippingInsideATransaction_AppliesAffixesThroughTheStatReceiverOnlyOnCommit()
        {
            var stats = new FakeStatReceiver();
            var equipment = new CharacterEquipment(new Vector2Int(14, 1), stats);
            var source = Inventory();

            using (var transaction = new ItemTransaction(equipment, source))
            {
                var helm = new Package(source, Helm(5f), 1u);
                _ = equipment.TryAddToContainer(ref helm);

                Assert.That(stats.Added, Is.Empty, "the stat apply is a commit-time effect");
                transaction.Commit();
            }

            Assert.That(stats.Added.Select(a => a.Stat), Is.EquivalentTo(new[] { StatName.Armor }));
        }

        [Test]
        public void EquippingInsideARolledBackTransaction_NeverAppliesAffixes()
        {
            var stats = new FakeStatReceiver();
            var equipment = new CharacterEquipment(new Vector2Int(14, 1), stats);
            var source = Inventory();

            using (new ItemTransaction(equipment, source))
            {
                var helm = new Package(source, Helm(5f), 1u);
                _ = equipment.TryAddToContainer(ref helm);
                // no commit
            }

            Assert.That(stats.Added, Is.Empty);
            Assert.That(equipment.StoredPackages, Is.Empty);
        }

        // ── The cursor as a one-capacity destination ────────────────────────

        [Test]
        public void CursorHolder_OnCommit_HandsTheHeldPackageToTheSink()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var inventory = Inventory(2, 2);
            var displaced = new Package(inventory, Sword(), 1u);

            using (var transaction = new ItemTransaction(cursor, inventory))
            {
                Assert.That(cursor.TryHold(displaced), Is.True);
                Assert.That(cursor.IsFree, Is.False);
                Assert.That(sink.Replaced, Is.Empty, "deferred to commit");
                transaction.Commit();
            }

            Assert.That(sink.Replaced.Single().Item.DefinitionId, Is.EqualTo(SwordId));
        }

        [Test]
        public void CursorHolder_OnRollback_NeverTouchesTheSink()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var inventory = Inventory(2, 2);

            using (new ItemTransaction(cursor, inventory))
                _ = cursor.TryHold(new Package(inventory, Sword(), 1u)); // no commit

            Assert.That(sink.Replaced, Is.Empty);
        }

        [Test]
        public void CursorHolder_IsOneCapacity()
        {
            var cursor = new CursorHolder(new FakeCursorSink());
            var inventory = Inventory(2, 2);

            Assert.That(cursor.TryHold(new Package(inventory, Sword(), 1u)), Is.True);
            Assert.That(cursor.TryHold(new Package(inventory, Arrows(), 1u)), Is.False);
            Assert.That(cursor.IsFree, Is.False);
        }

        [Test]
        public void CursorHolder_CommitWithNothingHeld_DoesNotCallTheSink()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var inventory = Inventory(2, 2);

            using (var transaction = new ItemTransaction(cursor, inventory))
            {
                var package = new Package(inventory, Sword(), 1u);
                _ = inventory.TryAddToContainer(ref package);
                transaction.Commit();
            }

            Assert.That(sink.Replaced, Is.Empty);
        }

        // ── The re-home cascade (issue #10) ────────────────────────────────

        [Test]
        public void TryReHomeToHandOrContainer_PutsTheDisplacedItemOnTheFreedCursorFirst()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var origin = Inventory();
            var inventory = Inventory();
            var displaced = new Package(origin, Sword(), 1u);

            using (var transaction = new ItemTransaction(cursor, origin, inventory).ReHomeThrough(origin, inventory))
            {
                Assert.That(transaction.TryReHomeToHandOrContainer(ref displaced), Is.True);
                Assert.That(displaced.IsValid, Is.False, "the item is fully placed - on the cursor");
                transaction.Commit();
            }

            Assert.That(sink.Replaced.Single().Item.DefinitionId, Is.EqualTo(SwordId));
            Assert.That(origin.StoredPackages, Is.Empty);
            Assert.That(inventory.StoredPackages, Is.Empty);
        }

        [Test]
        public void TryReHomeToHandOrContainer_WhenTheCursorIsAlreadyHolding_FallsThroughToTheFirstReHomeContainer()
        {
            var cursor = new CursorHolder(new FakeCursorSink());
            var origin = Inventory();
            var inventory = Inventory();

            using var transaction = new ItemTransaction(cursor, origin, inventory).ReHomeThrough(origin, inventory);

            var first = new Package(origin, Sword(), 1u);
            _ = transaction.TryReHomeToHandOrContainer(ref first); // takes the cursor

            var second = new Package(origin, Helm(2f), 1u);
            Assert.That(transaction.TryReHomeToHandOrContainer(ref second), Is.True);
            Assert.That(second.IsValid, Is.False);
            Assert.That(origin.StoredPackages.Values.Single().Item.DefinitionId, Is.EqualTo(HelmId),
                "the second displaced item landed in the origin container");
        }

        [Test]
        public void TryReHomeToHandOrContainer_WhenNothingCanTakeIt_AbortsTheTransactionAndCommitRollsBack()
        {
            var cursor = new CursorHolder(new FakeCursorSink());
            var origin = Inventory(1, 1);
            var inventory = Inventory(1, 1);

            // Fill both re-home containers so the second displaced item has nowhere to go.
            var originFiller = new Package(origin, Arrows(), 1u);
            _ = origin.TryAddToContainer(ref originFiller);
            var inventoryFiller = new Package(inventory, Arrows(), 1u);
            _ = inventory.TryAddToContainer(ref inventoryFiller);

            using (var transaction = new ItemTransaction(cursor, origin, inventory).ReHomeThrough(origin, inventory))
            {
                var first = new Package(origin, Sword(), 1u);
                _ = transaction.TryReHomeToHandOrContainer(ref first); // cursor

                var second = new Package(origin, Helm(2f), 1u);
                Assert.That(transaction.TryReHomeToHandOrContainer(ref second), Is.False);
                Assert.That(transaction.Aborted, Is.True);

                transaction.Commit(); // aborted -> rolls back
            }

            Assert.That(origin.StoredPackages.Values.Select(p => p.Item.DefinitionId), Is.EquivalentTo(new[] { ArrowId }));
            Assert.That(inventory.StoredPackages.Values.Select(p => p.Item.DefinitionId), Is.EquivalentTo(new[] { ArrowId }));
        }

        [Test]
        public void TryReHomeToContainer_NeverUsesTheCursor_AndAbortsWhenNoContainerHasRoom()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var origin = Inventory(1, 1);

            var filler = new Package(origin, Arrows(), 1u);
            _ = origin.TryAddToContainer(ref filler);

            using (var transaction = new ItemTransaction(cursor, origin).ReHomeThrough(origin))
            {
                var displaced = new Package(origin, Sword(), 1u);
                Assert.That(transaction.TryReHomeToContainer(ref displaced), Is.False, "the container is full - the cursor is not a fallback");
                Assert.That(transaction.Aborted, Is.True);
                transaction.Commit(); // aborted -> rolls back
            }

            Assert.That(sink.Replaced, Is.Empty, "the freed cursor was never offered the item");
            Assert.That(origin.StoredPackages.Values.Select(p => p.Item.DefinitionId), Is.EquivalentTo(new[] { ArrowId }));
        }

        [Test]
        public void TryReHomeToContainerOrHand_PrefersTheContainer_ThenFallsBackToTheHand()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var origin = Inventory(1, 1);

            using (var transaction = new ItemTransaction(cursor, origin).ReHomeThrough(origin))
            {
                var first = new Package(origin, Sword(), 1u);
                Assert.That(transaction.TryReHomeToContainerOrHand(ref first), Is.True);
                Assert.That(origin.StoredPackages.Values.Single().Item.DefinitionId, Is.EqualTo(SwordId), "the first went into the container");

                var second = new Package(origin, Helm(2f), 1u);
                Assert.That(transaction.TryReHomeToContainerOrHand(ref second), Is.True);
                Assert.That(second.IsValid, Is.False, "the container is now full - the second went to the hand");

                transaction.Commit();
            }

            Assert.That(sink.Replaced.Single().Item.DefinitionId, Is.EqualTo(HelmId));
        }

        [Test]
        public void ReHomeThrough_AContainerThatWasNeverEnrolled_Throws()
        {
            var enrolled = Inventory();
            var stranger = Inventory();

            using var transaction = new ItemTransaction(enrolled);

            Assert.That(() => transaction.ReHomeThrough(stranger), Throws.InvalidOperationException);
        }

        // ── Enrolment guards ───────────────────────────────────────────────

        [Test]
        public void EnrollingAContainerAlreadyInAnotherTransaction_Throws()
        {
            var inventory = Inventory();

            using var first = new ItemTransaction(inventory);

            Assert.That(() => new ItemTransaction(inventory), Throws.InvalidOperationException);
        }

        [Test]
        public void AFailedEnrolment_LeavesTheOtherNamedContainersUntouched()
        {
            var free = Inventory();
            var taken = Inventory();

            using var holder = new ItemTransaction(taken);

            Assert.That(() => new ItemTransaction(free, taken), Throws.InvalidOperationException);

            // `free` was un-enrolled when the constructor threw: it can join a new transaction.
            using (var transaction = new ItemTransaction(free))
            {
                var package = new Package(free, Sword(), 1u);
                _ = free.TryAddToContainer(ref package);
                transaction.Commit();
            }

            Assert.That(free.StoredPackages, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheSameContainerNamedTwice_IsEnrolledOnce()
        {
            var inventory = Inventory();

            Assert.That(() =>
            {
                using var transaction = new ItemTransaction(inventory, inventory);
                var package = new Package(inventory, Sword(), 1u);
                _ = inventory.TryAddToContainer(ref package);
                transaction.Commit();
            }, Throws.Nothing);

            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheContainerAssembly_NamesNoProvider()
        {
            // The provider singletons live in the predefined Assembly-CSharp, which an
            // asmdef cannot reference - so this holds by construction. The test pins it
            // against a future edit that introduces a bridge assembly with "Provider" in
            // its name.
            var referenced = typeof(ItemTransaction).Assembly
                .GetReferencedAssemblies()
                .Select(name => name.Name)
                .ToArray();

            Assert.That(referenced.Any(name => name.Contains("Provider")), Is.False, string.Join(", ", referenced));
            Assert.That(referenced, Has.None.EqualTo("Assembly-CSharp"));
        }
    }
}
