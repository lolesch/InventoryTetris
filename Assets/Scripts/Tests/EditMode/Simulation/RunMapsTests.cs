using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// Which map is up for a Run phase (ADR-0022, issue #367): the Town map whenever the Run is none or held, so a
    /// held Run's hero can use the Town Stops; the Field map only while the Run is live.
    /// </summary>
    [TestFixture]
    public sealed class RunMapsTests
    {
        [TestCase(RunPhase.None, true)]
        [TestCase(RunPhase.Held, true)]
        [TestCase(RunPhase.Live, false)]
        public void TheTownMapIsUp_WheneverTheRunIsNotLive(RunPhase phase, bool townUp)
        {
            Assert.That(phase.TownMapIsUp(), Is.EqualTo(townUp));
        }

        [TestCase(RunPhase.None, false)]
        [TestCase(RunPhase.Held, false)]
        [TestCase(RunPhase.Live, true)]
        public void TheFieldExit_ReturnsOnlyALiveRun(RunPhase phase, bool returns)
        {
            Assert.That(phase.FieldExitReturns(), Is.EqualTo(returns));
        }
    }
}
