using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>The beat a portal takes to open before the hero goes through it, or comes out of it (ADR-0022).</summary>
    public sealed class PortalAppearTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0.3f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(0.7f, 1f)]
        [TestCase(5f, 1f)]
        public void Progress_WaitsTheDelay_ThenGrowsLinearly(float elapsed, float expected) =>
            Assert.That(PortalAppear.Progress(elapsed, 0.3f, 0.4f), Is.EqualTo(expected).Within(1e-5f));

        [Test]
        public void Progress_NoDelay_StartsGrowingAtOnce() =>
            Assert.That(PortalAppear.Progress(0.2f, 0f, 0.4f), Is.EqualTo(0.5f).Within(1e-5f));

        [Test]
        public void Progress_NoGrowth_IsOpenOnceTheDelayIsOver()
        {
            Assert.That(PortalAppear.Progress(0.1f, 0.3f, 0f), Is.EqualTo(0f));
            Assert.That(PortalAppear.Progress(0.3f, 0.3f, 0f), Is.EqualTo(1f));
        }

        [Test]
        public void Scale_StartsNarrowAndHalfTall()
        {
            var (x, y) = PortalAppear.Scale(0f);

            Assert.That(x, Is.EqualTo(0f));
            Assert.That(y, Is.EqualTo(0.5f));
        }

        [Test]
        public void Scale_HalfWayIsHalfWide_AndThreeQuartersTall()
        {
            var (x, y) = PortalAppear.Scale(0.5f);

            Assert.That(x, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(y, Is.EqualTo(0.75f).Within(1e-5f));
        }

        [Test]
        public void Scale_WhenOpen_IsWhole()
        {
            var (x, y) = PortalAppear.Scale(1f);

            Assert.That(x, Is.EqualTo(1f));
            Assert.That(y, Is.EqualTo(1f));
        }
    }
}
