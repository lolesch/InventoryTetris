using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The <see cref="HeroBehaviour"/> is the player's whole input to a Run (issue #23), and
    /// these are the tests that cross <see cref="EncounterSimulation"/>'s interface to prove it
    /// lands: Engagement re-read every spawn tick, the CastThreshold latch gating
    /// <c>ResolveCast</c>, and the retreat fraction no longer ending a Run (ADR-0021).
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
            MagicResistPercent = 100f, // a Skirmisher Strikes magically
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

            var casts = 0;
            sim.HeroCast += () => casts++;

            Run(sim, 10);

            // 60 → 44 → 28 → 0: the latch stays armed all the way down (it is the *start* the threshold
            // gates, never the continuation), and the Cast that leaves too little for another takes the rest.
            Assert.That(casts, Is.EqualTo(3));
            Assert.That(hero.Resource, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void CastThreshold_Zero_CastsAgainAsSoonAsAnotherIsAffordable()
        {
            var hero = PassiveHero();
            hero.MaxResource = 100f;
            hero.Resource = 40f;
            hero.CastCost = 16f;

            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), Behaviours.Engaging(5), FastCast());

            var casts = 0;
            sim.HeroCast += () => casts++;

            Run(sim, 10); // 40 → 24 → 0
            hero.Resource = 20f;
            Run(sim, 10);

            Assert.That(casts, Is.EqualTo(3), "threshold 0 never holds: the next affordable Cast fires");
            Assert.That(hero.Resource, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void CastThreshold_DrainedPool_RechargesToTheThresholdBeforeCastingAgain()
        {
            var hero = PassiveHero();
            hero.MaxResource = 100f;
            hero.Resource = 60f;
            hero.CastCost = 16f;

            var behaviour = Behaviours.Engaging(5);
            behaviour.CastThreshold = 0.5f;

            var sim = new EncounterSimulation(hero, Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), behaviour, FastCast());

            Run(sim, 10); // 60 → 44 → 28 → 0: the run is over
            hero.Resource = 32f; // recharged by hand: past the cost, short of the threshold
            Run(sim, 10);

            Assert.That(hero.Resource, Is.EqualTo(32f).Within(0.001f),
                "a drained pool is not regen-gated at the cost: it waits for the threshold");
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

        // ─── no Auto-Recall (ADR-0021) ───────────────────────────────────────

        private static FakeHero WoundedHero(float healthFraction)
        {
            var hero = PassiveHero();
            hero.MaxHealth = 100f;
            hero.Health = 100f * healthFraction;
            return hero;
        }

        [Test]
        public void AHeroBelowTheRetreatHealth_KeepsFighting()
        {
            var behaviour = Behaviours.Engaging(5);
            behaviour.RetreatHealthFraction = 0.3f;

            var sim = new EncounterSimulation(WoundedHero(0.25f), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), behaviour);

            Run(sim, 50);

            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Fighting), "a Run ends only by a Return or a Death");
        }

        [Test]
        public void AHeroGoingDown_IsADeath()
        {
            var sim = new EncounterSimulation(WoundedHero(0f), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), Behaviours.Engaging(5));

            var downed = 0;
            sim.HeroDowned += () => downed++;

            Run(sim, 5);

            Assert.That(downed, Is.EqualTo(1));
        }
    }
}
