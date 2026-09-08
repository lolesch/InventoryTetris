using System.Collections.Generic;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Distributions;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Locations
{
    /// <summary>
    /// The seam between <see cref="LocationConfig"/> (the authored ScriptableObject) and the
    /// engine-free <see cref="EncounterProfile"/> the sim runs (issue #25). <see cref="ToProfile"/>
    /// maps the authored fields onto the profile — source level, packed archetype, the four
    /// <see cref="IntRange"/>s, spawn cadence — and wraps the two authored distributions into the
    /// profile's <see cref="LootTable"/>.
    /// </summary>
    [TestFixture]
    public sealed class LocationConfigToProfileTests
    {
        private static LocationConfig Build(
            string id = "location.test",
            int sourceLevel = 4,
            EnemyArchetype packed = EnemyArchetype.Brute,
            Vector2Int rosterBrute = default, Vector2Int rosterSkirmisher = default,
            Vector2Int packBatch = default,
            float packedSpawnWeight = 0.5f,
            float spawnInterval = 2f,
            float spawnJitter = 0.1f,
            ItemCategoryDistribution category = null,
            ItemRarityDistribution rarity = null)
        {
            var config = ScriptableObject.CreateInstance<LocationConfig>();
            config.Author(
                id, "Test Down", sourceLevel,
                category, rarity,
                packed,
                rosterBrute == default ? new Vector2Int(3, 4) : rosterBrute,
                rosterSkirmisher == default ? new Vector2Int(1, 2) : rosterSkirmisher,
                packBatch == default ? new Vector2Int(2, 3) : packBatch,
                packedSpawnWeight, spawnInterval, spawnJitter);
            return config;
        }

        [Test]
        public void ToProfile_MapsTheAuthoredFields()
        {
            var category = ScriptableObject.CreateInstance<ItemCategoryDistribution>();
            var rarity = ScriptableObject.CreateInstance<ItemRarityDistribution>();

            var profile = Build(
                category: category, rarity: rarity,
                sourceLevel: 4,
                packed: EnemyArchetype.Skirmisher,
                rosterBrute: new Vector2Int(3, 3), rosterSkirmisher: new Vector2Int(7, 7),
                packBatch: new Vector2Int(2, 4),
                packedSpawnWeight: 0.7f,
                spawnInterval: 3.6f,
                spawnJitter: 0.2f).ToProfile();

            Assert.That(profile.SourceLevel, Is.EqualTo(4));
            Assert.That(profile.Packed, Is.EqualTo(EnemyArchetype.Skirmisher));
            Assert.That(profile.RosterBrute.Min, Is.EqualTo(3));
            Assert.That(profile.RosterBrute.Max, Is.EqualTo(3));
            Assert.That(profile.RosterSkirmisher.Min, Is.EqualTo(7));
            Assert.That(profile.RosterSkirmisher.Max, Is.EqualTo(7));
            Assert.That(profile.PackBatch.Min, Is.EqualTo(2));
            Assert.That(profile.PackBatch.Max, Is.EqualTo(4));
            Assert.That(profile.PackedSpawnWeight, Is.EqualTo(0.7f));
            Assert.That(profile.SpawnInterval, Is.EqualTo(3.6f));
            Assert.That(profile.SpawnJitter, Is.EqualTo(0.2f));
        }

        [Test]
        public void ToProfile_WrapsTheDistributionIntoTheLootTable()
        {
            var category = ScriptableObject.CreateInstance<ItemCategoryDistribution>();
            var rarity = ScriptableObject.CreateInstance<ItemRarityDistribution>();

            var profile = Build(category: category, rarity: rarity).ToProfile();

            Assert.That(profile.Table, Is.Not.Null);
            Assert.That(profile.Table.CategoryOdds, Is.SameAs(category.Probabilities),
                "CategoryOdds must come from the authored category distribution");
            Assert.That(profile.Table.RarityOdds, Is.SameAs(rarity.Probabilities),
                "RarityOdds must come from the authored rarity distribution");
        }

        [Test]
        public void ToProfile_ThrowsWhenTheIdIsMissing()
        {
            var config = Build(id: "  ");

            Assert.That(() => config.ToProfile(),
                Throws.InvalidOperationException.With.Message.Contains("id is empty"));
        }

        [Test]
        public void ToProfile_ThrowsWhenADistributionIsMissing()
        {
            // Author with no distributions at all (Build passes null through to Author).
            var config = Build(category: null, rarity: null);

            Assert.That(() => config.ToProfile(),
                Throws.InvalidOperationException.With.Message.Contains("loot table"));
        }
    }
}