using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>The vanish beat a hero goes through at a portal before the map changes (ADR-0022).</summary>
    public sealed class PortalVanishTests
    {
        [TestCase(0f, 1f)]
        [TestCase(0.25f, 0.5f)]
        [TestCase(0.5f, 0f)]
        [TestCase(2f, 0f)]
        public void Remaining_FallsLinearlyFromWholeToNothing(float elapsed, float expected) =>
            Assert.That(PortalVanish.Remaining(elapsed, 0.5f), Is.EqualTo(expected).Within(1e-5f));

        [Test]
        public void Remaining_NegativeElapsed_IsWhole() =>
            Assert.That(PortalVanish.Remaining(-1f, 0.5f), Is.EqualTo(1f));

        [Test]
        public void Remaining_ABeatOfNoLength_IsGoneAtOnce() =>
            Assert.That(PortalVanish.Remaining(0f, 0f), Is.EqualTo(0f));
    }
}
