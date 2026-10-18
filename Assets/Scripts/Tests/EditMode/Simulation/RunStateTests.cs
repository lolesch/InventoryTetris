using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The <see cref="RunState"/> FSM (issue #21). Two states — <c>InTown</c> and
    /// <c>InField</c>; <c>Send</c> opens a Run at a Location, <c>Return</c> and
    /// <c>HandleDeath</c> close it. On close it freezes a <see cref="RunResult"/> read off the
    /// live <see cref="EncounterSimulation"/>: a <c>Returned</c> result carries the full
    /// accumulation, a <c>Died</c> result adds the <c>XpLost</c> / <c>CurrencyFee</c> penalty.
    /// Loot and coins settle live during the Run (issue #24 feeds <see cref="RunState.BankCurrency"/>);
    /// the result is a summary, not a delivery.
    /// </summary>
    [TestFixture]
    public sealed class RunStateTests
    {
        // A hero that clears one Skirmisher per tick and never takes damage worth noting.
        private static FakeHero OneShotHero() => new()
        {
            MaxHealth = 1_000_000f,
            Health = 1_000_000f,
            PhysicalDamage = 1_000_000f,
            AttackSpeed = 10f,
            MagicalDamage = 0f,
            Resource = 0f,
            Level = 5,
        };

        // A hero guaranteed to go down on the Skirmishers' first landed Strike.
        private static FakeHero FrailHero() => new()
        {
            MaxHealth = 1f,
            Health = 1f,
            PhysicalDamage = 0f,
            MagicalDamage = 0f,
            Resource = 0f,
            Level = 5,
        };

        // Advances a Run until the hero is down — bounded, so a broken setup fails fast rather than hanging.
        private static void DriveHeroDown(RunState run)
        {
            for (var i = 0; i < 200 && !run.HeroIsDown; i++)
                run.Advance(0.1f);
            Assert.That(run.HeroIsDown, Is.True, "setup failed to down the hero within the iteration budget");
        }

        private static EncounterTuning ShortBeat() => new() { Beat = 0.5f, CastCadence = 0.05f };
        private static EncounterTuning LongBeat() => new() { Beat = 100f, CastCadence = 0.05f };

        private static RunState NewRun(
            IHeroCombatant hero,
            RunPenalty penalty = default,
            EncounterTuning tuning = null,
            int engagement = 10,
            HeroBehaviour behaviour = null,
            float recallOpening = 0f) =>
            new(profile => new EncounterSimulation(
                    hero, profile, new ConstantRollSource(0f),
                    behaviour ?? Behaviours.Engaging(engagement), tuning ?? new EncounterTuning()),
                penalty, recallOpening);

        private static EncounterProfile Skirmishers(int count) =>
            Profiles.Group(EnemyArchetype.Skirmisher, count, sourceLevel: 5);

        // ─── transitions ─────────────────────────────────────────────────────

        [Test]
        public void ANewRunState_StartsWithNoRun_WithNoLocationAndNoEncounter()
        {
            var run = NewRun(OneShotHero());

            Assert.That(run.Phase, Is.EqualTo(RunPhase.None));
            Assert.That(run.Location, Is.Null);
            Assert.That(run.Encounter, Is.Null);
            Assert.That(run.LastResult, Is.Null);
        }

        [Test]
        public void Send_GoesLive_WithTheLocationAndALiveEncounter()
        {
            var run = NewRun(OneShotHero());
            var thornwood = Skirmishers(3);

            run.Send(thornwood);

            Assert.That(run.Phase, Is.EqualTo(RunPhase.Live));
            Assert.That(run.Location, Is.SameAs(thornwood));
            Assert.That(run.Encounter, Is.Not.Null);
            Assert.That(run.Encounter.AliveEnemyCount, Is.GreaterThan(0));
        }

        [Test]
        public void Return_ReturnsToTown_AndDropsTheLocationAndEncounter()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(3));

            run.Return();

            Assert.That(run.Phase, Is.EqualTo(RunPhase.None));
            Assert.That(run.Location, Is.Null);
            Assert.That(run.Encounter, Is.Null);
        }

        [Test]
        public void HandleDeath_ReturnsToTown()
        {
            var run = NewRun(FrailHero());
            run.Send(Skirmishers(4));
            DriveHeroDown(run);

            run.HandleDeath(xpTowardNextLevel: 0);

            Assert.That(run.Phase, Is.EqualTo(RunPhase.None));
            Assert.That(run.Location, Is.Null);
            Assert.That(run.Encounter, Is.Null);
        }

        [Test]
        public void Send_WhileLive_Throws()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(3));

            Assert.That(() => run.Send(Skirmishers(3)), Throws.InvalidOperationException);
        }

        [Test]
        public void Send_WhileHeroIsDown_Throws()
        {
            var run = NewRun(FrailHero());
            run.Send(Skirmishers(4));
            DriveHeroDown(run);

            Assert.That(() => run.Send(Skirmishers(4)), Throws.InvalidOperationException,
                "the hero is down — the Run ends in HandleDeath before a new one can be Sent");
        }

        [Test]
        public void Return_WhileNone_Throws()
        {
            var run = NewRun(OneShotHero());

            Assert.That(() => run.Return(), Throws.InvalidOperationException);
        }

        [Test]
        public void HandleDeath_WhileNone_Throws()
        {
            var run = NewRun(OneShotHero());

            Assert.That(() => run.HandleDeath(100), Throws.InvalidOperationException);
        }

        [Test]
        public void HandleDeath_WhileTheHeroIsStillAlive_Throws()
        {
            var run = NewRun(OneShotHero()); // huge HP — never goes down
            run.Send(Skirmishers(3));

            Assert.That(() => run.HandleDeath(100), Throws.InvalidOperationException,
                "a live hero's Run only ends in Return — HandleDeath is not a 'give up' button");
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Live), "the refused HandleDeath must not half-end the Run");
        }

        [Test]
        public void Advance_WhileNone_IsANoOp()
        {
            var run = NewRun(OneShotHero());

            Assert.That(run.Advance(1f), Is.EqualTo(0));
        }

        [Test]
        public void Send_WithNoLocation_Throws()
        {
            var run = NewRun(OneShotHero());

            Assert.That(() => run.Send(null), Throws.ArgumentNullException);
        }

        [Test]
        public void ANullEncounterFactory_IsRejectedAtConstruction()
        {
            Assert.That(() => new RunState(null), Throws.ArgumentNullException);
        }

        [Test]
        public void AnEncounterFactoryThatReturnsNull_FailsTheSend()
        {
            var run = new RunState(_ => null);

            Assert.That(() => run.Send(Skirmishers(3)), Throws.InvalidOperationException);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.None), "a failed Send leaves the Run in Town");
        }

        [Test]
        public void HandleDeath_WithNegativeProgress_Throws()
        {
            var run = NewRun(FrailHero());
            run.Send(Skirmishers(4));
            DriveHeroDown(run);

            Assert.That(() => run.HandleDeath(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void HandleDeath_StoresItsResultAsLastResult()
        {
            var run = NewRun(FrailHero(), penalty: new RunPenalty(0.2f, 0.2f));
            run.Send(Skirmishers(4));
            DriveHeroDown(run);

            var result = run.HandleDeath(500);

            Assert.That(run.LastResult, Is.EqualTo(result));
        }

        [Test]
        public void HandleDeath_RecordsWhereTheHeroFell()
        {
            var tuning = new EncounterTuning { Arena = new ArenaTuning { Origin = new Submodules.Utility.Extensions.Coordinate(7f, -3f) } };
            var run = NewRun(FrailHero(), tuning: tuning);
            run.Send(Skirmishers(4));
            DriveHeroDown(run);
            var fell = run.Encounter.HeroPosition;
            Assert.That(fell, Is.Not.EqualTo(default(Submodules.Utility.Extensions.Coordinate)), "setup: the hero fell away from the world's zero");

            var result = run.HandleDeath(0);

            Assert.That(result.FallPosition, Is.EqualTo(fell));
        }

        [Test]
        public void Send_ClearsThePreviousRunsLastResult()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(3));
            run.Return();
            Assert.That(run.LastResult, Is.Not.Null, "sanity check — the first Run did finish");

            run.Send(Skirmishers(3));

            Assert.That(run.LastResult, Is.Null, "a summary from two Runs ago has no business surviving into this one");
        }

        // ─── accumulation ────────────────────────────────────────────────────

        [Test]
        public void KillingAcrossEncounters_AccumulatesXp_IntoTheRunTotal()
        {
            var run = NewRun(OneShotHero(), tuning: ShortBeat());
            run.Send(Profiles.Group(EnemyArchetype.Skirmisher, 2, population: 3));

            for (var i = 0; i < 500 && run.Encounter.EncountersCleared < 3; i++)
                run.Advance(0.1f);

            var settledMidRun = run.Encounter.SettledXp;
            Assert.That(settledMidRun, Is.GreaterThan(0));

            var result = run.Return();
            Assert.That(result.XpSettled, Is.EqualTo(settledMidRun));
            Assert.That(result.EncountersCleared, Is.EqualTo(3));
        }

        [Test]
        public void BankCurrency_AccumulatesAcrossKills_IntoCurrencyBanked()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(3));

            run.BankCurrency(500);
            run.BankCurrency(250);

            Assert.That(run.CurrencyBanked, Is.EqualTo(750));

            var result = run.Return();
            Assert.That(result.CurrencyBanked, Is.EqualTo(750));
        }

        [Test]
        public void BankCurrency_WhileNone_Throws()
        {
            var run = NewRun(OneShotHero());

            Assert.That(() => run.BankCurrency(100), Throws.InvalidOperationException);
        }

        [Test]
        public void BankCurrency_WithANegativeAmount_Throws()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(3));

            Assert.That(() => run.BankCurrency(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void BankCurrency_AfterTheHeroIsDown_Throws()
        {
            var run = NewRun(FrailHero());
            run.Send(Skirmishers(4));
            DriveHeroDown(run);

            Assert.That(() => run.BankCurrency(100), Throws.InvalidOperationException,
                "the Run's take is frozen once the hero is down, for HandleDeath to read");
        }

        // ─── the Returned outcome ────────────────────────────────────────────

        [Test]
        public void Return_FreezesAReturnedResult_CarryingTheFullAccumulation()
        {
            var run = NewRun(OneShotHero(), tuning: ShortBeat());
            run.Send(Skirmishers(2));

            for (var i = 0; i < 500 && run.Encounter.EncountersCleared < 2; i++)
                run.Advance(0.1f);

            run.BankCurrency(1234);
            var cleared = run.Encounter.EncountersCleared;
            var settled = run.Encounter.SettledXp;
            var defeated = run.Encounter.EnemiesDefeated;
            var duration = run.Encounter.Duration;

            var result = run.Return();

            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Returned));
            Assert.That(result.XpSettled, Is.EqualTo(settled));
            Assert.That(result.CurrencyBanked, Is.EqualTo(1234));
            Assert.That(result.EnemiesDefeated, Is.EqualTo(defeated));
            Assert.That(result.EncountersCleared, Is.EqualTo(cleared));
            Assert.That(result.Duration, Is.EqualTo(duration).Within(0.0001f));
            Assert.That(result.XpLost, Is.EqualTo(0), "no penalty on a Return");
            Assert.That(result.CurrencyFee, Is.EqualTo(0), "no penalty on a Return");
            Assert.That(run.LastResult, Is.EqualTo(result));
        }

        [Test]
        public void Return_MidEncounter_KeepsTheXpOfTheKillsSoFar()
        {
            var run = NewRun(OneShotHero(), tuning: LongBeat());
            run.Send(Skirmishers(5));

            run.Advance(0.1f);
            run.Advance(0.1f); // 2 of 5 down, Encounter not cleared
            var earned = run.Encounter.SettledXp;
            Assert.That(earned, Is.GreaterThan(0));

            var result = run.Return();

            Assert.That(result.XpSettled, Is.EqualTo(earned), "XP is per kill — nothing waits on a clear");
            Assert.That(result.EncountersCleared, Is.EqualTo(0));
        }

        // ─── the Died outcome ────────────────────────────────────────────────

        [Test]
        public void HandleDeath_FreezesADiedResult_WithTheXpAndCurrencyPenaltyApplied()
        {
            var run = NewRun(FrailHero(), penalty: new RunPenalty(xpLossFraction: 0.25f, currencyFeeFraction: 0.10f));
            run.Send(Skirmishers(4));
            run.BankCurrency(1000); // must bank before the hero goes down — BankCurrency refuses after
            DriveHeroDown(run);

            var result = run.HandleDeath(xpTowardNextLevel: 800);

            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Died));
            Assert.That(result.XpLost, Is.EqualTo(200), "0.25 of 800 progress to the next level");
            Assert.That(result.CurrencyFee, Is.EqualTo(100), "0.10 of the 1000 banked this Run");
            Assert.That(result.CurrencyBanked, Is.EqualTo(1000), "the fee does not reduce the banked figure — it is a separate line");
        }

        [Test]
        public void HandleDeath_WithTheDefaultPenalty_CostsNothingExtra()
        {
            var run = NewRun(FrailHero());
            run.Send(Skirmishers(4));
            run.BankCurrency(1000);
            DriveHeroDown(run);

            var result = run.HandleDeath(xpTowardNextLevel: 800);

            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Died));
            Assert.That(result.XpLost, Is.EqualTo(0));
            Assert.That(result.CurrencyFee, Is.EqualTo(0));
        }

        [Test]
        public void HandleDeath_CurrencyFee_NeverExceedsWhatWasBanked()
        {
            var run = NewRun(FrailHero(), penalty: new RunPenalty(0f, 1f));
            run.Send(Skirmishers(4));
            run.BankCurrency(500);
            DriveHeroDown(run);

            var result = run.HandleDeath(0);

            Assert.That(result.CurrencyFee, Is.EqualTo(500));
        }

        [Test]
        public void WhenTheHeroIsDowned_TheRunReportsIt_AndReturnIsRefused()
        {
            var run = NewRun(FrailHero());
            run.Send(Skirmishers(4));
            DriveHeroDown(run);

            Assert.That(run.Encounter.Phase, Is.EqualTo(SimulationPhase.Ended));
            Assert.That(() => run.Return(), Throws.InvalidOperationException,
                "a downed hero ends the Run in Death, not a Return");

            var result = run.HandleDeath(xpTowardNextLevel: 0);
            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Died));
        }

        [Test]
        public void ADiedResult_CarriesTheSameRunTotalsAsAReturn_PlusThePenalty()
        {
            var hero = OneShotHero();
            var run = NewRun(hero, penalty: new RunPenalty(0.5f, 0.5f), tuning: LongBeat());
            run.Send(Skirmishers(5));

            run.Advance(0.1f);
            run.Advance(0.1f); // 2 kills, no clear
            run.BankCurrency(400); // bank while the hero is still up
            var defeated = run.Encounter.EnemiesDefeated;
            var earned = run.Encounter.SettledXp;

            hero.PhysicalDamage = 0f; // stop killing so the down-tick adds nothing more
            hero.Health = 0f;
            DriveHeroDown(run); // the sim notices the hero is down on its next tick

            var result = run.HandleDeath(xpTowardNextLevel: 100);

            Assert.That(result.EnemiesDefeated, Is.EqualTo(defeated));
            Assert.That(result.EncountersCleared, Is.EqualTo(0));
            Assert.That(result.XpSettled, Is.EqualTo(earned), "a Death does not claw back XP the kills already delivered");
            Assert.That(result.CurrencyBanked, Is.EqualTo(400));
            Assert.That(result.XpLost, Is.EqualTo(50));
            Assert.That(result.CurrencyFee, Is.EqualTo(200));
        }

        // ─── many Runs in one Session ────────────────────────────────────────

        [Test]
        public void AfterARunEnds_AnotherCanBeSent_AndTheRunTotalsReset()
        {
            var run = NewRun(OneShotHero());

            run.Send(Skirmishers(3));
            run.BankCurrency(900);
            var first = run.Return();
            Assert.That(first.CurrencyBanked, Is.EqualTo(900));

            run.Send(Skirmishers(3));
            Assert.That(run.CurrencyBanked, Is.EqualTo(0), "a fresh Run starts from zero");

            run.BankCurrency(50);
            var second = run.Return();
            Assert.That(second.CurrencyBanked, Is.EqualTo(50));
        }

        [Test]
        public void Advance_Live_RunsTheEncounter_AndReportsTicks()
        {
            var run = NewRun(new FakeHero { PhysicalDamage = 0f, MagicalDamage = 0f, Resource = 0f, Level = 5 });
            run.Send(Profiles.Solo(EnemyArchetype.Brute, sourceLevel: 5));

            var ticks = run.Advance(0.25f); // 2 whole ticks at 0.1 s

            Assert.That(ticks, Is.EqualTo(2));
            Assert.That(run.Encounter.Duration, Is.EqualTo(0.2f).Within(0.0001f));
        }

        // ─── RunEnded (issue #24's ground-Drop clearing hook) ───────────────

        [Test]
        public void Return_RaisesRunEnded()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(3));

            var raised = 0;
            run.RunEnded += () => raised++;

            run.Return();

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void HandleDeath_RaisesRunEnded()
        {
            var run = NewRun(FrailHero());
            run.Send(Skirmishers(4));
            DriveHeroDown(run);

            var raised = 0;
            run.RunEnded += () => raised++;

            run.HandleDeath(xpTowardNextLevel: 0);

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void RunEnded_IsNotRaised_WhileTheRunIsStillLive()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(3));

            var raised = 0;
            run.RunEnded += () => raised++;

            run.BankCurrency(10);

            Assert.That(raised, Is.EqualTo(0));
        }

        // ─── no Auto-Recall (ADR-0021) ───────────────────────────────────────

        [Test]
        public void AHeroUnderTheRetreatHealth_StaysInTheField()
        {
            var hero = new FakeHero
            {
                MaxHealth = 100f,
                Health = 25f,
                PhysicalDamage = 0f,
                MagicalDamage = 0f,
                AttackSpeed = 0.01f,
                ArmorPercent = 100f,
                MagicResistPercent = 100f, // a Skirmisher Strikes magically
                Resource = 0f,
                Level = 5,
            };
            var run = NewRun(hero, behaviour: new HeroBehaviour { Engagement = 10 });
            run.Send(Skirmishers(3));

            for (var i = 0; i < 50; i++) run.Advance(0.1f);

            Assert.That(run.Phase, Is.EqualTo(RunPhase.Live));
            Assert.That(run.Encounter.Phase, Is.EqualTo(SimulationPhase.Fighting), "the Run ends only by a Return or a Death");
        }

        // ─── the Recall: an opening, then Held (ADR-0022) ────────────────────

        // Enemies that outlast a weak hero and keep spawning, so a live tick moves health, positions and the spawn timer.
        private static EncounterProfile SlowFight() => new(
            sourceLevel: 5,
            packed: EnemyArchetype.Skirmisher,
            rosterBrute: new IntRange(0),
            rosterSkirmisher: new IntRange(30),
            packBatch: new IntRange(1),
            packedSpawnWeight: 1f,
            spawnInterval: 1f,
            table: FakeLootTable.ForCategory(ItemCategory.Equipment),
            spawnJitter: 0f,
            initialSpawn: 1,
            population: new IntRange(1));

        private static FakeHero SteadyHero() => new()
        {
            MaxHealth = 1_000_000f,
            Health = 1_000_000f,
            PhysicalDamage = 1f,
            AttackSpeed = 10f,
            MagicalDamage = 0f,
            Resource = 0f,
            Level = 5,
        };

        // Everything the Encounter moves with time: health, positions, the spawn count and the clock the spawn timer reads.
        private static string Snapshot(RunState run)
        {
            var encounter = run.Encounter;
            var text = $"{encounter.Duration}|{encounter.AliveEnemyCount}|{encounter.EnemiesDefeated}|{encounter.HeroPosition.ToString("F6")}";
            foreach (var enemy in encounter.Enemies)
                text += $"|{enemy.Health}@{enemy.Position.ToString("F6")}";
            return text;
        }

        private static RunState HeldRun()
        {
            var run = NewRun(SteadyHero(), recallOpening: 0.5f);
            run.Send(SlowFight());
            run.Recall();
            run.Advance(0.5f);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Held), "setup failed to hold the Run");
            return run;
        }

        [Test]
        public void Recall_OnALiveRun_OpensThePortal_AndTheRunStaysLive()
        {
            var run = NewRun(SteadyHero(), recallOpening: 0.5f);
            run.Send(SlowFight());

            run.Recall();

            Assert.That(run.IsRecalling, Is.True);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Live));
        }

        [Test]
        public void TheOpening_KeepsTheEncounterTicking_ThenHoldsTheRunOnce()
        {
            var run = NewRun(SteadyHero(), recallOpening: 0.5f);
            run.Send(SlowFight());
            var phases = new List<RunPhase>();
            run.PhaseChanged += phases.Add;
            run.Recall();
            var before = run.Encounter.Duration;

            var ticks = run.Advance(0.3f);

            Assert.That(ticks, Is.GreaterThan(0), "hits keep landing through the opening");
            Assert.That(run.Encounter.Duration, Is.GreaterThan(before));
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Live));
            Assert.That(phases, Is.Empty);

            run.Advance(0.3f);

            Assert.That(run.Phase, Is.EqualTo(RunPhase.Held));
            Assert.That(run.IsRecalling, Is.False);
            Assert.That(phases, Is.EqualTo(new[] { RunPhase.Held }));
        }

        [Test]
        public void TheOpening_TicksTheEncounterNoFurtherThanItsOwnSimTime()
        {
            var run = NewRun(SteadyHero(), recallOpening: 0.5f);
            run.Send(SlowFight());
            run.Recall();

            run.Advance(60f);

            Assert.That(run.Phase, Is.EqualTo(RunPhase.Held));
            Assert.That(run.Encounter.Duration, Is.EqualTo(0.5f).Within(0.11f), "one opening of sim time, not the whole frame");
        }

        [Test]
        public void AHeldRun_AdvancesNothing()
        {
            var run = HeldRun();
            var frozen = Snapshot(run);

            for (var i = 0; i < 100; i++)
                Assert.That(run.Advance(0.1f), Is.EqualTo(0));

            Assert.That(Snapshot(run), Is.EqualTo(frozen));
        }

        [Test]
        public void TheOpening_MovesWhatTheHeldRunFreezes()
        {
            var run = NewRun(SteadyHero(), recallOpening: 5f);
            run.Send(SlowFight());
            run.Recall();
            var before = Snapshot(run);

            run.Advance(2f);

            Assert.That(Snapshot(run), Is.Not.EqualTo(before), "else the frozen-snapshot test proves nothing");
        }

        [Test]
        public void AHeroDownedDuringTheOpening_CancelsIt_AndTheRunEndsInDeath()
        {
            var run = NewRun(FrailHero(), recallOpening: 30f);
            run.Send(Skirmishers(4));
            run.Recall();

            DriveHeroDown(run);

            Assert.That(run.IsRecalling, Is.False);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Live));
            Assert.That(run.HandleDeath(xpTowardNextLevel: 0).Outcome, Is.EqualTo(RunOutcome.Died));
            Assert.That(run.Phase, Is.EqualTo(RunPhase.None));
        }

        [Test]
        public void ADeathOnTheTickTheOpeningRunsOut_WinsOverTheHold()
        {
            var run = NewRun(FrailHero(), recallOpening: 30f);
            run.Send(Skirmishers(4));
            run.Recall();

            run.Advance(60f);

            Assert.That(run.HeroIsDown, Is.True, "setup: the hero falls inside the opening");
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Live), "a Death is not held");
        }

        [Test]
        public void ReturnRelocateAndRecall_AreRefusedWhileThePortalOpens()
        {
            var run = NewRun(SteadyHero(), recallOpening: 0.5f);
            run.Send(SlowFight());
            run.Recall();

            Assert.That(() => run.Return(), Throws.InvalidOperationException);
            Assert.That(() => run.Relocate(Skirmishers(3)), Throws.InvalidOperationException);
            Assert.That(() => run.Recall(), Throws.InvalidOperationException);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Live));
            Assert.That(run.IsRecalling, Is.True, "a refusal leaves the opening running");
        }

        [Test]
        public void Recall_IsRefused_WithNoRun_WhileHeld_AndOnceTheHeroIsDown()
        {
            var none = NewRun(SteadyHero());
            Assert.That(() => none.Recall(), Throws.InvalidOperationException);

            var held = HeldRun();
            Assert.That(() => held.Recall(), Throws.InvalidOperationException);
            Assert.That(held.Phase, Is.EqualTo(RunPhase.Held));

            var down = NewRun(FrailHero(), recallOpening: 0.5f);
            down.Send(Skirmishers(4));
            DriveHeroDown(down);
            Assert.That(() => down.Recall(), Throws.InvalidOperationException);
            Assert.That(down.IsRecalling, Is.False);
        }

        [Test]
        public void AHeldRun_RefusesReturnAndHandleDeath()
        {
            var run = HeldRun();

            Assert.That(() => run.Return(), Throws.InvalidOperationException);
            Assert.That(() => run.HandleDeath(0), Throws.InvalidOperationException);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Held));
        }

        [Test]
        public void Recall_FromTheCompletePhase_OpensAndHoldsLikeAnyOther()
        {
            var run = NewRun(OneShotHero(), recallOpening: 0.5f);
            run.Send(Profiles.Group(EnemyArchetype.Skirmisher, 1));
            for (var i = 0; i < 500 && run.Encounter.Phase != SimulationPhase.Complete; i++)
                run.Advance(0.1f);
            Assert.That(run.Encounter.Phase, Is.EqualTo(SimulationPhase.Complete), "setup: the Location completes");
            var phases = new List<RunPhase>();
            run.PhaseChanged += phases.Add;

            run.Recall();
            run.Advance(0.3f);

            Assert.That(run.Phase, Is.EqualTo(RunPhase.Live), "the opening's timer is the Run's, and 0.3 of 0.5 s is not enough");

            run.Advance(0.3f);

            Assert.That(run.Phase, Is.EqualTo(RunPhase.Held));
            Assert.That(run.Encounter.Phase, Is.EqualTo(SimulationPhase.Complete));
            Assert.That(phases, Is.EqualTo(new[] { RunPhase.Held }));
        }

        [Test]
        public void ARecall_DoesNotSurviveTheRunThatEnded()
        {
            // A Return is refused while the portal opens, so the Run ends the only other way: a Death in it.
            var run = NewRun(FrailHero(), recallOpening: 30f);
            run.Send(Skirmishers(4));
            run.Recall();
            DriveHeroDown(run);
            run.HandleDeath(0);

            run.Send(Skirmishers(4));

            Assert.That(run.IsRecalling, Is.False);
        }
    }
}
