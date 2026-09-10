using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// XP settles per Encounter clear, not per kill (ADR-0010 second amendment). Each kill adds
    /// <c>archetype.Xp · (1 + (SourceLevel − heroLevel)/100)</c> to a per-Encounter pot; the pot
    /// pays out on the clear and resets; a Run driven off (hero down or
    /// <see cref="EncounterSimulation.Abandon"/>) before the clear forfeits the whole pot. That
    /// forfeit is the Encounter boundary's only player-visible effect — so it is tested here.
    /// </summary>
    [TestFixture]
    public sealed class EncounterXpTests
    {
        private static float SkirmisherXp(int sourceLevel, int heroLevel) =>
            EnemyArchetypes.Skirmisher.Xp.At(sourceLevel) * (1f + (sourceLevel - heroLevel) / 100f);

        private static FakeHero PicksThemOff(int level = 5) => new()
        {
            Level = level,
            PhysicalDamage = 1_000_000f,   // one Strike per kill
            AttackSpeed = 10f,
            MagicalDamage = 0f,
            Resource = 0f,
        };

        // A big beat keeps the cleared Encounter from rolling into the next one mid-test.
        private static EncounterTuning LongBeat() => new() { Beat = 100f, CastCadence = 0.05f };

        [Test]
        public void EachKill_AddsItsBalancedXpToThePot_NotToTheSettledTotal()
        {
            var sim = new EncounterSimulation(PicksThemOff(level: 5), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            sim.Advance(0.1f); // kill 1 of 3
            Assert.That(sim.UnsettledXp, Is.EqualTo(SkirmisherXp(5, 5)).Within(0.01f));
            Assert.That(sim.SettledXp, Is.EqualTo(0), "nothing settles until the clear");

            sim.Advance(0.1f); // kill 2 of 3
            Assert.That(sim.UnsettledXp, Is.EqualTo(2f * SkirmisherXp(5, 5)).Within(0.01f));
            Assert.That(sim.SettledXp, Is.EqualTo(0));
        }

        [Test]
        public void TheHeroLevelGap_BalancesThePot()
        {
            var under = new EncounterSimulation(PicksThemOff(level: 1), Profiles.Group(EnemyArchetype.Skirmisher, 3, sourceLevel: 5),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            under.Advance(0.1f); // one kill, hero 4 levels under the Location

            Assert.That(under.UnsettledXp, Is.EqualTo(SkirmisherXp(5, 1)).Within(0.01f));
            Assert.That(SkirmisherXp(5, 1), Is.GreaterThan(SkirmisherXp(5, 5)), "under-level is worth more");
        }

        [Test]
        public void ThePot_SettlesOnTheClear_AndResets()
        {
            var sim = new EncounterSimulation(PicksThemOff(level: 5), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            var settledByEvent = -1;
            sim.EncounterCleared += xp => settledByEvent = xp;

            for (var i = 0; i < 3; i++) sim.Advance(0.1f); // kill all 3 → clear

            var expected = (int)Math.Round(3f * SkirmisherXp(5, 5), MidpointRounding.AwayFromZero);
            Assert.That(sim.SettledXp, Is.EqualTo(expected));
            Assert.That(settledByEvent, Is.EqualTo(expected), "the clear event carries the settlement");
            Assert.That(sim.UnsettledXp, Is.EqualTo(0f), "the pot is spent");
            Assert.That(sim.ForfeitedXp, Is.EqualTo(0));
        }

        [Test]
        public void SettledXp_SumsAcrossClearedEncounters()
        {
            var sim = new EncounterSimulation(PicksThemOff(level: 5), Profiles.Group(EnemyArchetype.Skirmisher, 2),
                new ConstantRollSource(0f), Behaviours.Engaging(10), new EncounterTuning { Beat = 0.5f, CastCadence = 0.05f });

            for (var i = 0; i < 500 && sim.EncountersCleared < 3; i++) sim.Advance(0.1f);

            var perClear = (int)Math.Round(2f * SkirmisherXp(5, 5), MidpointRounding.AwayFromZero);
            Assert.That(sim.SettledXp, Is.EqualTo(3 * perClear));
        }

        [Test]
        public void Abandon_MidEncounter_ForfeitsThePot()
        {
            var sim = new EncounterSimulation(PicksThemOff(level: 5), Profiles.Group(EnemyArchetype.Skirmisher, 5),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            sim.Advance(0.1f);
            sim.Advance(0.1f); // 2 of 5 down — pot is live, Encounter not cleared
            var pot = sim.UnsettledXp;
            var expected = (int)Math.Round(pot, MidpointRounding.AwayFromZero);

            var forfeited = sim.Abandon();

            Assert.That(forfeited, Is.EqualTo(expected));
            Assert.That(sim.ForfeitedXp, Is.EqualTo(expected));
            Assert.That(sim.SettledXp, Is.EqualTo(0), "an abandoned Encounter settles nothing");
            Assert.That(sim.UnsettledXp, Is.EqualTo(0f));
        }

        [Test]
        public void HeroDown_MidEncounter_ForfeitsThePot()
        {
            var hero = PicksThemOff(level: 5);
            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Skirmisher, 5),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            sim.Advance(0.1f);
            sim.Advance(0.1f); // pot holds 2 kills' worth
            var expected = (int)Math.Round(sim.UnsettledXp, MidpointRounding.AwayFromZero);

            hero.PhysicalDamage = 0f; // stop killing so the down-tick adds nothing to the pot
            hero.Health = 0f;
            sim.Advance(0.1f);        // the sim notices the hero is down

            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Ended));
            Assert.That(sim.ForfeitedXp, Is.EqualTo(expected));
            Assert.That(sim.SettledXp, Is.EqualTo(0));
            Assert.That(sim.UnsettledXp, Is.EqualTo(0f));
        }

        [Test]
        public void APotThatRoundsToNothing_ForfeitsNothing()
        {
            var sim = new EncounterSimulation(PicksThemOff(level: 5), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            var forfeited = sim.Abandon(); // no kills yet

            Assert.That(forfeited, Is.EqualTo(0));
            Assert.That(sim.ForfeitedXp, Is.EqualTo(0));
        }
    }
}
