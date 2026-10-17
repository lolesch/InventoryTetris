using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// Where the Town hero figure stands when it has not walked there (issue #328, the start-point table of
    /// <c>dev/specs/2026-10-16-hero-travel-design.md</c>): at the Town Exit after a Return, at the Waypoint after
    /// anything else. The Field figure has no rule: it always comes up at the Field Exit.
    /// </summary>
    [TestFixture]
    public sealed class TownStartPointTests
    {
        [Test]
        public void AfterAReturn_TheFigureStandsAtTheTownExit()
        {
            Assert.That(TownStartPoint.For(TownEntry.Return), Is.EqualTo(TownStand.TownExit));
        }

        [TestCase(TownEntry.Play)]
        [TestCase(TownEntry.Load)]
        [TestCase(TownEntry.Respawn)]
        public void EnteringTheGame_ALoad_OrARespawn_TheFigureStandsAtTheWaypoint(TownEntry entry)
        {
            Assert.That(TownStartPoint.For(entry), Is.EqualTo(TownStand.Waypoint));
        }
    }
}
