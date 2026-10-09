using NUnit.Framework;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The quick-move table (issue #86, replacing #30's hand-rolled matrix): shift-click
    /// routes an item to wherever the open context says. A pure resolver maps every (source
    /// container, Inventory Context) pair to one intent - do nothing, move to a named
    /// container, acquire into the hub (honouring auto-equip), sell, or
    /// buy - so the three slot displays that used to hand-roll a move each now ask it.
    /// This fixture wires the Stash rows, the "no context" rows, and the Vendor rows
    /// (issue #33): with the Vendor open, backpack and equipment shift-clicks sell the
    /// item, while the vendor shelf's own shift-click stays a buy in every context.
    ///
    /// <para>The context replaced <c>SidePanelContext</c> in #85, so the rows are keyed on the
    /// Inventory Context now. Its two new members resolve to nothing on purpose: the Hero
    /// Panel's sink would be Equipment, but that row duplicates right-click and has no ticket,
    /// and the Hero context has no Town Stop, so it has no sink at all. It is asserted here
    /// rather than left implicit, because "a Quick Move with only the Hero Panel open does
    /// nothing" is a stated outcome of #85, not an oversight. The one exception is #63's
    /// ground row: in a Run, a backpack shift-click drops to the ground. The Healer gained the
    /// Vendor's sale rows in #121.</para>
    /// </summary>
    [TestFixture]
    public sealed class QuickMoveResolverTests
    {
        // The resolver compares sources by MonoBehaviour reference identity (a container is
        // a UnityEngine.Object); distinct instances are enough - nothing here needs items.
        // Routing only - no item is ever resolved - so an empty catalog is enough.
        private static readonly IItemCatalog catalog = new TestCatalog();

        private readonly AbstractDimensionalContainer backpack = new CharacterInventory(new Vector2Int(4, 4), catalog);
        private readonly AbstractDimensionalContainer stash = new CharacterInventory(new Vector2Int(4, 4), catalog);
        private readonly AbstractDimensionalContainer equipment = new CharacterEquipment(new Vector2Int(14, 1), catalog, null);
        private readonly AbstractDimensionalContainer store = new CharacterInventory(new Vector2Int(4, 4), catalog);

        private readonly AbstractDimensionalContainer healerSupply = new CharacterInventory(new Vector2Int(4, 4), catalog);
        private readonly AbstractDimensionalContainer sold = new SoldContainer(new Vector2Int(4, 4), catalog);
        private readonly AbstractDimensionalContainer ground = new GroundContainer(new Vector2Int(4, 4), catalog);

        private QuickMoveIntent Resolve(InventoryContext context, AbstractDimensionalContainer source,
            AbstractDimensionalContainer backpack, AbstractDimensionalContainer stash,
            AbstractDimensionalContainer equipment, AbstractDimensionalContainer store, bool groundOpen = false)
            => QuickMoveResolver.Resolve(context, source, backpack, stash, equipment, store, healerSupply, sold, ground, groundOpen);

        // ── Stash open: backpack ↔ Stash, equipment → Stash (byte-for-byte as today) ──

        [Test]
        public void StashContext_Backpack_SendsTheItemToTheStash()
        {
            var intent = Resolve(InventoryContext.Stash, backpack, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(stash));
        }

        [Test]
        public void StashContext_Stash_AcquiresTheItemIntoTheBackpack()
        {
            // #86: retrieval from the Stash routes through the acquisition entry point
            // (auto-equip), not a plain move - so this carries no Target.
            var intent = Resolve(InventoryContext.Stash, stash, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.Acquire));
            Assert.That(intent.Target, Is.Null);
        }

        [Test]
        public void StashContext_Equipment_SendsTheItemToTheStash()
        {
            var intent = Resolve(InventoryContext.Stash, equipment, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(stash));
        }

        // ── No context open: shift-click does nothing ──

        [TestCase(nameof(backpack))]
        [TestCase(nameof(stash))]
        [TestCase(nameof(equipment))]
        public void NoneContext_PlayerContainer_DoesNothing(string sourceName)
        {
            var intent = Resolve(InventoryContext.None, SourceOf(sourceName), backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        // ── Healer open: the same sale row as the Vendor's (issues #121, #128) ──

        [TestCase(nameof(backpack))]
        [TestCase(nameof(equipment))]
        public void HealerContext_BackpackAndEquipment_SellTheItem(string sourceName)
        {
            var intent = Resolve(InventoryContext.Healer, SourceOf(sourceName), backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.Sell));
        }

                [Test]
        public void HealerContext_Stash_DoesNothing()
        {
            var intent = Resolve(InventoryContext.Healer, stash, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        // ── Hero context: no rows in Town, deliberately (issue #85); the ground is its one sink in a Run (issue #63) ──

        [TestCase(nameof(backpack))]
        [TestCase(nameof(stash))]
        [TestCase(nameof(equipment))]
        public void HeroContext_PlayerContainer_DoesNothing_WhenThereIsNoGround(string sourceName)
        {
            var intent = Resolve(InventoryContext.Hero, SourceOf(sourceName), backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        [Test]
        public void HeroContext_Backpack_DropsTheItemToTheGround_WhenARunHasOne()
        {
            var intent = Resolve(InventoryContext.Hero, backpack, backpack, stash, equipment, store, groundOpen: true);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.Drop));
            Assert.That(intent.Target, Is.Null);
        }

        [TestCase(nameof(stash))]
        [TestCase(nameof(equipment))]
        public void HeroContext_OtherPlayerContainers_StillDoNothing_WithAGround(string sourceName)
        {
            // Equipment keeps the Hero exemption from rule two: unequipping to the dirt on a stray
            // shift-click would duplicate right-click, and the Stash is not on screen in this context.
            var intent = Resolve(InventoryContext.Hero, SourceOf(sourceName), backpack, stash, equipment, store, groundOpen: true);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        // ── Ground context: the Hero row, and picking a Drop up off the ground ──

        [Test]
        public void GroundContext_Backpack_DropsTheItemToTheGround()
        {
            var intent = Resolve(InventoryContext.Ground, backpack, backpack, stash, equipment, store, groundOpen: true);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.Drop));
        }

        [Test]
        public void GroundContext_TheGround_PicksTheDropUp()
        {
            var intent = Resolve(InventoryContext.Ground, ground, backpack, stash, equipment, store, groundOpen: true);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.PickUp));
            Assert.That(intent.Target, Is.Null);
        }

        [TestCase(nameof(stash))]
        [TestCase(nameof(equipment))]
        public void GroundContext_OtherPlayerContainers_DoNothing(string sourceName)
        {
            var intent = Resolve(InventoryContext.Ground, SourceOf(sourceName), backpack, stash, equipment, store, groundOpen: true);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        [Test]
        public void GroundContext_WithNoGroundOpen_DoesNothing()
        {
            Assert.That(Resolve(InventoryContext.Ground, backpack, backpack, stash, equipment, store).Kind, Is.EqualTo(QuickMoveIntentKind.None));
            Assert.That(Resolve(InventoryContext.Ground, ground, backpack, stash, equipment, store).Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        [Test]
        public void TheGround_PicksNothingUp_OutsideTheGroundContext()
        {
            Assert.That(Resolve(InventoryContext.Hero, ground, backpack, stash, equipment, store, groundOpen: true).Kind,
                Is.EqualTo(QuickMoveIntentKind.None));
        }

        [TestCase(InventoryContext.None)]
        [TestCase(InventoryContext.Stash, QuickMoveIntentKind.MoveToContainer)]
        [TestCase(InventoryContext.Vendor, QuickMoveIntentKind.Sell)]
        [TestCase(InventoryContext.Healer, QuickMoveIntentKind.Sell)]
        public void AnotherContext_IsUnchangedByTheGround(InventoryContext context, QuickMoveIntentKind expected = QuickMoveIntentKind.None)
        {
            var withGround = Resolve(context, backpack, backpack, stash, equipment, store, groundOpen: true);

            Assert.That(withGround.Kind, Is.EqualTo(expected), "the ground is the Hero context's sink, not a second sink elsewhere");
        }

        // ── Vendor open: shift-click sells through the Sale (#128, replacing #33's staging) ──

        [Test]
        public void VendorContext_Backpack_SellsTheItem()
        {
            var intent = Resolve(InventoryContext.Vendor, backpack, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.Sell));
        }

        [Test]
        public void VendorContext_Equipment_SellsTheItem()
        {
            var intent = Resolve(InventoryContext.Vendor, equipment, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.Sell));
        }

                [Test]
        public void VendorContext_Stash_DoesNothing()
        {
            var intent = Resolve(InventoryContext.Vendor, stash, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        // ── The shelf: its own shift-click stays a buy in every context ──

        [TestCase(InventoryContext.None)]
        [TestCase(InventoryContext.Hero)]
        [TestCase(InventoryContext.Stash)]
        [TestCase(InventoryContext.Vendor)]
        [TestCase(InventoryContext.Healer)]
        public void ShelfSource_StaysABuy(InventoryContext context)
        {
            var intent = Resolve(context, store, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.Buy));
        }

        // ── The Healer's shelf (issue #121): any Supply is a buy, not just the Vendor's ──

        [TestCase(InventoryContext.None)]
        [TestCase(InventoryContext.Hero)]
        [TestCase(InventoryContext.Stash)]
        [TestCase(InventoryContext.Vendor)]
        [TestCase(InventoryContext.Healer)]
        public void HealerShelfSource_IsABuy_InEveryContext(InventoryContext context)
        {
            var intent = Resolve(context, healerSupply, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.Buy));
        }

        // ── The Sold container (issue #126): one more Supply, outside the table ──

        [TestCase(InventoryContext.None)]
        [TestCase(InventoryContext.Hero)]
        [TestCase(InventoryContext.Stash)]
        [TestCase(InventoryContext.Vendor)]
        [TestCase(InventoryContext.Healer)]
        public void SoldSource_IsABuy_InEveryContext(InventoryContext context)
        {
            var intent = Resolve(context, sold, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.Buy));
        }

        // ── intent shape ──

        [Test]
        public void MoveToIntent_TargetsTheNamedContainer()
        {
            var intent = QuickMoveIntent.MoveTo(stash);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(stash));
        }

        [Test]
        public void DoNothingAndBuyCarryNoTarget()
        {
            Assert.That(QuickMoveIntent.None.Target, Is.Null);
            Assert.That(QuickMoveIntent.Buy.Target, Is.Null);
        }

        private AbstractDimensionalContainer SourceOf(string name) => name switch
        {
            nameof(backpack) => backpack,
            nameof(stash) => stash,
            nameof(equipment) => equipment,
            _ => store
        };
    }
}