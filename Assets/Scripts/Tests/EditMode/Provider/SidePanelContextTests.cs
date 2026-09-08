using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Provider
{
    /// <summary>
    /// Tests for the SidePanelContext state machine (issue #54): the enum, the
    /// set/clear idempotency contract, and the change-event contract. These are
    /// pure C# and don't need a MonoBehaviour — we test the logic on a small
    /// helper class that mirrors InventoryProvider's SidePanel implementation.
    /// </summary>
    [TestFixture]
    public sealed class SidePanelContextTests
    {
        // Mirror of InventoryProvider's SidePanel methods for testability.
        private sealed class SidePanelHarness
        {
            public SidePanelContext ActiveSidePanel { get; private set; }
            public event Action<SidePanelContext> OnSidePanelChanged;

            public void SetSidePanel(SidePanelContext context)
            {
                if (ActiveSidePanel == context)
                    return;

                ActiveSidePanel = context;
                OnSidePanelChanged?.Invoke(context);
            }

            public void ClearSidePanel(SidePanelContext context)
            {
                if (ActiveSidePanel == SidePanelContext.None || ActiveSidePanel != context)
                    return;

                ActiveSidePanel = SidePanelContext.None;
                OnSidePanelChanged?.Invoke(SidePanelContext.None);
            }
        }

        private SidePanelHarness harness;

        [SetUp]
        public void SetUp() => harness = new SidePanelHarness();

        // ── Default state ────────────────────────────────────────────────

        [Test]
        public void ActiveSidePanel_DefaultsToNone()
        {
            Assert.That(harness.ActiveSidePanel, Is.EqualTo(SidePanelContext.None));
        }

        // ── SetSidePanel ─────────────────────────────────────────────────

        [Test]
        public void SetSidePanel_ChangesActiveSidePanel()
        {
            harness.SetSidePanel(SidePanelContext.Stash);

            Assert.That(harness.ActiveSidePanel, Is.EqualTo(SidePanelContext.Stash));
        }

        [Test]
        public void SetSidePanel_SameValue_DoesNotFireEvent()
        {
            harness.SetSidePanel(SidePanelContext.Stash);
            var fired = 0;
            harness.OnSidePanelChanged += _ => fired++;

            harness.SetSidePanel(SidePanelContext.Stash);

            Assert.That(fired, Is.Zero);
            Assert.That(harness.ActiveSidePanel, Is.EqualTo(SidePanelContext.Stash));
        }

        [Test]
        public void SetSidePanel_DifferentValue_FiresEvent()
        {
            harness.SetSidePanel(SidePanelContext.Stash);
            SidePanelContext? seen = null;
            harness.OnSidePanelChanged += ctx => seen = ctx;

            harness.SetSidePanel(SidePanelContext.Vendor);

            Assert.That(seen, Is.EqualTo(SidePanelContext.Vendor));
            Assert.That(harness.ActiveSidePanel, Is.EqualTo(SidePanelContext.Vendor));
        }

        [Test]
        public void SetSidePanel_CanTransitionFromNone()
        {
            harness.SetSidePanel(SidePanelContext.Vendor);

            Assert.That(harness.ActiveSidePanel, Is.EqualTo(SidePanelContext.Vendor));
        }

        // ── ClearSidePanel ───────────────────────────────────────────────

        [Test]
        public void ClearSidePanel_Idempotent_WhenContextMatches()
        {
            harness.SetSidePanel(SidePanelContext.Stash);
            SidePanelContext? seen = null;
            harness.OnSidePanelChanged += ctx => seen = ctx;

            harness.ClearSidePanel(SidePanelContext.Stash);

            Assert.That(harness.ActiveSidePanel, Is.EqualTo(SidePanelContext.None));
            Assert.That(seen, Is.EqualTo(SidePanelContext.None));
        }

        [Test]
        public void ClearSidePanel_DoesNothing_WhenContextDoesNotMatch()
        {
            harness.SetSidePanel(SidePanelContext.Stash);
            var fired = 0;
            harness.OnSidePanelChanged += _ => fired++;

            harness.ClearSidePanel(SidePanelContext.Vendor);

            Assert.That(fired, Is.Zero);
            Assert.That(harness.ActiveSidePanel, Is.EqualTo(SidePanelContext.Stash));
        }

        [Test]
        public void ClearSidePanel_FromNone_DoesNothing()
        {
            var fired = 0;
            harness.OnSidePanelChanged += _ => fired++;

            harness.ClearSidePanel(SidePanelContext.None);

            Assert.That(fired, Is.Zero);
            Assert.That(harness.ActiveSidePanel, Is.EqualTo(SidePanelContext.None));
        }

        // ── OnSidePanelChanged ───────────────────────────────────────────

        [Test]
        public void OnSidePanelChanged_FiresWithNewContext()
        {
            SidePanelContext? seen = null;
            harness.OnSidePanelChanged += ctx => seen = ctx;

            harness.SetSidePanel(SidePanelContext.Vendor);

            Assert.That(seen, Is.EqualTo(SidePanelContext.Vendor));
        }

        [Test]
        public void OnSidePanelChanged_FiresNone_WhenCleared()
        {
            harness.SetSidePanel(SidePanelContext.Stash);
            SidePanelContext? seen = null;
            harness.OnSidePanelChanged += ctx => seen = ctx;

            harness.ClearSidePanel(SidePanelContext.Stash);

            Assert.That(seen, Is.EqualTo(SidePanelContext.None));
        }
    }
}
