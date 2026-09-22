using NUnit.Framework;
using System;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The Inventory Context rule (issue #83) - the only thing the scene's panel visibility and
    /// toggle pressed-state are derived from since #85. Exercises the real
    /// <see cref="InventoryContextState"/> rather than a copy of it: the default state, the
    /// single-change-per-Set contract, the derived panel set, closing-is-always-None, and phase
    /// reachability.
    ///
    /// <para>The two static derivations the scene calls are covered here too, because they are
    /// the statement of the rule rather than a helper: <see cref="InventoryContextState.PanelFor"/>
    /// is the one panel a context names for itself, and <see cref="InventoryContextState.PanelsFor"/>
    /// adds the Hero Panel to every non-<c>None</c> context. A panel asks the second about the
    /// active context and the first about itself; if the two disagreed, a panel would show or
    /// hide for the wrong context, and only a test here would notice.</para>
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

        [TestCase(InventoryContext.None, InventoryPanels.None)]
        [TestCase(InventoryContext.Hero, InventoryPanels.Hero)]
        [TestCase(InventoryContext.Stash, InventoryPanels.Stash)]
        [TestCase(InventoryContext.Vendor, InventoryPanels.Vendor)]
        [TestCase(InventoryContext.Healer, InventoryPanels.Healer)]
        public void PanelFor_NamesTheOnePanelAContextOwns(InventoryContext context, InventoryPanels expected)
        {
            Assert.That(InventoryContextState.PanelFor(context), Is.EqualTo(expected));
        }

        /// <summary>
        /// The exact computation every panel's subscription and every toggle's pressed-visual
        /// resync makes (issue #85): a panel is up when the active context derives the one panel
        /// it owns. The Hero Panel is up in every non-<c>None</c> context, a Town Stop's panel
        /// only in its own, and none of them when nothing is open - which is the rule, spelled
        /// out once, for all four panels at once.
        /// </summary>
        [TestCase(InventoryContext.None, InventoryPanels.Hero, false)]
        [TestCase(InventoryContext.None, InventoryPanels.Stash, false)]
        [TestCase(InventoryContext.None, InventoryPanels.Vendor, false)]
        [TestCase(InventoryContext.None, InventoryPanels.Healer, false)]
        [TestCase(InventoryContext.Hero, InventoryPanels.Hero, true)]
        [TestCase(InventoryContext.Hero, InventoryPanels.Stash, false)]
        [TestCase(InventoryContext.Hero, InventoryPanels.Vendor, false)]
        [TestCase(InventoryContext.Hero, InventoryPanels.Healer, false)]
        [TestCase(InventoryContext.Stash, InventoryPanels.Hero, true)]
        [TestCase(InventoryContext.Stash, InventoryPanels.Stash, true)]
        [TestCase(InventoryContext.Stash, InventoryPanels.Vendor, false)]
        [TestCase(InventoryContext.Stash, InventoryPanels.Healer, false)]
        [TestCase(InventoryContext.Vendor, InventoryPanels.Hero, true)]
        [TestCase(InventoryContext.Vendor, InventoryPanels.Stash, false)]
        [TestCase(InventoryContext.Vendor, InventoryPanels.Vendor, true)]
        [TestCase(InventoryContext.Vendor, InventoryPanels.Healer, false)]
        [TestCase(InventoryContext.Healer, InventoryPanels.Hero, true)]
        [TestCase(InventoryContext.Healer, InventoryPanels.Stash, false)]
        [TestCase(InventoryContext.Healer, InventoryPanels.Vendor, false)]
        [TestCase(InventoryContext.Healer, InventoryPanels.Healer, true)]
        public void PanelIsUp_ExactlyWhenTheActiveContextDerivesIt(InventoryContext active, InventoryPanels panel, bool expected)
        {
            state.Set(active);

            var isUp = (state.Panels & panel) != InventoryPanels.None;

            Assert.That(isUp, Is.EqualTo(expected));
        }

        /// <summary>
        /// One Town Stop never derives another's panel, whichever one it is - the property that
        /// used to need a runtime exclusivity group to enforce (issue #85).
        /// </summary>
        [TestCase(InventoryContext.Stash, InventoryPanels.Vendor)]
        [TestCase(InventoryContext.Stash, InventoryPanels.Healer)]
        [TestCase(InventoryContext.Vendor, InventoryPanels.Stash)]
        [TestCase(InventoryContext.Vendor, InventoryPanels.Healer)]
        [TestCase(InventoryContext.Healer, InventoryPanels.Stash)]
        [TestCase(InventoryContext.Healer, InventoryPanels.Vendor)]
        public void TownStopContext_NeverDerivesAnotherTownStopsPanel(InventoryContext active, InventoryPanels sibling)
        {
            state.Set(active);

            Assert.That(state.Panels & sibling, Is.EqualTo(InventoryPanels.None));
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
