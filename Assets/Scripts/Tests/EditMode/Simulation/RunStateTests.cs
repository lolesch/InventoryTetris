using System;
using NUnit.Framework;
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
            HeroBehaviour behaviour = null) =>
            new(profile => new EncounterSimulation(
                    hero, profile, new ConstantRollSource(0f),
                    behaviour ?? Behaviours.Engaging(engagement), tuning ?? new EncounterTuning()),
                penalty);

        private static EncounterProfile Skirmishers(int count) =>
            Profiles.Group(EnemyArchetype.Skirmisher, count, sourceLevel: 5);

        // ─── transitions ─────────────────────────────────────────────────────

        [Test]
        public void ANewRunState_StartsInTown_WithNoLocationAndNoEncounter()
        {
            var run = NewRun(OneShotHero());

            Assert.That(run.Phase, Is.EqualTo(RunPhase.InTown));
            Assert.That(run.Location, Is.Null);
            Assert.That(run.Encounter, Is.Null);
            Assert.That(run.LastResult, Is.Null);
        }

        [Test]
        public void Send_EntersInField_WithTheLocationAndALiveEncounter()
        {
            var run = NewRun(OneShotHero());
            var thornwood = Skirmishers(3);

            run.Send(thornwood);

            Assert.That(run.Phase, Is.EqualTo(RunPhase.InField));
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

            Assert.That(run.Phase, Is.EqualTo(RunPhase.InTown));
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

            Assert.That(run.Phase, Is.EqualTo(RunPhase.InTown));
            Assert.That(run.Location, Is.Null);
            Assert.That(run.Encounter, Is.Null);
        }

        [Test]
        public void Send_WhileInField_Throws()
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
        public void Return_WhileInTown_Throws()
        {
            var run = NewRun(OneShotHero());

            Assert.That(() => run.Return(), Throws.InvalidOperationException);
        }

        [Test]
        public void HandleDeath_WhileInTown_Throws()
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
            Assert.That(run.Phase, Is.EqualTo(RunPhase.InField), "the refused HandleDeath must not half-end the Run");
        }

        [Test]
        public void Advance_WhileInTown_IsANoOp()
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
            Assert.That(run.Phase, Is.EqualTo(RunPhase.InTown), "a failed Send leaves the Run in Town");
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
            run.Send(Skirmishers(2));

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
        public void BankCurrency_WhileInTown_Throws()
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
        public void Advance_InField_RunsTheEncounter_AndReportsTicks()
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
        public void RunEnded_IsNotRaised_WhileTheRunIsStillInField()
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

            Assert.That(run.Phase, Is.EqualTo(RunPhase.InField));
            Assert.That(run.Encounter.Phase, Is.EqualTo(SimulationPhase.Fighting), "the Run ends only by a Return or a Death");
        }
    }
}
