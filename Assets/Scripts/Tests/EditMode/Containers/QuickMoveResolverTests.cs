using NUnit.Framework;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The quick-move matrix (issue #30): shift-click routes an item to whichever side
    /// panel is open. A pure resolver maps every (source container, side panel) pair to
    /// one intent - do nothing, move to a named container, or buy - so the three slot
    /// displays that used to hand-roll a move each now ask it. This ticket wires only the
    /// Stash and "no panel open" rows: the player's containers sent to the Vendor return
    /// "do nothing" until the Sell Basket exists (#33); the vendor shelf's own shift-click
    /// stays a buy in every context.
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

        private static QuickMoveIntent Resolve(SidePanelContext context, AbstractDimensionalContainer source,
            AbstractDimensionalContainer backpack, AbstractDimensionalContainer stash,
            AbstractDimensionalContainer equipment, AbstractDimensionalContainer store)
            => QuickMoveResolver.Resolve(context, source, backpack, stash, equipment, store);

        // ── Stash open: backpack ↔ Stash, equipment → Stash (byte-for-byte as today) ──

        [Test]
        public void StashContext_Backpack_SendsTheItemToTheStash()
        {
            var intent = Resolve(SidePanelContext.Stash, backpack, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(stash));
        }

        [Test]
        public void StashContext_Stash_SendsTheItemBackToTheBackpack()
        {
            var intent = Resolve(SidePanelContext.Stash, stash, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(backpack));
        }

        [Test]
        public void StashContext_Equipment_SendsTheItemToTheStash()
        {
            var intent = Resolve(SidePanelContext.Stash, equipment, backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.MoveToContainer));
            Assert.That(intent.Target, Is.SameAs(stash));
        }

        // ── No panel open: shift-click does nothing ──

        [TestCase(nameof(backpack))]
        [TestCase(nameof(stash))]
        [TestCase(nameof(equipment))]
        public void NoneContext_PlayerContainer_DoesNothing(string sourceName)
        {
            var intent = Resolve(SidePanelContext.None, SourceOf(sourceName), backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        // ── Vendor open: the player's containers wait for the Sell Basket (#33) ──

        [TestCase(nameof(backpack))]
        [TestCase(nameof(stash))]
        [TestCase(nameof(equipment))]
        public void VendorContext_PlayerContainer_DoesNothingForNow(string sourceName)
        {
            var intent = Resolve(SidePanelContext.Vendor, SourceOf(sourceName), backpack, stash, equipment, store);

            Assert.That(intent.Kind, Is.EqualTo(QuickMoveIntentKind.None));
        }

        // ── The shelf: its own shift-click stays a buy in every context ──

        [TestCase(SidePanelContext.None)]
        [TestCase(SidePanelContext.Stash)]
        [TestCase(SidePanelContext.Vendor)]
        public void ShelfSource_StaysABuy(SidePanelContext context)
        {
            var intent = Resolve(context, store, backpack, stash, equipment, store);

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