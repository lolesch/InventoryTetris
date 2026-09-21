using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The <see cref="RunState"/> FSM (issue #21). Two states — <c>InTown</c> and
    /// <c>InField</c>; <c>Send</c> opens a Run at a Location, <c>Recall</c> and
    /// <c>HandleDeath</c> close it. On close it freezes a <see cref="RunResult"/> read off the
    /// live <see cref="EncounterSimulation"/>: a <c>Recalled</c> result carries the full
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
            IBagGauge bag = null) =>
            new(profile => new EncounterSimulation(
                    hero, profile, new ConstantRollSource(0f),
                    behaviour ?? Behaviours.Engaging(engagement), tuning ?? new EncounterTuning(), bag),
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
        public void Recall_ReturnsToTown_AndDropsTheLocationAndEncounter()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(3));

            run.Recall();

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
        public void Recall_WhileInTown_Throws()
        {
            var run = NewRun(OneShotHero());

            Assert.That(() => run.Recall(), Throws.InvalidOperationException);
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
                "a live hero's Run only ends in Recall — HandleDeath is not a 'give up' button");
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

        // ─── accumulation ────────────────────────────────────────────────────

        [Test]
        public void ClearingEncounters_AccumulatesSettledXp_IntoTheRunTotal()
        {
            var run = NewRun(OneShotHero(), tuning: ShortBeat());
            run.Send(Skirmishers(2));

            for (var i = 0; i < 500 && run.Encounter.EncountersCleared < 3; i++)
                run.Advance(0.1f);

            var settledMidRun = run.Encounter.SettledXp;
            Assert.That(settledMidRun, Is.GreaterThan(0));

            var result = run.Recall();
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

            var result = run.Recall();
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

        // ─── the Recalled outcome ────────────────────────────────────────────

        [Test]
        public void Recall_FreezesARecalledResult_CarryingTheFullAccumulation()
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

            var result = run.Recall();

            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Recalled));
            Assert.That(result.XpSettled, Is.EqualTo(settled));
            Assert.That(result.CurrencyBanked, Is.EqualTo(1234));
            Assert.That(result.EnemiesDefeated, Is.EqualTo(defeated));
            Assert.That(result.EncountersCleared, Is.EqualTo(cleared));
            Assert.That(result.Duration, Is.EqualTo(duration).Within(0.0001f));
            Assert.That(result.XpLost, Is.EqualTo(0), "no penalty on a Recall");
            Assert.That(result.CurrencyFee, Is.EqualTo(0), "no penalty on a Recall");
            Assert.That(run.LastResult, Is.EqualTo(result));
        }

        [Test]
        public void Recall_MidEncounter_ForfeitsTheUnsettledPot()
        {
            var run = NewRun(OneShotHero(), tuning: LongBeat());
            run.Send(Skirmishers(5));

            run.Advance(0.1f);
            run.Advance(0.1f); // 2 of 5 down — pot is live, Encounter not cleared
            var expectedForfeit = (int)Math.Round(run.Encounter.UnsettledXp, MidpointRounding.AwayFromZero);
            Assert.That(expectedForfeit, Is.GreaterThan(0));

            var result = run.Recall();

            Assert.That(result.XpForfeited, Is.EqualTo(expectedForfeit));
            Assert.That(result.XpSettled, Is.EqualTo(0), "nothing cleared this Run");
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
        public void WhenTheHeroIsDowned_TheRunReportsIt_AndRecallIsRefused()
        {
            var run = NewRun(FrailHero());
            run.Send(Skirmishers(4));
            DriveHeroDown(run);

            Assert.That(run.Encounter.Phase, Is.EqualTo(SimulationPhase.Ended));
            Assert.That(() => run.Recall(), Throws.InvalidOperationException,
                "a downed hero ends the Run in Death, not a Recall");

            var result = run.HandleDeath(xpTowardNextLevel: 0);
            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Died));
        }

        [Test]
        public void ADiedResult_CarriesTheSameRunTotalsAsARecall_PlusThePenalty()
        {
            var hero = OneShotHero();
            var run = NewRun(hero, penalty: new RunPenalty(0.5f, 0.5f), tuning: LongBeat());
            run.Send(Skirmishers(5));

            run.Advance(0.1f);
            run.Advance(0.1f); // 2 kills — partial pot, no clear
            run.BankCurrency(400); // bank while the hero is still up
            var defeated = run.Encounter.EnemiesDefeated;
            var expectedForfeit = (int)Math.Round(run.Encounter.UnsettledXp, MidpointRounding.AwayFromZero);

            hero.PhysicalDamage = 0f; // stop killing so the down-tick adds nothing more to the pot
            hero.Health = 0f;
            DriveHeroDown(run); // the sim notices the hero is down on its next tick

            var result = run.HandleDeath(xpTowardNextLevel: 100);

            Assert.That(result.EnemiesDefeated, Is.EqualTo(defeated));
            Assert.That(result.EncountersCleared, Is.EqualTo(0));
            Assert.That(result.XpSettled, Is.EqualTo(0));
            Assert.That(result.XpForfeited, Is.EqualTo(expectedForfeit));
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
            var first = run.Recall();
            Assert.That(first.CurrencyBanked, Is.EqualTo(900));

            run.Send(Skirmishers(3));
            Assert.That(run.CurrencyBanked, Is.EqualTo(0), "a fresh Run starts from zero");

            run.BankCurrency(50);
            var second = run.Recall();
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
        public void Recall_RaisesRunEnded()
        {
            var run = NewRun(OneShotHero());
            run.Send(Skirmishers(3));

            var raised = 0;
            run.RunEnded += () => raised++;

            run.Recall();

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

        // ─── the behaviour's own retreat (issue #23) ─────────────────────────

        /// <summary>A hero already under a 0.3 retreat fraction, that neither deals nor takes damage.</summary>
        private static FakeHero RetreatingHero() => new()
        {
            MaxHealth = 100f,
            Health = 25f,
            PhysicalDamage = 0f,
            MagicalDamage = 0f,
            AttackSpeed = 0.01f,
            ArmorPercent = 100f,
            Resource = 0f,
            Level = 5,
        };

        private static HeroBehaviour RetreatsAt(float healthFraction) => new()
        {
            Engagement = 10,
            RetreatHealthFraction = healthFraction,
        };

        [Test]
        public void ARetreatTrigger_MarksTheRunAsWantingToComeHome_WithoutEndingItItself()
        {
            var run = NewRun(RetreatingHero(), behaviour: RetreatsAt(0.3f));
            run.Send(Skirmishers(3));

            run.Advance(0.1f);

            Assert.That(run.RecallRequested, Is.True);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.InField),
                "the sim only asks — the transition is the driver's, outside the tick that raised it");
        }

        [Test]
        public void RecallRequested_IsFalse_WhileTheHeroIsHealthy()
        {
            var run = NewRun(RetreatingHero(), behaviour: RetreatsAt(0.1f));
            run.Send(Skirmishers(3));

            for (var i = 0; i < 50; i++) run.Advance(0.1f);

            Assert.That(run.RecallRequested, Is.False);
        }

        [Test]
        public void RecallingAfterARetreatRequest_EndsTheRunWithEverythingKept()
        {
            var run = NewRun(RetreatingHero(), behaviour: RetreatsAt(0.3f));
            run.Send(Skirmishers(3));
            run.BankCurrency(120);
            run.Advance(0.1f);

            var ended = 0;
            run.RunEnded += () => ended++;

            var result = run.Recall();

            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Recalled), "a retreat is a Recall, not a Death");
            Assert.That(result.CurrencyBanked, Is.EqualTo(120));
            Assert.That(result.XpLost, Is.EqualTo(0));
            Assert.That(result.CurrencyFee, Is.EqualTo(0));
            Assert.That(run.Phase, Is.EqualTo(RunPhase.InTown));
            Assert.That(ended, Is.EqualTo(1), "down the one existing RunEnded path");
        }

        [Test]
        public void RecallRequested_DoesNotSurvive_IntoTheNextRun()
        {
            var behaviour = RetreatsAt(0.3f);
            var run = NewRun(RetreatingHero(), behaviour: behaviour);

            run.Send(Skirmishers(3));
            run.Advance(0.1f);
            _ = run.Recall();
            Assert.That(run.RecallRequested, Is.False, "cleared on the way out");

            behaviour.RetreatHealthFraction = 0f; // the player pulled the slider back down
            run.Send(Skirmishers(3));

            Assert.That(run.RecallRequested, Is.False);
        }

        [Test]
        public void ABagFullTrigger_ReachesTheRunTheSameWay()
        {
            var bag = new FakeBagGauge { FillFraction = 0.2f };
            var behaviour = new HeroBehaviour { Engagement = 10, RecallBagFillFraction = 0.8f };

            var run = NewRun(RetreatingHero(), behaviour: behaviour, bag: bag);
            run.Send(Skirmishers(3));

            run.Advance(0.1f);
            Assert.That(run.RecallRequested, Is.False);

            bag.FillFraction = 0.85f;
            run.Advance(0.1f);

            Assert.That(run.RecallRequested, Is.True);
        }
    }
}
