using NUnit.Framework;
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
