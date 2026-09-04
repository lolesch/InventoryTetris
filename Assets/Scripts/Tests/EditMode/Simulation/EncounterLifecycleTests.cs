using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// An Encounter clears when its Roster is spent and the last body falls — then a beat, then
    /// the next; the sim runs Encounters endlessly. Only the hero going down (or an external
    /// <see cref="EncounterSimulation.Abandon"/>) ends it. <c>EnemiesDefeated</c> and
    /// <c>Duration</c> report across the whole multi-Encounter sequence.
    /// </summary>
    [TestFixture]
    public sealed class EncounterLifecycleTests
    {
        private static FakeHero OneShotHero() => new()
        {
            PhysicalDamage = 1_000_000f,
            AttackSpeed = 10f,      // a Strike every tick
            MagicalDamage = 0f,
            Resource = 0f,
        };

        private static EncounterTuning ShortBeat() => new() { Beat = 0.5f, CastCadence = 0.05f };

        [Test]
        public void Encounter_ClearsWhenTheRosterIsSpentAndTheLastEnemyFalls()
        {
            var sim = new EncounterSimulation(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher),
                new ConstantRollSource(0f), engagementTarget: 5, ShortBeat());

            var clears = 0;
            sim.EncounterCleared += _ => clears++;

            sim.Advance(0.1f); // one tick: Strike kills the lone enemy → clear

            Assert.That(clears, Is.EqualTo(1));
            Assert.That(sim.EncountersCleared, Is.EqualTo(1));
            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Beat));
        }

        [Test]
        public void AfterTheBeat_TheNextEncounterBuilds()
        {
            var sim = new EncounterSimulation(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher),
                new ConstantRollSource(0f), engagementTarget: 5, ShortBeat());

            sim.Advance(0.1f);                 // clears Encounter 1 → Beat
            Assert.That(sim.CurrentEncounter, Is.EqualTo(1));

            for (var i = 0; i < 5; i++) sim.Advance(0.1f); // 0.5 s beat elapses → Encounter 2 opens

            Assert.That(sim.CurrentEncounter, Is.EqualTo(2));
            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Fighting));
            Assert.That(sim.AliveEnemyCount, Is.EqualTo(1), "a fresh Roster");
        }

        [Test]
        public void EnemiesDefeated_AndEncountersCleared_AccumulateAcrossTheSequence()
        {
            // Each Encounter fields 3 Skirmishers; the hero clears one per tick, one beat between.
            var sim = new EncounterSimulation(OneShotHero(), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), engagementTarget: 10, ShortBeat());

            // Stop the instant the 4th Encounter clears — no partial next Encounter in the counts.
            for (var i = 0; i < 500 && sim.EncountersCleared < 4; i++) sim.Advance(0.1f);

            Assert.That(sim.EncountersCleared, Is.EqualTo(4));
            Assert.That(sim.EnemiesDefeated, Is.EqualTo(12), "3 bodies per cleared Encounter, back to the first");
        }

        [Test]
        public void Duration_IsTheTickQuantisedElapsedTime()
        {
            var sim = new EncounterSimulation(new FakeHero { PhysicalDamage = 0f, MagicalDamage = 0f, Resource = 0f },
                Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0f), engagementTarget: 5);

            sim.Advance(0.25f); // 2 ticks, 0.05 banked
            sim.Advance(0.25f); // 0.30 banked+new → 3 ticks
            sim.Advance(0.25f); // 2 ticks

            var quanta = sim.Duration / 0.1f;
            Assert.That(quanta, Is.EqualTo(Math.Round(quanta)).Within(0.0001f), "always a whole number of ticks");
            Assert.That(sim.Duration, Is.EqualTo(0.7f).Within(0.0001f));
        }

        [Test]
        public void HeroDown_EndsTheSim_AndFreezesTheClock()
        {
            var hero = new FakeHero { MaxHealth = 1f, Health = 1f, PhysicalDamage = 0f, MagicalDamage = 0f, Resource = 0f };
            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Skirmisher, 4),
                new ConstantRollSource(0f), engagementTarget: 10);

            var downed = false;
            sim.HeroDowned += () => downed = true;

            for (var i = 0; i < 100 && sim.Phase != SimulationPhase.Ended; i++) sim.Advance(0.1f);

            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Ended));
            Assert.That(downed, Is.True);

            var frozen = sim.Duration;
            Assert.That(sim.Advance(5f), Is.EqualTo(0), "an ended sim ignores further time");
            Assert.That(sim.Duration, Is.EqualTo(frozen));
        }

        [Test]
        public void Duration_FreezesAtTheHerosDeath_EvenWhenAdvanceOvershootsIt()
        {
            var hero = new FakeHero { MaxHealth = 1f, Health = 1f, PhysicalDamage = 0f, MagicalDamage = 0f, Resource = 0f };
            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Skirmisher, 4),
                new ConstantRollSource(0f), engagementTarget: 10);

            // One coarse Advance that spans past the first lethal enemy Strike (t = 0.7 s).
            sim.Advance(5f);

            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Ended));
            Assert.That(sim.Duration, Is.EqualTo(0.7f).Within(0.05f), "frozen at the death tick, not past it");
        }

        [Test]
        public void Abandon_EndsTheSim()
        {
            var sim = new EncounterSimulation(new FakeHero { PhysicalDamage = 0f, MagicalDamage = 0f, Resource = 0f },
                Profiles.Solo(EnemyArchetype.Brute), new ConstantRollSource(0f), engagementTarget: 5);

            sim.Advance(0.5f);
            var atAbandon = sim.Duration;

            sim.Abandon();

            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Ended));
            Assert.That(sim.Advance(1f), Is.EqualTo(0));
            Assert.That(sim.Duration, Is.EqualTo(atAbandon));
        }

        [Test]
        public void OnlyTheHeroGoingDownEndsTheSim_NotClearingAnEncounter()
        {
            var sim = new EncounterSimulation(OneShotHero(), Profiles.Group(EnemyArchetype.Skirmisher, 2),
                new ConstantRollSource(0f), engagementTarget: 10, ShortBeat());

            for (var i = 0; i < 300; i++) sim.Advance(0.1f);

            Assert.That(sim.Phase, Is.Not.EqualTo(SimulationPhase.Ended));
            Assert.That(sim.EncountersCleared, Is.GreaterThan(5), "it just keeps going");
        }
    }
}
