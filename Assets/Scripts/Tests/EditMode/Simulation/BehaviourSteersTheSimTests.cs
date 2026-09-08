using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The <see cref="HeroBehaviour"/> is the player's whole input to a Run (issue #23), and
    /// these are the tests that cross <see cref="EncounterSimulation"/>'s interface to prove it
    /// lands: Engagement re-read every spawn tick, the CastThreshold latch gating
    /// <c>ResolveCast</c>, and both retreat triggers raising
    /// <see cref="EncounterSimulation.RecallRequested"/>.
    ///
    /// <see cref="HeroBehaviourTests"/> covers the predicates in isolation — that they compute
    /// the right answer. These cover that the sim asks them, which is the part a slider a player
    /// drags actually depends on.
    /// </summary>
    [TestFixture]
    public sealed class BehaviourSteersTheSimTests
    {
        /// <summary>Deals nothing and takes nothing — health and Resource stay where a test puts them.</summary>
        private static FakeHero PassiveHero() => new()
        {
            PhysicalDamage = 0f,
            MagicalDamage = 0f,
            AttackSpeed = 0.01f,
            Resource = 0f,
            ArmorPercent = 100f,
        };

        private static EncounterTuning FastCast() => new() { CastCadence = 0.05f };

        /// <summary>A trickle of Skirmishers, deep enough a raised Engagement has somewhere to draw from.</summary>
        private static EncounterProfile Trickle() => new(
            sourceLevel: 3,
            packed: EnemyArchetype.Skirmisher,
            rosterBrute: new IntRange(0),
            rosterSkirmisher: new IntRange(12),
            packBatch: new IntRange(1),
            packedSpawnWeight: 1f,
            spawnInterval: 0.5f,
            table: FakeLootTable.ForCategory(ItemCategory.Equipment),
            spawnJitter: 0f,
            initialSpawn: 2);

        private static void Run(EncounterSimulation sim, int ticks)
        {
            for (var i = 0; i < ticks; i++) sim.Advance(0.1f);
        }

        // ─── Engagement ──────────────────────────────────────────────────────

        [Test]
        public void Engagement_RaisedMidFight_RefillsTowardTheNewTarget()
        {
            var behaviour = Behaviours.Engaging(3);
            var sim = new EncounterSimulation(PassiveHero(), Trickle(), new ConstantRollSource(0f), behaviour);

            Run(sim, 200);
            Assert.That(sim.AliveEnemyCount, Is.EqualTo(3), "settled at the original target");

            behaviour.Engagement = 6;
            Run(sim, 200);

            Assert.That(sim.AliveEnemyCount, Is.EqualTo(6),
                "the spawn schedule re-reads Engagement, so the slider moves the live fight");
        }

        [Test]
        public void Engagement_LoweredMidFight_StopsRefilling()
        {
            var behaviour = Behaviours.Engaging(6);
            var sim = new EncounterSimulation(PassiveHero(), Trickle(), new ConstantRollSource(0f), behaviour);

            Run(sim, 200);
            Assert.That(sim.AliveEnemyCount, Is.EqualTo(6));

            // Nothing dies in this fixture, so a lowered target cannot shrink the crowd — it can
            // only stop it growing. That is the honest assertion: Engagement is a refill target.
            behaviour.Engagement = 2;
            Run(sim, 200);

            Assert.That(sim.AliveEnemyCount, Is.EqualTo(6), "already past the new target — no more arrive");
        }

        // ─── the Cast threshold ──────────────────────────────────────────────

        [Test]
        public void CastThreshold_HeldBelowTheThreshold_SpendsNoResource()
        {
            var hero = PassiveHero();
            hero.MaxResource = 100f;
            hero.Resource = 30f;   // 30 % — under the hold
            hero.CastCost = 16f;   // affordable, so only the threshold can be stopping it

            var behaviour = Behaviours.Engaging(5);
            behaviour.CastThreshold = 0.5f;

            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), behaviour, FastCast());

            Run(sim, 10); // ~20 Cast cadences

            Assert.That(hero.Resource, Is.EqualTo(30f).Within(0.001f),
                "the Cast is affordable and there are targets — only the threshold holds it");
        }

        [Test]
        public void CastThreshold_OnceCharged_BurnsThePoolDown()
        {
            var hero = PassiveHero();
            hero.MaxResource = 100f;
            hero.Resource = 60f;   // 60 % — clears the hold
            hero.CastCost = 16f;

            var behaviour = Behaviours.Engaging(5);
            behaviour.CastThreshold = 0.5f;

            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), behaviour, FastCast());

            Run(sim, 10);

            // 60 → 44 → 28 → 12, then the next Cast is unaffordable. The latch stays armed all
            // the way down: it is the *start* the threshold gates, never the continuation.
            Assert.That(hero.Resource, Is.EqualTo(12f).Within(0.001f));
        }

        [Test]
        public void CastThreshold_RaisedMidFight_StopsTheNextCastingRun()
        {
            var hero = PassiveHero();
            hero.MaxResource = 100f;
            hero.Resource = 100f;
            hero.CastCost = 100f; // one Cast empties the pool, releasing the latch

            var behaviour = Behaviours.Engaging(5);
            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), behaviour, FastCast());

            Run(sim, 5);
            Assert.That(hero.Resource, Is.EqualTo(0f).Within(0.001f), "the default 0 threshold never holds");

            // Refill by hand and raise the bar above the new level — the run has to stay released.
            hero.Resource = 40f;
            behaviour.CastThreshold = 0.9f;
            Run(sim, 10);

            Assert.That(hero.Resource, Is.EqualTo(40f).Within(0.001f),
                "40 % is under the raised threshold, so no new casting run starts");
        }

        // ─── the retreat triggers ────────────────────────────────────────────

        private static FakeHero WoundedHero(float healthFraction)
        {
            var hero = PassiveHero();
            hero.MaxHealth = 100f;
            hero.Health = 100f * healthFraction;
            return hero;
        }

        [Test]
        public void RetreatHealth_AtTheThreshold_RequestsARecallAndStopsTheFight()
        {
            var behaviour = Behaviours.Engaging(5);
            behaviour.RetreatHealthFraction = 0.3f;

            var sim = new EncounterSimulation(WoundedHero(0.25f), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), behaviour);

            var requested = 0;
            sim.RecallRequested += () => requested++;

            Run(sim, 1);

            Assert.That(requested, Is.EqualTo(1));
            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Ended), "the fight stops where the slider said");
        }

        [Test]
        public void RetreatHealth_AboveTheThreshold_KeepsFighting()
        {
            var behaviour = Behaviours.Engaging(5);
            behaviour.RetreatHealthFraction = 0.3f;

            var sim = new EncounterSimulation(WoundedHero(0.5f), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), behaviour);

            var requested = 0;
            sim.RecallRequested += () => requested++;

            Run(sim, 50);

            Assert.That(requested, Is.Zero);
            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Fighting));
        }

        [Test]
        public void RetreatHealth_UnsetByDefault_NeverFires()
        {
            var sim = new EncounterSimulation(WoundedHero(0.01f), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), Behaviours.Engaging(5));

            var requested = 0;
            sim.RecallRequested += () => requested++;

            Run(sim, 50);

            Assert.That(requested, Is.Zero, "a 0 retreat fraction is inert — only a dead hero reaches it");
        }

        [Test]
        public void ARetreat_ForfeitsTheInProgressPot_JustAsAnAbandonWould()
        {
            var hero = WoundedHero(0.25f);
            hero.PhysicalDamage = 1_000_000f;
            hero.AttackSpeed = 10f; // one Strike on the first tick

            var behaviour = Behaviours.Engaging(5);
            behaviour.RetreatHealthFraction = 0.3f;

            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Skirmisher, 2),
                new ConstantRollSource(0f), behaviour);

            Run(sim, 1); // Strike fells one of the two, then the retreat trigger fires

            Assert.That(sim.EnemiesDefeated, Is.EqualTo(1), "the kill landed before the retreat check");
            Assert.That(sim.ForfeitedXp, Is.GreaterThan(0));
            Assert.That(sim.UnsettledXp, Is.EqualTo(0f));
        }

        [Test]
        public void AHeroGoingDown_IsADeath_NotARetreat()
        {
            var hero = WoundedHero(0f); // down, and far below the retreat fraction

            var behaviour = Behaviours.Engaging(5);
            behaviour.RetreatHealthFraction = 0.3f;

            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), behaviour);

            var downed = 0;
            var requested = 0;
            sim.HeroDowned += () => downed++;
            sim.RecallRequested += () => requested++;

            Run(sim, 5);

            Assert.That(downed, Is.EqualTo(1));
            Assert.That(requested, Is.Zero, "the Death penalty is not dodgeable by a retreat racing it");
        }

        // ─── the bag-full trigger ────────────────────────────────────────────

        [Test]
        public void BagFull_FillingMidFight_RequestsARecall()
        {
            var bag = new FakeBagGauge { FillFraction = 0.5f };

            var behaviour = Behaviours.Engaging(5);
            behaviour.RecallBagFillFraction = 0.9f;

            var sim = new EncounterSimulation(PassiveHero(), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), behaviour, null, bag);

            var requested = 0;
            sim.RecallRequested += () => requested++;

            Run(sim, 10);
            Assert.That(requested, Is.Zero, "half full is under the bar");

            bag.FillFraction = 0.95f;
            Run(sim, 1);

            Assert.That(requested, Is.EqualTo(1));
            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Ended));
        }

        [Test]
        public void BagFull_WithNoGaugeWired_NeverFires()
        {
            var behaviour = Behaviours.Engaging(5);
            behaviour.RecallBagFillFraction = 0f; // fires for any fill at all — if it can measure one

            var sim = new EncounterSimulation(PassiveHero(), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), behaviour);

            var requested = 0;
            sim.RecallRequested += () => requested++;

            Run(sim, 50);

            Assert.That(requested, Is.Zero, "no gauge, nothing to measure, no trigger");
        }

        [Test]
        public void BagFull_LeftAtItsDefault_NeedsARealFull()
        {
            var bag = new FakeBagGauge { FillFraction = 0.99f };

            var sim = new EncounterSimulation(PassiveHero(), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), Behaviours.Engaging(5), null, bag);

            var requested = 0;
            sim.RecallRequested += () => requested++;

            Run(sim, 50);
            Assert.That(requested, Is.Zero, "the 1f default is inert short of a bag with no room left");

            bag.FillFraction = 1f;
            Run(sim, 1);

            Assert.That(requested, Is.EqualTo(1));
        }
    }
}
