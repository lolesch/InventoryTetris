using NUnit.Framework;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The side-panel state machine (issue #54): the default state, the set/clear
    /// idempotency contract, and the change-event contract. These exercise the real
    /// <see cref="SidePanelState"/> the <c>InventoryProvider</c> forwards to - not a copy
    /// of it - so a change to the rule that breaks the contract fails here.
    /// </summary>
    [TestFixture]
    public sealed class SidePanelStateTests
    {
        private SidePanelState state;

        [SetUp]
        public void SetUp() => state = new SidePanelState();

        // ── Default state ────────────────────────────────────────────────

        [Test]
        public void Active_DefaultsToNone()
        {
            Assert.That(state.Active, Is.EqualTo(SidePanelContext.None));
        }

        // ── Set ──────────────────────────────────────────────────────────

        [Test]
        public void Set_ChangesActive()
        {
            state.Set(SidePanelContext.Stash);

            Assert.That(state.Active, Is.EqualTo(SidePanelContext.Stash));
        }

        [Test]
        public void Set_SameValue_DoesNotFireEvent()
        {
            state.Set(SidePanelContext.Stash);
            var fired = 0;
            state.Changed += _ => fired++;

            state.Set(SidePanelContext.Stash);

            Assert.That(fired, Is.Zero);
            Assert.That(state.Active, Is.EqualTo(SidePanelContext.Stash));
        }

        [Test]
        public void Set_DifferentValue_FiresEvent()
        {
            state.Set(SidePanelContext.Stash);
            SidePanelContext? seen = null;
            state.Changed += ctx => seen = ctx;

            state.Set(SidePanelContext.Vendor);

            Assert.That(seen, Is.EqualTo(SidePanelContext.Vendor));
            Assert.That(state.Active, Is.EqualTo(SidePanelContext.Vendor));
        }

        [Test]
        public void Set_CanTransitionFromNone()
        {
            state.Set(SidePanelContext.Vendor);

            Assert.That(state.Active, Is.EqualTo(SidePanelContext.Vendor));
        }

        // ── Clear ────────────────────────────────────────────────────────

        [Test]
        public void Clear_ReturnsToNone_WhenContextMatches()
        {
            state.Set(SidePanelContext.Stash);
            SidePanelContext? seen = null;
            state.Changed += ctx => seen = ctx;

            state.Clear(SidePanelContext.Stash);

            Assert.That(state.Active, Is.EqualTo(SidePanelContext.None));
            Assert.That(seen, Is.EqualTo(SidePanelContext.None));
        }

        [Test]
        public void Clear_DoesNothing_WhenContextDoesNotMatch()
        {
            state.Set(SidePanelContext.Stash);
            var fired = 0;
            state.Changed += _ => fired++;

            state.Clear(SidePanelContext.Vendor);

            Assert.That(fired, Is.Zero);
            Assert.That(state.Active, Is.EqualTo(SidePanelContext.Stash));
        }

        [Test]
        public void Clear_FromNone_DoesNothing()
        {
            var fired = 0;
            state.Changed += _ => fired++;

            state.Clear(SidePanelContext.None);

            Assert.That(fired, Is.Zero);
            Assert.That(state.Active, Is.EqualTo(SidePanelContext.None));
        }

        [Test]
        public void Clear_IsIdempotent_WhenRepeated()
        {
            state.Set(SidePanelContext.Stash);
            state.Clear(SidePanelContext.Stash);
            var fired = 0;
            state.Changed += _ => fired++;

            state.Clear(SidePanelContext.Stash);

            Assert.That(fired, Is.Zero);
            Assert.That(state.Active, Is.EqualTo(SidePanelContext.None));
        }

        // ── Changed ──────────────────────────────────────────────────────

        [Test]
        public void Changed_FiresWithNewContext()
        {
            SidePanelContext? seen = null;
            state.Changed += ctx => seen = ctx;

            state.Set(SidePanelContext.Vendor);

            Assert.That(seen, Is.EqualTo(SidePanelContext.Vendor));
        }

        // ── Toggle handover (issue #57) ──────────────────────────────────

        /// <summary>
        /// The order a RadioGroup handover actually produces: AbstractToggle.SetToggle calls
        /// RadioGroup.Activate from inside base.SetToggle, so every loser Clears before the
        /// winner Sets. The Sell Basket depends on the None in the middle - that is the edge
        /// it cancels a staged sale on.
        /// </summary>
        [Test]
        public void Handover_LoserClearsBeforeWinnerSets_PublishesNoneInBetween()
        {
            state.Set(SidePanelContext.Vendor);
            var seen = new List<SidePanelContext>();
            state.Changed += seen.Add;

            state.Clear(SidePanelContext.Vendor);   // the losing toggle switches off
            state.Set(SidePanelContext.Stash);      // the winning toggle switches on

            Assert.That(seen, Is.EqualTo(new[] { SidePanelContext.None, SidePanelContext.Stash }));
            Assert.That(state.Active, Is.EqualTo(SidePanelContext.Stash));
        }

        /// <summary>
        /// The same handover with the two halves swapped - which is what a change to
        /// AbstractToggle's ordering would produce. The winner must survive: Clear only clears
        /// when the context matches, so a late loser cannot close the panel just opened. This
        /// is why SidePanelToggle does not depend on the current order.
        /// </summary>
        [Test]
        public void Handover_WinnerSetsBeforeLoserClears_LoserDoesNotCloseTheWinner()
        {
            state.Set(SidePanelContext.Vendor);
            state.Changed += _ => { };

            state.Set(SidePanelContext.Stash);      // the winning toggle switches on first
            state.Clear(SidePanelContext.Vendor);   // the losing toggle switches off after

            Assert.That(state.Active, Is.EqualTo(SidePanelContext.Stash));
        }

        /// <summary>
        /// Healer and Go Venture are members of the same TownGroup but carry no context
        /// (#58), so a handover to one of them is a bare Clear. The Vendor still closes - that
        /// is what cancels a staged sale without the Healer knowing the Vendor exists.
        /// </summary>
        [Test]
        public void Handover_ToAContextlessSibling_StillClosesTheOpenPanel()
        {
            state.Set(SidePanelContext.Vendor);
            SidePanelContext? seen = null;
            state.Changed += ctx => seen = ctx;

            state.Clear(SidePanelContext.Vendor);   // Healer / Go Venture switches the Vendor off

            Assert.That(seen, Is.EqualTo(SidePanelContext.None));
            Assert.That(state.Active, Is.EqualTo(SidePanelContext.None));
        }

        [Test]
        public void Changed_FiresNone_WhenCleared()
        {
            state.Set(SidePanelContext.Stash);
            SidePanelContext? seen = null;
            state.Changed += ctx => seen = ctx;

            state.Clear(SidePanelContext.Stash);

            Assert.That(seen, Is.EqualTo(SidePanelContext.None));
        }
    }
}
