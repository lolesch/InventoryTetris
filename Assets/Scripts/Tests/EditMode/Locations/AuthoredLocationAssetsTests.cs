using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Simulation;
using UnityEditor;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Locations
{
    /// <summary>
    /// Validates the two authored <see cref="LocationConfig"/> assets shipped for the MVP
    /// (issue #25 acceptance: "Two authored assets exist and pass validation"; "The harder
    /// Location's table rolls a richer rarity spread than the easy one"). Thornwood is the easy
    /// Location (low source level, small Roster, trickle), Ashfall the hard one (higher source
    /// level, richer loot table, bigger Roster, Packs, more XP).
    ///
    /// Like <c>ItemCatalogValidationTests</c>, this fixture reports <see cref="Assert.Ignore"/>
    /// when the assets are missing (a code-only checkout stays green) and becomes hard assertions
    /// once they exist.
    /// </summary>
    [TestFixture]
    public sealed class AuthoredLocationAssetsTests
    {
        private const string DataPath = "Assets/Scripts/InventorySystem/Locations/Data/";

        private LocationConfig thornwood;
        private LocationConfig ashfall;

        [SetUp]
        public void LoadAssets()
        {
            thornwood = AssetDatabase.LoadAssetAtPath<LocationConfig>(DataPath + "Thornwood.asset");
            ashfall = AssetDatabase.LoadAssetAtPath<LocationConfig>(DataPath + "Ashfall.asset");
            if (thornwood == null || ashfall == null)
                Assert.Ignore($"no authored Location assets under '{DataPath}' - nothing to validate yet");
        }

        [Test]
        public void BothLocations_PassValidation()
        {
            Assert.That(thornwood.Validate(), Is.Empty, "Thornwood");
            Assert.That(ashfall.Validate(), Is.Empty, "Ashfall");
        }

        [Test]
        public void BothLocations_CarryAStableNonAssetGuidId()
        {
            Assert.That(thornwood.Id, Is.Not.Empty, "Thornwood");
            Assert.That(thornwood.Id, Is.Not.EqualTo(AssetGuid(thornwood)), "Thornwood id must not be the Unity asset GUID");
            Assert.That(thornwood.Id, Is.Not.EqualTo(thornwood.name), "Thornwood id must not be the asset name");

            Assert.That(ashfall.Id, Is.Not.Empty, "Ashfall");
            Assert.That(ashfall.Id, Is.Not.EqualTo(AssetGuid(ashfall)), "Ashfall id must not be the Unity asset GUID");
            Assert.That(ashfall.Id, Is.Not.EqualTo(ashfall.name), "Ashfall id must not be the asset name");
        }

        [Test]
        public void Thornwood_IsTheEasyLocation()
        {
            var profile = thornwood.ToProfile();

            Assert.That(profile.SourceLevel, Is.LessThan(ashfall.ToProfile().SourceLevel),
                "easy Location must have the lower source level");
            Assert.That(profile.Packed, Is.EqualTo(EnemyArchetype.Brute));
            Assert.That(profile.RosterSkirmisher.Min, Is.GreaterThan(0), "a trickle still fields Skirmishers");
        }

        [Test]
        public void Ashfall_IsTheHardLocation()
        {
            var profile = ashfall.ToProfile();

            Assert.That(profile.SourceLevel, Is.GreaterThan(thornwood.ToProfile().SourceLevel),
                "hard Location must have the higher source level");
            Assert.That(profile.Packed, Is.EqualTo(EnemyArchetype.Skirmisher));
            Assert.That(profile.PackBatch.Min, Is.GreaterThan(1), "Ashfall Packs, Thornwood merely trickles");
        }

        [Test]
        public void Ashfall_RarityTable_IsRicherThanThornwood()
        {
            var easyOdds = thornwood.ToProfile().Table.RarityOdds;
            var hardOdds = ashfall.ToProfile().Table.RarityOdds;

            // Rarity odds are in enum order: NoDrop, Common, Magic, Rare, Unique (ItemRarity.cs).
            var easyRich = Rare(easyOdds) + Unique(easyOdds);
            var hardRich = Rare(hardOdds) + Unique(hardOdds);
            Assert.That(hardRich, Is.GreaterThan(easyRich),
                "Ashfall must roll Rare+Unique more often than Thornwood");

            var easyCommon = Common(easyOdds);
            var hardCommon = Common(hardOdds);
            Assert.That(easyCommon, Is.GreaterThan(hardCommon),
                "Ashfall must roll Common less often than Thornwood");
        }

        // ─── helpers ────────────────────────────────────────────────────────

        private static string AssetGuid(UnityEngine.Object asset) =>
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));

        /// <summary>Index of a rarity in the <c>RarityOdds</c> vector — enum declaration order, not enum value.</summary>
        private static int IndexOf(ItemRarity rarity) =>
            Array.IndexOf((ItemRarity[])Enum.GetValues(typeof(ItemRarity)), rarity);

        private static float Common(IReadOnlyList<float> rarityOdds) => rarityOdds[IndexOf(ItemRarity.Common)];
        private static float Rare(IReadOnlyList<float> rarityOdds) => rarityOdds[IndexOf(ItemRarity.Rare)];
        private static float Unique(IReadOnlyList<float> rarityOdds) => rarityOdds[IndexOf(ItemRarity.Unique)];
    }
}