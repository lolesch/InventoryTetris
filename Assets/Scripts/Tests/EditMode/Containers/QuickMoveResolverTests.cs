using NUnit.Framework;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The quick-move matrix (issue #30): shift-click routes an item to wherever the open
    /// context says. A pure resolver maps every (source container, Inventory Context) pair to
    /// one intent - do nothing, move to a named container, send to the Sell Basket, or
    /// buy - so the three slot displays that used to hand-roll a move each now ask it.
    /// This fixture wires the Stash rows, the "no context" rows, and the Vendor rows
    /// (issue #33): with the Vendor open, backpack and equipment shift-clicks go to the
    /// Sell Basket and a basket Package returns to the backpack, while the vendor shelf's
    /// own shift-click stays a buy in every context.
    ///
    /// <para>The context replaced <c>SidePanelContext</c> in #85, so the rows are keyed on the
    /// Inventory Context now. Its two new members resolve to nothing on purpose: the Hero
    /// Panel's sink would be Equipment, but that row duplicates right-click and has no ticket,
    /// and the Healer has no containers yet, so it has no sink at all. Both are asserted here
    /// rather than left implicit, because "a Quick Move with the Healer open does nothing" is a
    /// stated outcome of #85, not an oversight.</para>
    /// </summary>
    [TestFixture]
    public sealed class QuickMoveResolverTests
    {
        // The resolver compares sources by MonoBehaviour reference identity (a container is
        // a UnityEngine.Object); distinct instances are enough - nothing here needs items.
        private readonly AbstractDimensionalContainer backpack = new CharacterInventory(new Vector2Int(4, 4));
        private readonly AbstractDimensionalContainer stash = new CharacterInventory(new Vector2Int(4, 4));
        private readonly AbstractDimensionalContainer equipment = new CharacterEquipment(new Vector2Int(14, 1), null);
        private readonly AbstractDimensionalContainer store = new CharacterInventory(new Vector2Int(4, 4));
        private readonly AbstractDimensionalContainer basket = new CharacterInventory(new Vector2Int(4, 4));

        private static QuickMoveIntent Resolve(InventoryContext context, AbstractDimensionalContainer source,
            AbstractDimensionalContainer backpack, AbstractDimensionalContainer stash,
            AbstractDimensionalContainer equipment, AbstractDimensionalContainer store,
            AbstractDimensionalContainer basket)
            => QuickMoveResolver.Resolve(context, source, backpack, stash, equipment, store, basket);

        // ── Stash open: backpack ↔ Stash, equipment → Stash (byte-for-byte as today) ──

        [Test]
        public void StashContext_Backpack_SendsTheItemToTheStash()
        {
            var intent = Resolve(InventoryContext.Stash, backpack, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(stash));
        }

        [Test]
        public void StashContext_Stash_SendsTheItemBackToTheBackpack()
        {
            var intent = Resolve(InventoryContext.Stash, stash, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(backpack));
        }

        [Test]
        public void StashContext_Equipment_SendsTheItemToTheStash()
        {
            var intent = Resolve(InventoryContext.Stash, equipment, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(stash));
        }

        // ── No context open: shift-click does nothing ──

        [TestCase(nameof(backpack))]
        [TestCase(nameof(stash))]
        [TestCase(nameof(equipment))]
        [TestCase(nameof(basket))]
        public void NoneContext_PlayerContainer_DoesNothing(string sourceName)
        {
            var intent = Resolve(InventoryContext.None, SourceOf(sourceName), backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        // ── Hero and Healer contexts: no rows, deliberately (issue #85) ──

        [TestCase(nameof(backpack))]
        [TestCase(nameof(stash))]
        [TestCase(nameof(equipment))]
        [TestCase(nameof(basket))]
        public void HealerContext_PlayerContainer_DoesNothing(string sourceName)
        {
            var intent = Resolve(InventoryContext.Healer, SourceOf(sourceName), backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        [TestCase(nameof(backpack))]
        [TestCase(nameof(stash))]
        [TestCase(nameof(equipment))]
        [TestCase(nameof(basket))]
        public void HeroContext_PlayerContainer_DoesNothing(string sourceName)
        {
            var intent = Resolve(InventoryContext.Hero, SourceOf(sourceName), backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        // ── Vendor open: shift-click sells into the Sell Basket (#33) ──

        [Test]
        public void VendorContext_Backpack_SendsTheItemToTheSellBasket()
        {
            var intent = Resolve(InventoryContext.Vendor, backpack, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.SellBasket));
        }

        [Test]
        public void VendorContext_Equipment_SendsTheItemToTheSellBasket()
        {
            var intent = Resolve(InventoryContext.Vendor, equipment, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.SellBasket));
        }

        [Test]
        public void VendorContext_Basket_ReturnsTheItemToTheBackpack()
        {
            var intent = Resolve(InventoryContext.Vendor, basket, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(backpack));
        }

        [Test]
        public void VendorContext_Stash_DoesNothing()
        {
            var intent = Resolve(InventoryContext.Vendor, stash, backpack, stash, equipment, store, basket);

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
            var intent = Resolve(context, store, backpack, stash, equipment, store, basket);

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
            nameof(basket) => basket,
            _ => store
        };
    }
}