using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The sim side of the enemy HP bar binding (issue #94): an <see cref="Enemy"/>'s health is a
    /// real <c>CharacterResource</c> the bar can subscribe to, and <see cref="EncounterSimulation"/>
    /// announces each body as it arrives. The <c>EnemyArena</c> (formerly the HP bar pool, #176) that
    /// consumes both lives in <c>Assembly-CSharp</c> and has no EditMode coverage — these pin everything it depends on.
    /// </summary>
    [TestFixture]
    public sealed class EnemyHealthBarSeamTests
    {
        private static EncounterTuning ShortBeat() => new() { Beat = 0.5f, CastCadence = 0.05f };

        private static FakeHero OneShotHero() => new()
        {
            PhysicalDamage = 1_000_000f,
            AttackSpeed = 10f,
            MagicalDamage = 0f,
            Resource = 0f,
        };

        private static FakeHero InertHero() => new()
        {
            PhysicalDamage = 0f,
            MagicalDamage = 0f,
            Resource = 0f,
            AttackSpeed = 0.01f,
        };

        // ─── health is a resource ──────────────────────────────────────────

        [Test]
        public void Health_IsARealResource_OverTheArchetypeMaxHealth()
        {
            var brute = new Enemy(EnemyArchetype.Brute, sourceLevel: 5);

            var resource = brute.HealthResource;

            Assert.That(resource.Stat, Is.EqualTo(StatName.Health));
            Assert.That(resource.TotalValue, Is.EqualTo(brute.MaxHealth));
            Assert.That(resource.CurrentValue, Is.EqualTo(brute.Health));
            Assert.That(brute.Health, Is.EqualTo(brute.MaxHealth), "spawns at full health");
            Assert.That(resource.IsFull, Is.True);
            Assert.That(brute.HealthFraction, Is.EqualTo(1f));
            Assert.That(brute.IsDown, Is.False);
        }

        [Test]
        public void ReceivePhysical_AppliesArmorThenRemovesFromTheResource()
        {
            var brute = new Enemy(EnemyArchetype.Brute, sourceLevel: 5);
            var expected = 10f * (1f - brute.ArmorPercent * 0.01f);

            brute.ReceivePhysical(10f);

            Assert.That(brute.ArmorPercent, Is.GreaterThan(0f), "the test needs an armored body");
            Assert.That(brute.HealthResource.CurrentValue, Is.EqualTo(brute.MaxHealth - expected).Within(1e-3f));
            Assert.That(brute.Health, Is.EqualTo(brute.HealthResource.CurrentValue), "no parallel float to drift");
        }

        [Test]
        public void ReceiveMagical_IgnoresArmor()
        {
            var brute = new Enemy(EnemyArchetype.Brute, sourceLevel: 5);

            brute.ReceiveMagical(10f);

            Assert.That(brute.Health, Is.EqualTo(brute.MaxHealth - 10f).Within(1e-3f));
        }

        [Test]
        public void HealthFraction_TracksTheResource()
        {
            var skirmisher = new Enemy(EnemyArchetype.Skirmisher, sourceLevel: 5);

            skirmisher.ReceiveMagical(skirmisher.MaxHealth / 4f);

            Assert.That(skirmisher.HealthFraction, Is.EqualTo(0.75f).Within(1e-4f));
        }

        [Test]
        public void Damage_RaisesCurrentHasChanged_WithPreviousNewAndTotal()
        {
            var skirmisher = new Enemy(EnemyArchetype.Skirmisher, sourceLevel: 5);
            var seen = new List<(float previous, float current, float total)>();
            skirmisher.HealthResource.CurrentHasChanged += (p, c, t) => seen.Add((p, c, t));

            skirmisher.ReceiveMagical(10f);

            Assert.That(seen, Has.Count.EqualTo(1));
            Assert.That(seen[0].previous, Is.EqualTo(skirmisher.MaxHealth));
            Assert.That(seen[0].current, Is.EqualTo(skirmisher.MaxHealth - 10f).Within(1e-3f));
            Assert.That(seen[0].total, Is.EqualTo(skirmisher.MaxHealth));
        }

        [Test]
        public void NonPositiveDamage_ChangesNothingAndRaisesNothing()
        {
            var skirmisher = new Enemy(EnemyArchetype.Skirmisher, sourceLevel: 5);
            var raised = 0;
            skirmisher.HealthResource.CurrentHasChanged += (_, _, _) => raised++;

            skirmisher.ReceivePhysical(0f);
            skirmisher.ReceiveMagical(-5f);

            Assert.That(raised, Is.EqualTo(0));
            Assert.That(skirmisher.Health, Is.EqualTo(skirmisher.MaxHealth));
        }

        [Test]
        public void Overkill_ClampsToZero_AndDepletesOnce()
        {
            var skirmisher = new Enemy(EnemyArchetype.Skirmisher, sourceLevel: 5);
            var depleted = 0;
            skirmisher.HealthResource.CurrentHasDepleted += () => depleted++;

            skirmisher.ReceiveMagical(skirmisher.MaxHealth * 10f);
            skirmisher.ReceiveMagical(5f); // already down — nothing left to remove

            Assert.That(skirmisher.Health, Is.EqualTo(0f));
            Assert.That(skirmisher.IsDown, Is.True);
            Assert.That(skirmisher.HealthFraction, Is.EqualTo(0f));
            Assert.That(depleted, Is.EqualTo(1));
        }

        [Test]
        public void TheResourceCarriesItsWholeSurface_ChangeDepletedAndRecharged()
        {
            var skirmisher = new Enemy(EnemyArchetype.Skirmisher, sourceLevel: 5);
            var log = new List<string>();
            var resource = skirmisher.HealthResource;
            resource.CurrentHasChanged += (_, _, _) => log.Add("changed");
            resource.CurrentHasDepleted += () => log.Add("depleted");
            resource.CurrentHasRecharged += () => log.Add("recharged");

            skirmisher.ReceiveMagical(skirmisher.MaxHealth);
            resource.RefillCurrent(); // no caller heals an enemy today — the surface is for the next consumer

            Assert.That(log, Is.EqualTo(new[] { "changed", "depleted", "changed", "recharged" }));
        }

        // ─── EnemySpawned ──────────────────────────────────────────────────

        [Test]
        public void EnemySpawned_FiresOncePerTrickledBody_AfterItIsFullyConstructed()
        {
            var profile = new EncounterProfile(
                sourceLevel: 3,
                packed: EnemyArchetype.Skirmisher,
                rosterBrute: new IntRange(0),
                rosterSkirmisher: new IntRange(3),
                packBatch: new IntRange(1),
                packedSpawnWeight: 1f,
                spawnInterval: 0.5f,
                table: FakeLootTable.ForCategory(ItemCategory.Equipment),
                spawnJitter: 0f,
                initialSpawn: 1);
            var sim = new EncounterSimulation(InertHero(), profile, new ConstantRollSource(0f), Behaviours.Engaging(10));

            var spawned = new List<Enemy>();
            var inEnemiesAtRaise = new List<bool>();
            var fullAtRaise = new List<bool>();
            sim.EnemySpawned += e =>
            {
                spawned.Add(e);
                inEnemiesAtRaise.Add(sim.Enemies.Contains(e));
                fullAtRaise.Add(e.Health == e.MaxHealth && e.HealthResource.IsFull);
            };

            for (var i = 0; i < 50; i++) sim.Advance(0.1f);

            Assert.That(spawned, Has.Count.EqualTo(2), "the roster of 3 minus the initial body that opened the fight");
            Assert.That(spawned, Is.Unique);
            Assert.That(inEnemiesAtRaise, Is.All.True, "already in Enemies when announced");
            Assert.That(fullAtRaise, Is.All.True, "the resource is built and full when announced");
            Assert.That(sim.Enemies, Is.EquivalentTo(new[] { sim.Enemies[0] }.Concat(spawned)));
        }

        [Test]
        public void EnemySpawned_CoversTheInitialBatchOfEveryLaterEncounter_WithNoSpecialCase()
        {
            var sim = new EncounterSimulation(OneShotHero(), Profiles.Group(EnemyArchetype.Skirmisher, 3),
                new ConstantRollSource(0f), Behaviours.Engaging(10), ShortBeat());

            var spawned = new List<Enemy>();
            sim.EnemySpawned += spawned.Add;

            // Encounter 1's batch was spawned inside the constructor, before anyone could listen.
            // Run it out, ride the beat, and Encounter 2's initial batch arrives through the event.
            for (var i = 0; i < 500 && sim.CurrentEncounter < 2; i++) sim.Advance(0.1f);

            Assert.That(sim.CurrentEncounter, Is.EqualTo(2));
            Assert.That(spawned, Has.Count.EqualTo(3), "one event per body of the new Roster");
            Assert.That(spawned, Is.EquivalentTo(sim.Enemies));
        }

        // ─── depletion as an enemy falls ───────────────────────────────────

        [Test]
        public void WhenTheHeroKillsAnEnemy_ItsResourceChangesAndDepletesBeforeEnemyDefeated()
        {
            var sim = new EncounterSimulation(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher),
                new ConstantRollSource(0f), Behaviours.Engaging(5), ShortBeat());
            var enemy = sim.Enemies[0];

            var log = new List<string>();
            var lastChange = (previous: -1f, current: -1f, total: -1f);
            enemy.HealthResource.CurrentHasChanged += (p, c, t) =>
            {
                log.Add("changed");
                lastChange = (p, c, t);
            };
            enemy.HealthResource.CurrentHasDepleted += () => log.Add("depleted");
            sim.EnemyDefeated += _ => log.Add("defeated");

            sim.Advance(0.1f); // one tick: the Strike kills the lone enemy

            Assert.That(log, Is.EqualTo(new[] { "changed", "depleted", "defeated" }));
            Assert.That(lastChange.previous, Is.EqualTo(enemy.MaxHealth));
            Assert.That(lastChange.current, Is.EqualTo(0f));
            Assert.That(lastChange.total, Is.EqualTo(enemy.MaxHealth));
            Assert.That(enemy.IsDown, Is.True);
        }

        [Test]
        public void DamageThatDoesNotKill_MovesTheResourceAndLeavesTheEnemyStanding()
        {
            var hero = new FakeHero { PhysicalDamage = 5f, AttackSpeed = 10f, MagicalDamage = 0f, Resource = 0f };
            var sim = new EncounterSimulation(hero, Profiles.Solo(EnemyArchetype.Brute),
                new ConstantRollSource(0f), Behaviours.Engaging(5), ShortBeat());
            var enemy = sim.Enemies[0];
            var changes = 0;
            enemy.HealthResource.CurrentHasChanged += (_, _, _) => changes++;

            sim.Advance(0.1f);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(enemy.Health, Is.LessThan(enemy.MaxHealth));
            Assert.That(sim.Enemies, Does.Contain(enemy));
        }
    }
}
