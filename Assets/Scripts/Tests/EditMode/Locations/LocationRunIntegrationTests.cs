using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Simulation;
using UnityEditor;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Locations
{
    /// <summary>
    /// The <see cref="LocationConfig"/> assets, run (issue #25 acceptance: "A Run against each
    /// produces that Location's source level ... its Roster and spawn cadence in the sim, and its
    /// authored enemy difficulty"). The authored ScriptableObject maps onto an
    /// <see cref="EncounterProfile"/> the engine-free <see cref="EncounterSimulation"/> runs; these
    /// tests drive that sim with an inert hero (nothing dies, the spawn side isolated) and assert
    /// the sim's fielded enemies, counts and difficulty come from the authored Location.
    /// </summary>
    [TestFixture]
    public sealed class LocationRunIntegrationTests
    {
        private const string DataPath = "Assets/Scripts/InventorySystem/Locations/Data/";

        private LocationConfig Load(string name)
        {
            var config = AssetDatabase.LoadAssetAtPath<LocationConfig>(DataPath + name + ".asset");
            if (config == null)
                Assert.Ignore($"no authored Location asset at '{DataPath}{name}.asset' to run");
            return config;
        }

        private static LocationFakes.FakeHero InertHero() => new()
        {
            PhysicalDamage = 0f,
            MagicalDamage = 0f,
            Resource = 0f,
            AttackSpeed = 0.01f,
        };

        [Test]
        public void AThornwoodRun_SpawnsAtItsAuthoredCadence()
        {
            var config = Load("Thornwood");
            var profile = config.ToProfile();

            // Thornwood: Packed Brute, PackBatch [2,3], SpawnInterval 2.4, engagement 3.
            // Initial spawn 2, then a Pack of 2 overshoots the engagement target and it holds.
            var sim = new EncounterSimulation(InertHero(), profile, new LocationFakes.ConstantRollSource(0f), engagementTarget: 3);

            for (var i = 0; i < 200; i++) sim.Advance(0.1f); // 20 s

            Assert.That(sim.Profile, Is.SameAs(profile), "the sim runs the Location's profile");
            Assert.That(sim.AliveEnemyCount, Is.EqualTo(4), "initial 2 + a Pack of 2, overshooting engagement 3");
            Assert.That(sim.EncountersCleared, Is.EqualTo(0), "nothing died, so nothing cleared");
        }

        [Test]
        public void AThornwoodRun_FieldsEnemiesAtTheAuthoredSourceLevel()
        {
            var config = Load("Thornwood");
            var profile = config.ToProfile();

            var sim = new EncounterSimulation(InertHero(), profile, new LocationFakes.ConstantRollSource(0f), engagementTarget: 3);
            for (var i = 0; i < 30; i++) sim.Advance(0.1f); // few ticks to field the initial spawn

            Assert.That(sim.Enemies.Count, Is.GreaterThan(0), "the initial spawn fields enemies");
            foreach (var enemy in sim.Enemies)
            {
                Assert.That(enemy.MaxHealth, Is.EqualTo(EnemyArchetypes.Of(enemy.Archetype).Health.At(profile.SourceLevel)),
                    $"a {enemy.Archetype} at source level {profile.SourceLevel} uses the shared curve");
            }
        }

        [Test]
        public void AnAshfallRun_IsSourcedFromTheHarderLocation()
        {
            var config = Load("Ashfall");
            var profile = config.ToProfile();

            var sim = new EncounterSimulation(InertHero(), profile, new LocationFakes.ConstantRollSource(0f), engagementTarget: 3);

            for (var i = 0; i < 200; i++) sim.Advance(0.1f);

            // Ashfall: Packed Skirmisher, PackBatch [3,5] -> the first Pack arrives as 3+ and the
            // engagement target (3) is overshot; alive is held at the initial spawn (2) plus a Pack.
            Assert.That(sim.Profile, Is.SameAs(profile));
            Assert.That(sim.AliveEnemyCount, Is.EqualTo(5),
                "initial 2 + a Pack of 3, overshooting engagement 3");
            Assert.That(sim.EncountersCleared, Is.EqualTo(0));
        }
    }
}