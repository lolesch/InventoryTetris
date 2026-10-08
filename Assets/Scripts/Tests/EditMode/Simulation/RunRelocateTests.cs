using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// <see cref="RunState.Relocate"/> — switching Location mid-Run without a Recall. The Run
    /// stays <c>InField</c> and never passes through Town, the live Encounter is swapped for a
    /// fresh one at the new Location, and the Run's totals carry on into the one
    /// <see cref="RunResult"/> it eventually freezes.
    /// </summary>
    [TestFixture]
    public sealed class RunRelocateTests
    {
        private static FakeHero OneShotHero() => new()
        {
            PhysicalDamage = 1_000_000f,
            AttackSpeed = 10f,
            MagicalDamage = 0f,
            Resource = 0f,
            Level = 5,
        };

        private static FakeHero FrailHero() => new()
        {
            MaxHealth = 1f,
            Health = 1f,
            PhysicalDamage = 0f,
            MagicalDamage = 0f,
            Resource = 0f,
            Level = 5,
        };

        private static EncounterTuning LongBeat() => new() { Beat = 100f, CastCadence = 0.05f };

        private static RunState NewRun(IHeroCombatant hero, EncounterTuning tuning = null, HeroBehaviour behaviour = null) =>
            new(profile => new EncounterSimulation(
                hero, profile, new ConstantRollSource(0f), behaviour ?? Behaviours.Engaging(10), tuning ?? LongBeat()));

        private static EncounterProfile Skirmishers(int count, int sourceLevel = 5) =>
            Profiles.Group(EnemyArchetype.Skirmisher, count, sourceLevel);

        // ─── the swap ────────────────────────────────────────────────────────

        [Test]
        public void Relocate_StaysInField_WithTheNewLocationAndAFreshEncounter()
        {
            var run = NewRun(OneShotHero());
            var first = Skirmishers(3);
            var second = Skirmishers(4, sourceLevel: 9);
            run.Send(first);
            var oldEncounter = run.Encounter;

            run.Relocate(second);

            Assert.That(run.Phase, Is.EqualTo(RunPhase.InField));
            Assert.That(run.Location, Is.SameAs(second));
            Assert.That(run.Encounter, Is.Not.SameAs(oldEncounter));
            Assert.That(run.Encounter.Profile, Is.SameAs(second));
            Assert.That(run.Encounter.CurrentEncounter, Is.EqualTo(1), "a fresh Encounter sequence");
            Assert.That(run.Encounter.AliveEnemyCount, Is.EqualTo(4));
        }

        [Test]
        public void Relocate_StopsTheOldEncounter()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(3));
            var oldEncounter = run.Encounter;

            run.Relocate(Skirmishers(3));

            Assert.That(oldEncounter.Phase, Is.EqualTo(SimulationPhase.Ended));
        }

        [Test]
        public void Relocate_IsNotARecall_NoRunEnded_NoPhaseChange_ButRelocatedIsRaised()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(3));

            var ended = 0;
            var phaseChanges = 0;
            var relocated = 0;
            run.RunEnded += () => ended++;
            run.PhaseChanged += _ => phaseChanges++;
            run.Relocated += () => relocated++;

            run.Relocate(Skirmishers(3));

            Assert.That(ended, Is.EqualTo(0));
            Assert.That(phaseChanges, Is.EqualTo(0), "the Run never passes through Town");
            Assert.That(relocated, Is.EqualTo(1));
            Assert.That(run.LastResult, Is.Null, "no Run finished, so there is no result yet");
        }

        [Test]
        public void TheRelocatedEncounter_IsLive_AndItsEventsReachTheRun()
        {
            var run = NewRun(FrailHero());
            run.Send(Skirmishers(4));
            run.Relocate(Skirmishers(4));

            for (var i = 0; i < 200 && !run.HeroIsDown; i++)
                run.Advance(0.1f);

            Assert.That(run.HeroIsDown, Is.True, "the new Encounter's HeroDowned is wired to the Run");
        }

        // ─── totals carry on ─────────────────────────────────────────────────

        [Test]
        public void TheRunTotals_CarryAcrossARelocate_IntoTheOneResult()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(5));

            run.Advance(0.1f);
            run.Advance(0.1f); // 2 kills at the first Location
            run.BankCurrency(300);
            var xpBefore = run.Encounter.SettledXp;
            var killsBefore = run.Encounter.EnemiesDefeated;
            var durationBefore = run.Encounter.Duration;
            Assert.That(xpBefore, Is.GreaterThan(0));

            run.Relocate(Skirmishers(5));
            run.Advance(0.1f); // 1 kill at the second
            run.BankCurrency(50);
            var xpAfter = run.Encounter.SettledXp;
            var durationAfter = run.Encounter.Duration;

            var result = run.Recall();

            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Recalled));
            Assert.That(result.XpSettled, Is.EqualTo(xpBefore + xpAfter));
            Assert.That(result.EnemiesDefeated, Is.EqualTo(killsBefore + 1));
            Assert.That(result.CurrencyBanked, Is.EqualTo(350), "the Run's take is not reset by a Relocate");
            Assert.That(result.Duration, Is.EqualTo(durationBefore + durationAfter).Within(0.0001f));
        }

        [Test]
        public void ClearsAtTheFirstLocation_StayCounted_AfterARelocate()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(2));

            run.Advance(0.1f);
            run.Advance(0.1f); // both fall → clear
            Assert.That(run.Encounter.EncountersCleared, Is.EqualTo(1));

            run.Relocate(Skirmishers(2));
            var result = run.Recall();

            Assert.That(result.EncountersCleared, Is.EqualTo(1));
        }

        [Test]
        public void ADeathAfterARelocate_CarriesTheEarlierTotals_PlusThePenalty()
        {
            var hero = OneShotHero();
            var run = new RunState(
                profile => new EncounterSimulation(hero, profile, new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat()),
                new RunPenalty(0.5f, 0.5f));
            run.Send(Skirmishers(5));
            run.Advance(0.1f); // 1 kill
            run.BankCurrency(200);
            var earned = run.Encounter.SettledXp;

            run.Relocate(Skirmishers(5));
            hero.PhysicalDamage = 0f;
            hero.Health = 0f;
            for (var i = 0; i < 20 && !run.HeroIsDown; i++) run.Advance(0.1f);

            var result = run.HandleDeath(xpTowardNextLevel: 100);

            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Died));
            Assert.That(result.XpSettled, Is.EqualTo(earned));
            Assert.That(result.EnemiesDefeated, Is.EqualTo(1));
            Assert.That(result.CurrencyBanked, Is.EqualTo(200));
            Assert.That(result.CurrencyFee, Is.EqualTo(100));
            Assert.That(result.XpLost, Is.EqualTo(50));
        }

        [Test]
        public void TheCarriedTotals_DoNotLeakIntoTheNextRun()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(5));
            run.Advance(0.1f);
            run.Relocate(Skirmishers(5));
            run.Recall();

            run.Send(Skirmishers(5));
            var result = run.Recall();

            Assert.That(result.XpSettled, Is.EqualTo(0));
            Assert.That(result.EnemiesDefeated, Is.EqualTo(0));
            Assert.That(result.Duration, Is.EqualTo(0f));
        }

        [Test]
        public void ARecallRequestedByTheOldEncounter_DoesNotFollowTheRunToTheNewLocation()
        {
            var behaviour = Behaviours.Engaging(10);
            behaviour.RetreatHealthFraction = 0.3f;
            var hero = new FakeHero
            {
                MaxHealth = 100f, Health = 25f, PhysicalDamage = 0f, MagicalDamage = 0f,
                AttackSpeed = 0.01f, ArmorPercent = 100f, MagicResistPercent = 100f, Resource = 0f, Level = 5,
            };
            var run = NewRun(hero, behaviour: behaviour);
            run.Send(Skirmishers(3));
            run.Advance(0.1f);
            Assert.That(run.RecallRequested, Is.True, "setup: the retreat trigger fired");

            hero.Health = 100f; // the hero is healthy again by the time the player switches
            run.Relocate(Skirmishers(3));

            Assert.That(run.RecallRequested, Is.False);
        }

        // ─── refusals ────────────────────────────────────────────────────────

        [Test]
        public void Relocate_WhileInTown_Throws()
        {
            var run = NewRun(OneShotHero());

            Assert.That(() => run.Relocate(Skirmishers(3)), Throws.InvalidOperationException);
        }

        [Test]
        public void Relocate_WhileTheHeroIsDown_Throws_AndLeavesTheRunAlone()
        {
            var run = NewRun(FrailHero());
            run.Send(Skirmishers(4));
            var encounter = run.Encounter;
            for (var i = 0; i < 200 && !run.HeroIsDown; i++) run.Advance(0.1f);
            Assert.That(run.HeroIsDown, Is.True, "setup failed to down the hero");

            Assert.That(() => run.Relocate(Skirmishers(4)), Throws.InvalidOperationException,
                "a downed hero's Run ends in HandleDeath");
            Assert.That(run.Encounter, Is.SameAs(encounter));
        }

        [Test]
        public void Relocate_WithNoLocation_Throws()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(3));

            Assert.That(() => run.Relocate(null), Throws.ArgumentNullException);
        }

        [Test]
        public void AnEncounterFactoryThatThrows_LeavesTheCurrentEncounterRunning_WithItsTotals()
        {
            var calls = 0;
            var hero = OneShotHero();
            var run = new RunState(profile =>
            {
                if (++calls == 2) throw new InvalidOperationException("no player");
                return new EncounterSimulation(hero, profile, new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat());
            });
            var first = Skirmishers(5);
            run.Send(first);
            run.Advance(0.1f);
            var encounter = run.Encounter;

            Assert.That(() => run.Relocate(Skirmishers(5)), Throws.InvalidOperationException);

            Assert.That(run.Encounter, Is.SameAs(encounter));
            Assert.That(run.Location, Is.SameAs(first));
            Assert.That(encounter.Phase, Is.Not.EqualTo(SimulationPhase.Ended), "the old Encounter was not stopped");
            Assert.That(run.Recall().EnemiesDefeated, Is.EqualTo(1));
        }

        [Test]
        public void AnEncounterFactoryThatReturnsNull_FailsTheRelocate()
        {
            var calls = 0;
            var hero = OneShotHero();
            var run = new RunState(profile => ++calls == 1
                ? new EncounterSimulation(hero, profile, new ConstantRollSource(0f), Behaviours.Engaging(10), LongBeat())
                : null);
            run.Send(Skirmishers(3));

            Assert.That(() => run.Relocate(Skirmishers(3)), Throws.InvalidOperationException);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.InField));
        }

        [Test]
        public void ARunCanBeRelocatedRepeatedly()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(5));

            for (var i = 0; i < 4; i++)
            {
                run.Advance(0.1f); // 1 kill per Location
                run.Relocate(Skirmishers(5));
            }
            var result = run.Recall();

            Assert.That(result.EnemiesDefeated, Is.EqualTo(4));
        }
    }
}
