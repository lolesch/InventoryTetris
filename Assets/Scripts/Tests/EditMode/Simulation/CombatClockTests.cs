using System.Collections.Generic;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// Locks the fixed-tick contract behind the Encounter simulation (ADR-0008): the clock
    /// advances on a discrete interval decoupled from frame time, runs 0..N ticks per Advance with
    /// an accumulator that carries the remainder, and is therefore frame-rate independent — the
    /// same total elapsed time yields the same tick count regardless of how it is chunked.
    ///
    /// Ported from AutoBattler's <c>CombatClockTests</c>; the assertions are re-expressed in the
    /// NUnit constraint model this repo uses (FluentAssertions is not a dependency here).
    /// </summary>
    [TestFixture]
    public sealed class CombatClockTests
    {
        private const float Interval = 0.1f; // 10 ticks/sec

        [Test]
        public void SubIntervalAdvance_FiresNoTick_ButAccumulates()
        {
            var ticks = 0;
            var clock = new CombatClock(Interval);
            clock.OnTick += () => ticks++;

            Assert.That(clock.Advance(Interval * 0.6f), Is.EqualTo(0));
            Assert.That(ticks, Is.EqualTo(0));

            // The leftover 0.6 carries: another 0.6 crosses the interval exactly once.
            Assert.That(clock.Advance(Interval * 0.6f), Is.EqualTo(1));
            Assert.That(ticks, Is.EqualTo(1));
        }

        [Test]
        public void Advance_FiresOneTickPerWholeInterval()
        {
            var ticks = 0;
            var clock = new CombatClock(Interval);
            clock.OnTick += () => ticks++;

            Assert.That(clock.Advance(Interval * 2.5f), Is.EqualTo(2));
            Assert.That(ticks, Is.EqualTo(2));
        }

        [Test]
        public void Advance_IsFrameRateIndependent()
        {
            // One big step vs many small steps over the same total time → same tick count.
            // Kept within the per-Advance clamp (frame-rate independence is a within-budget
            // property; the spiral-of-death clamp deliberately breaks it for pathological frames,
            // covered separately by Advance_ClampsRunawayTicks).
            var coarse = 0;
            var clockCoarse = new CombatClock(Interval);
            clockCoarse.OnTick += () => coarse++;
            clockCoarse.Advance(Interval * 5f);

            var fine = 0;
            var clockFine = new CombatClock(Interval);
            clockFine.OnTick += () => fine++;
            for (var i = 0; i < 50; i++)
                clockFine.Advance(Interval * 0.1f);

            Assert.That(fine, Is.EqualTo(coarse));
            Assert.That(coarse, Is.EqualTo(5));
        }

        [Test]
        public void Advance_ClampsRunawayTicks_AgainstSpiralOfDeath()
        {
            var ticks = 0;
            var clock = new CombatClock(Interval, maxTicksPerAdvance: 5);
            clock.OnTick += () => ticks++;

            // A 100-interval hitch must not fire 100 ticks in one frame.
            Assert.That(clock.Advance(Interval * 100f), Is.EqualTo(5));
            Assert.That(ticks, Is.EqualTo(5));
        }

        [Test]
        public void Reset_DropsAccumulatedRemainder()
        {
            var ticks = 0;
            var clock = new CombatClock(Interval);
            clock.OnTick += () => ticks++;

            clock.Advance(Interval * 0.9f);
            clock.Reset();
            Assert.That(clock.Advance(Interval * 0.9f), Is.EqualTo(0)); // remainder was cleared, so still short
            Assert.That(ticks, Is.EqualTo(0));
        }

        [Test]
        public void TickInterval_IsExposed()
        {
            Assert.That(new CombatClock(Interval).TickInterval, Is.EqualTo(Interval));
        }

        // ----- ElapsedTime: the simulation timeline -----

        /// <summary>
        /// Simulated time is ticks × interval, not the raw deltas fed in. Mutation guard: accumulating
        /// deltaTime instead of the interval reports 0.25 here, not 0.2.
        /// </summary>
        [Test]
        public void ElapsedTime_CountsWholeTicksOnly_NotBankedRemainder()
        {
            var clock = new CombatClock(Interval);

            clock.Advance(Interval * 2.5f);

            Assert.That(clock.ElapsedTime, Is.EqualTo(Interval * 2f).Within(0.0001f));
        }

        [Test]
        public void ElapsedTime_StartsAtZero()
        {
            Assert.That(new CombatClock(Interval).ElapsedTime, Is.EqualTo(0f));
        }

        [Test]
        public void ElapsedTime_AccumulatesAcrossAdvances()
        {
            var clock = new CombatClock(Interval);

            clock.Advance(Interval);
            clock.Advance(Interval);
            clock.Advance(Interval);

            Assert.That(clock.ElapsedTime, Is.EqualTo(Interval * 3f).Within(0.0001f));
        }

        /// <summary>Each fight's timeline starts at 0:00, so restarting combat rewinds the clock.</summary>
        [Test]
        public void Reset_RewindsElapsedTime()
        {
            var clock = new CombatClock(Interval);
            clock.Advance(Interval * 3f);

            clock.Reset();

            Assert.That(clock.ElapsedTime, Is.EqualTo(0f));
        }

        /// <summary>
        /// A handler must see the time of the tick it is running in, not the previous one — otherwise
        /// every resolved event is stamped one tick early.
        /// </summary>
        [Test]
        public void ElapsedTime_IsAlreadyAdvanced_WhenTheTickHandlerRuns()
        {
            var seen  = new List<float>();
            var clock = new CombatClock(Interval);
            clock.OnTick += () => seen.Add(clock.ElapsedTime);

            clock.Advance(Interval * 3f);

            Assert.That(seen, Has.Count.EqualTo(3));
            Assert.That(seen[0], Is.EqualTo(Interval).Within(0.0001f));
            Assert.That(seen[2], Is.EqualTo(Interval * 3f).Within(0.0001f));
        }

        /// <summary>
        /// The spiral-of-death clamp drops surplus time. Elapsed must drop it too — otherwise a frame
        /// hitch teleports the timeline past events that never resolved.
        /// </summary>
        [Test]
        public void ElapsedTime_IgnoresTimeDroppedByTheHitchClamp()
        {
            var clock = new CombatClock(Interval, maxTicksPerAdvance: 5);

            clock.Advance(Interval * 100f);

            Assert.That(clock.ElapsedTime, Is.EqualTo(Interval * 5f).Within(0.0001f));
        }
    }
}
