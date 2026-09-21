using NUnit.Framework;
using System;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The Inventory Context rule (issue #83), standing beside <see cref="SidePanelState"/>
    /// rather than replacing it - nothing is wired to this yet. Exercises the real
    /// <see cref="InventoryContextState"/>: the default state, the single-change-per-Set
    /// contract, the derived panel set, closing-is-always-None, and phase reachability.
    /// </summary>
    [TestFixture]
    public sealed class InventoryContextStateTests
    {
        private InventoryContextState state;

        [SetUp]
        public void SetUp() => state = new InventoryContextState();

        // ── Default state ────────────────────────────────────────────────

        [Test]
        public void Active_DefaultsToNone()
        {
            Assert.That(state.Active, Is.EqualTo(InventoryContext.None));
        }

        [Test]
        public void Panels_DefaultsToNone()
        {
            Assert.That(state.Panels, Is.EqualTo(InventoryPanels.None));
        }

        // ── Set ──────────────────────────────────────────────────────────

        [Test]
        public void Set_ChangesActive()
        {
            state.Set(InventoryContext.Vendor);

            Assert.That(state.Active, Is.EqualTo(InventoryContext.Vendor));
        }

        [Test]
        public void Set_SameValue_DoesNotFireEvent()
        {
            state.Set(InventoryContext.Vendor);
            var fired = 0;
            state.Changed += _ => fired++;

            state.Set(InventoryContext.Vendor);

            Assert.That(fired, Is.Zero);
        }

        [Test]
        public void Set_DifferentValue_FiresEventOnce()
        {
            InventoryContext? seen = null;
            var fired = 0;
            state.Changed += ctx => { seen = ctx; fired++; };

            state.Set(InventoryContext.Vendor);

            Assert.That(fired, Is.EqualTo(1));
            Assert.That(seen, Is.EqualTo(InventoryContext.Vendor));
        }

        // ── Handover (no intermediate None) ─────────────────────────────

        /// <summary>
        /// The behaviour change from #75's contract: a Town Stop handover is one context
        /// change, not a close followed by an open - so the Hero Panel never flickers.
        /// </summary>
        [Test]
        public void Handover_BetweenTwoTownStops_PublishesOneChange()
        {
            state.Set(InventoryContext.Stash);
            var seen = new System.Collections.Generic.List<InventoryContext>();
            state.Changed += seen.Add;

            state.Set(InventoryContext.Vendor);

            Assert.That(seen, Is.EqualTo(new[] { InventoryContext.Vendor }));
            Assert.That(state.Active, Is.EqualTo(InventoryContext.Vendor));
        }

        // ── Close ────────────────────────────────────────────────────────

        [Test]
        public void Close_FromAnyContext_ReturnsToNone()
        {
            state.Set(InventoryContext.Healer);
            InventoryContext? seen = null;
            state.Changed += ctx => seen = ctx;

            state.Close();

            Assert.That(state.Active, Is.EqualTo(InventoryContext.None));
            Assert.That(seen, Is.EqualTo(InventoryContext.None));
        }

        [Test]
        public void Close_FromNone_DoesNothing()
        {
            var fired = 0;
            state.Changed += _ => fired++;

            state.Close();

            Assert.That(fired, Is.Zero);
            Assert.That(state.Active, Is.EqualTo(InventoryContext.None));
        }

        [Test]
        public void Close_IsIdempotent_WhenRepeated()
        {
            state.Set(InventoryContext.Stash);
            state.Close();
            var fired = 0;
            state.Changed += _ => fired++;

            state.Close();

            Assert.That(fired, Is.Zero);
        }

        // ── Derived panel set ────────────────────────────────────────────

        [TestCase(InventoryContext.None, InventoryPanels.None)]
        [TestCase(InventoryContext.Hero, InventoryPanels.Hero)]
        [TestCase(InventoryContext.Stash, InventoryPanels.Hero | InventoryPanels.Stash)]
        [TestCase(InventoryContext.Vendor, InventoryPanels.Hero | InventoryPanels.Vendor)]
        [TestCase(InventoryContext.Healer, InventoryPanels.Hero | InventoryPanels.Healer)]
        public void Panels_IncludesHeroForEveryNonNoneContext(InventoryContext context, InventoryPanels expected)
        {
            state.Set(context);

            Assert.That(state.Panels, Is.EqualTo(expected));
        }

        // ── The context and the panel set are different, differently-shaped types ──

        [Test]
        public void Context_IsSingleValued_NotAFlagsType()
        {
            // InventoryContext can never hold two Town Stops at once - it isn't that kind
            // of type. Stash-and-Vendor-together is not an expressible Active value.
            Assert.That(Attribute.GetCustomAttribute(typeof(InventoryContext), typeof(FlagsAttribute)), Is.Null);
        }

        [Test]
        public void Panels_IsAFlagsType_SoAContextCanNameTwoPanels()
        {
            Assert.That(Attribute.GetCustomAttribute(typeof(InventoryPanels), typeof(FlagsAttribute)), Is.Not.Null);
        }

        // ── Phase reachability ───────────────────────────────────────────

        [Test]
        public void SyncToPhase_EnteringField_DropsUnreachableContext_ToNone()
        {
            state.Set(InventoryContext.Vendor);
            InventoryContext? seen = null;
            state.Changed += ctx => seen = ctx;

            state.SyncToPhase(inField: true);

            Assert.That(state.Active, Is.EqualTo(InventoryContext.None));
            Assert.That(seen, Is.EqualTo(InventoryContext.None));
        }

        [Test]
        public void SyncToPhase_EnteringField_KeepsHeroReachable()
        {
            state.Set(InventoryContext.Hero);
            var fired = 0;
            state.Changed += _ => fired++;

            state.SyncToPhase(inField: true);

            Assert.That(state.Active, Is.EqualTo(InventoryContext.Hero));
            Assert.That(fired, Is.Zero);
        }

        [Test]
        public void SyncToPhase_EnteringField_KeepsNoneReachable()
        {
            var fired = 0;
            state.Changed += _ => fired++;

            state.SyncToPhase(inField: true);

            Assert.That(state.Active, Is.EqualTo(InventoryContext.None));
            Assert.That(fired, Is.Zero);
        }

        [Test]
        public void SyncToPhase_ReturningToTown_DoesNotReopenAnything()
        {
            state.Set(InventoryContext.Vendor);
            state.SyncToPhase(inField: true); // drops to None

            state.SyncToPhase(inField: false);

            Assert.That(state.Active, Is.EqualTo(InventoryContext.None));
        }

        [Test]
        public void SyncToPhase_InTown_LeavesReachableContextUntouched()
        {
            state.Set(InventoryContext.Stash);
            var fired = 0;
            state.Changed += _ => fired++;

            state.SyncToPhase(inField: false);

            Assert.That(state.Active, Is.EqualTo(InventoryContext.Stash));
            Assert.That(fired, Is.Zero);
        }
    }
}
