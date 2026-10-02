using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// XP is delivered per kill, the moment the body falls — no per-Encounter pot. Each kill is
    /// worth <c>archetype.Xp · (1 + (SourceLevel − heroLevel)/100)</c>, rounded to whole XP per
    /// kill and raised on <see cref="EncounterSimulation.XpGained"/>. Nothing waits for a clear,
    /// so nothing carries over one and an exit (hero down, <see cref="EncounterSimulation.Abandon"/>)
    /// forfeits nothing.
    /// </summary>
    [TestFixture]
    public sealed class EncounterXpTests
    {
        private static float SkirmisherXp(int sourceLevel, int heroLevel) =>
            EnemyArchetypes.Skirmisher.Xp.At(sourceLevel) * (1f + (sourceLevel - heroLevel) / 100f);

        private static int Whole(float xp) => (int)Math.Round(xp, MidpointRounding.AwayFromZero);

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
        public void EachKill_DeliversItsBalancedXp_TheTickItFalls()
        {
            var sim = new EncounterSimulation(PicksThemOff(level: 5), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            var gained = 0;
            sim.XpGained += xp => gained += xp;

            sim.Advance(0.1f); // kill 1 of 3 — mid-Encounter, nothing cleared
            Assert.That(sim.EncountersCleared, Is.EqualTo(0));
            Assert.That(sim.SettledXp, Is.EqualTo(Whole(SkirmisherXp(5, 5))));
            Assert.That(gained, Is.EqualTo(sim.SettledXp), "the event carried the same XP");

            sim.Advance(0.1f); // kill 2 of 3
            Assert.That(sim.SettledXp, Is.EqualTo(2 * Whole(SkirmisherXp(5, 5))));
            Assert.That(gained, Is.EqualTo(sim.SettledXp));
        }

        [Test]
        public void TheHeroLevelGap_BalancesEachKill()
        {
            var under = new EncounterSimulation(PicksThemOff(level: 1), Profiles.Group(EnemyArchetype.Skirmisher, 3, sourceLevel: 5),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            under.Advance(0.1f); // one kill, hero 4 levels under the Location

            Assert.That(under.SettledXp, Is.EqualTo(Whole(SkirmisherXp(5, 1))));
            Assert.That(SkirmisherXp(5, 1), Is.GreaterThan(SkirmisherXp(5, 5)), "under-level is worth more");
        }

        [Test]
        public void XpGained_IsRaisedBeforeEnemyDefeated()
        {
            var sim = new EncounterSimulation(PicksThemOff(level: 5), Profiles.Solo(EnemyArchetype.Skirmisher),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            var order = "";
            sim.XpGained += _ => order += "x";
            sim.EnemyDefeated += _ => order += "d";

            sim.Advance(0.1f);

            Assert.That(order, Is.EqualTo("xd"));
        }

        [Test]
        public void TheClear_AddsNothing_BecauseEveryKillAlreadyDelivered()
        {
            var sim = new EncounterSimulation(PicksThemOff(level: 5), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            var gained = 0;
            sim.XpGained += xp => gained += xp;

            for (var i = 0; i < 2; i++) sim.Advance(0.1f);
            var beforeClear = sim.SettledXp;
            Assert.That(sim.EncountersCleared, Is.EqualTo(0));

            sim.Advance(0.1f); // the last kill → clear

            Assert.That(sim.EncountersCleared, Is.EqualTo(1));
            Assert.That(sim.SettledXp, Is.EqualTo(beforeClear + Whole(SkirmisherXp(5, 5))), "only the last kill's share arrived on the clear tick");
            Assert.That(gained, Is.EqualTo(3 * Whole(SkirmisherXp(5, 5))));
        }

        [Test]
        public void SettledXp_SumsAcrossClearedEncounters()
        {
            var sim = new EncounterSimulation(PicksThemOff(level: 5), Profiles.Group(EnemyArchetype.Skirmisher, 2),
                new ConstantRollSource(0f), Behaviours.Engaging(10), new EncounterTuning { Beat = 0.5f, CastCadence = 0.05f });

            for (var i = 0; i < 500 && sim.EncountersCleared < 3; i++) sim.Advance(0.1f);

            Assert.That(sim.SettledXp, Is.EqualTo(3 * 2 * Whole(SkirmisherXp(5, 5))));
        }

        [Test]
        public void Abandon_MidEncounter_KeepsTheXpAlreadyEarned_AndForfeitsNothing()
        {
            var sim = new EncounterSimulation(PicksThemOff(level: 5), Profiles.Group(EnemyArchetype.Skirmisher, 5),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            sim.Advance(0.1f);
            sim.Advance(0.1f); // 2 of 5 down, Encounter not cleared
            var earned = sim.SettledXp;
            Assert.That(earned, Is.GreaterThan(0));

            sim.Abandon();

            Assert.That(sim.SettledXp, Is.EqualTo(earned), "an exit takes nothing back");
            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Ended));
        }

        [Test]
        public void HeroDown_MidEncounter_KeepsTheXpAlreadyEarned()
        {
            var hero = PicksThemOff(level: 5);
            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Skirmisher, 5),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            sim.Advance(0.1f);
            sim.Advance(0.1f);
            var earned = sim.SettledXp;

            hero.PhysicalDamage = 0f; // stop killing so the down-tick adds nothing
            hero.Health = 0f;
            sim.Advance(0.1f);        // the sim notices the hero is down

            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Ended));
            Assert.That(sim.SettledXp, Is.EqualTo(earned));
        }

        [Test]
        public void ALevelGapThatZeroesTheBalance_DeliversNothing_AndRaisesNoEvent()
        {
            // The (1 + gap/100) term goes to <= 0 when the hero is 100+ levels over the Location.
            var sim = new EncounterSimulation(PicksThemOff(level: 200), Profiles.Solo(EnemyArchetype.Skirmisher, sourceLevel: 5),
                new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());

            var raised = 0;
            sim.XpGained += _ => raised++;

            sim.Advance(0.1f);

            Assert.That(sim.EnemiesDefeated, Is.EqualTo(1));
            Assert.That(sim.SettledXp, Is.EqualTo(0));
            Assert.That(raised, Is.EqualTo(0));
        }
    }
}
