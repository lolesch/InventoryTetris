using NUnit.Framework;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The quick-move matrix (issue #30): shift-click routes an item to whichever side
    /// panel is open. A pure resolver maps every (source container, side panel) pair to
    /// one intent - do nothing, move to a named container, send to the Sell Basket, or
    /// buy - so the three slot displays that used to hand-roll a move each now ask it.
    /// This ticket wires the Stash rows, the "no panel open" rows, and the Vendor rows
    /// (issue #33): with the Vendor open, backpack and equipment shift-clicks go to the
    /// Sell Basket and a basket Package returns to the backpack, while the vendor shelf's
    /// own shift-click stays a buy in every context.
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

        private static QuickMoveIntent Resolve(SidePanelContext context, AbstractDimensionalContainer source,
            AbstractDimensionalContainer backpack, AbstractDimensionalContainer stash,
            AbstractDimensionalContainer equipment, AbstractDimensionalContainer store,
            AbstractDimensionalContainer basket)
            => QuickMoveResolver.Resolve(context, source, backpack, stash, equipment, store, basket);

        // ── Stash open: backpack ↔ Stash, equipment → Stash (byte-for-byte as today) ──

        [Test]
        public void StashContext_Backpack_SendsTheItemToTheStash()
        {
            var intent = Resolve(SidePanelContext.Stash, backpack, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(stash));
        }

        [Test]
        public void StashContext_Stash_SendsTheItemBackToTheBackpack()
        {
            var intent = Resolve(SidePanelContext.Stash, stash, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(backpack));
        }

        [Test]
        public void StashContext_Equipment_SendsTheItemToTheStash()
        {
            var intent = Resolve(SidePanelContext.Stash, equipment, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(stash));
        }

        // ── No panel open: shift-click does nothing ──

        [TestCase(nameof(backpack))]
        [TestCase(nameof(stash))]
        [TestCase(nameof(equipment))]
        [TestCase(nameof(basket))]
        public void NoneContext_PlayerContainer_DoesNothing(string sourceName)
        {
            var intent = Resolve(SidePanelContext.None, SourceOf(sourceName), backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        // ── Vendor open: shift-click sells into the Sell Basket (#33) ──

        [Test]
        public void VendorContext_Backpack_SendsTheItemToTheSellBasket()
        {
            var intent = Resolve(SidePanelContext.Vendor, backpack, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.SellBasket));
        }

        [Test]
        public void VendorContext_Equipment_SendsTheItemToTheSellBasket()
        {
            var intent = Resolve(SidePanelContext.Vendor, equipment, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.SellBasket));
        }

        [Test]
        public void VendorContext_Basket_ReturnsTheItemToTheBackpack()
        {
            var intent = Resolve(SidePanelContext.Vendor, basket, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(backpack));
        }

        [Test]
        public void VendorContext_Stash_DoesNothing()
        {
            var intent = Resolve(SidePanelContext.Vendor, stash, backpack, stash, equipment, store, basket);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        // ── The shelf: its own shift-click stays a buy in every context ──

        [TestCase(SidePanelContext.None)]
        [TestCase(SidePanelContext.Stash)]
        [TestCase(SidePanelContext.Vendor)]
        public void ShelfSource_StaysABuy(SidePanelContext context)
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