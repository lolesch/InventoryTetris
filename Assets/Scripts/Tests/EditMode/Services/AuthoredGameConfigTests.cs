using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// Pins the authored <c>Assets/Resources/GameConfig.asset</c> to what the scene-scoped providers
    /// authored when it was carried over (issue #108): the same references, sizes and tuning, so the
    /// move changed nothing. The item data is read from here by <c>ItemService</c> (#109), and the
    /// default hero, the container sizes and the Behaviour Profile defaults by <c>SessionBuilder</c>
    /// (#112); the rest of the tuning and the Locations are still held by their providers until those
    /// are retired, so those values must change in both places or in neither.
    /// </summary>
    [TestFixture]
    public sealed class AuthoredGameConfigTests
    {
        private GameConfig config;

        [SetUp]
        public void SetUp() => config = Resources.Load<GameConfig>(GameConfig.ResourceKey);

        [Test]
        public void TheRootAssetExists_AndEveryAuthoredReferenceIsAssigned()
        {
            Assert.That(config, Is.Not.Null, "Assets/Resources/GameConfig.asset is missing");

            Assert.That(config.DefaultHero, Is.Not.Null);
            Assert.That(config.ItemTypeData, Is.Not.Null);
            Assert.That(config.Catalog, Is.Not.Null);
            Assert.That(config.ItemCategoryDistribution, Is.Not.Null);
            Assert.That(config.ItemRarityDistribution, Is.Not.Null);
            Assert.That(config.CurrencyTypeDistribution, Is.Not.Null);
            Assert.That(config.CurrencyDropTable, Is.Not.Null);
        }

        [Test]
        public void TheDefaultHero_IsTheAuthoredDefaultHeroAsset()
        {
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(config.DefaultHero),
                Is.EqualTo("Assets/Scripts/InventorySystem/Characters/DefaultHero.asset"),
                "the scene's player component used to serialize this template; the boot builds the hero from it now");
        }

        [Test]
        public void CurrencyIcons_AreTheFourScenePlacedSprites_InTheOrderTheProviderIndexes()
        {
            Assert.That(config.CurrencyIcons, Has.Count.EqualTo(4));

            foreach (var icon in config.CurrencyIcons)
                Assert.That(icon, Is.Not.Null);
        }

        [Test]
        public void ContainerSizes_AreTheSceneAuthoredOnes()
        {
            Assert.That(config.EquipmentSize, Is.EqualTo(new Vector2Int(14, 1)));
            Assert.That(config.InventorySize, Is.EqualTo(new Vector2Int(10, 6)));
            Assert.That(config.StashSize, Is.EqualTo(new Vector2Int(10, 13)));
            Assert.That(config.SupplySize, Is.EqualTo(new Vector2Int(10, 7)), "the scene's storeSize");
            Assert.That(config.SoldSize, Is.EqualTo(config.SupplySize), "the Sold container is sized like the Supply (sold-tab spec); the Sell Basket's size is not carried");
        }

        [Test]
        public void SimulationDefaults_AreTheSceneAuthoredOnes()
        {
            Assert.That(config.CastCost, Is.EqualTo(16f));
            Assert.That(config.SimSpeed, Is.EqualTo(1f));
            Assert.That(config.Engagement, Is.EqualTo(3));
            Assert.That(config.CastThreshold, Is.Zero);
            Assert.That(config.OriginWeight, Is.EqualTo(0.5f), "home and nearness pull equally");
            Assert.That(config.LootFilterMinimum, Is.EqualTo(ItemRarity.Common));
            Assert.That(config.XpLossFraction, Is.EqualTo(0.25f));
            Assert.That(config.CurrencyFeeFraction, Is.EqualTo(0.5f));
        }

        [Test]
        public void TheGroundFade_IsTheOneTheGridAndTheListUsedToSerialize()
        {
            var fade = config.GroundFade;

            Assert.That(fade.AlphaOf(age: 7), Is.EqualTo(1f), "the newest eight are fully opaque");
            Assert.That(fade.AlphaOf(age: 8), Is.EqualTo(0.98f).Within(1e-6f), "then a 0.02 step per drop");
            Assert.That(fade.AlphaOf(age: 1000), Is.EqualTo(0.3f), "the oldest rest at the minimum");
        }

        [Test]
        public void TheEncounterTuning_DefaultsToTheStandardPlaceholders()
        {
            var standard = EncounterTuning.Standard();
            var tuning = config.Tuning;

            Assert.That(tuning.DelayFirstSpawn, Is.EqualTo(standard.DelayFirstSpawn));
            Assert.That(tuning.DamageSpread, Is.EqualTo(standard.DamageSpread));
            Assert.That(tuning.Arena.Radius, Is.EqualTo(10f), "the authored asset keeps the arena the game plays on");
            Assert.That(tuning.Arena.SpawnMargin, Is.EqualTo(standard.Arena.SpawnMargin));
            Assert.That(tuning.Arena.StopJitter, Is.EqualTo(standard.Arena.StopJitter));
            Assert.That(tuning.Arena.BearingJitter, Is.EqualTo(standard.Arena.BearingJitter));
            Assert.That(tuning.Arena.HeroStrikeRange, Is.EqualTo(standard.Arena.HeroStrikeRange));
            Assert.That(tuning.Arena.MovementSpeedScale, Is.EqualTo(standard.Arena.MovementSpeedScale));
            Assert.That(tuning.Cast.Range, Is.EqualTo(standard.Cast.Range));
            Assert.That(tuning.Cast.Shape.Kind, Is.EqualTo(standard.Cast.Shape.Kind));
            Assert.That(tuning.Cast.Shape.Radius, Is.EqualTo(standard.Cast.Shape.Radius));
            Assert.That(tuning.Cast.Shape.InnerRadius, Is.EqualTo(standard.Cast.Shape.InnerRadius));
            Assert.That(tuning.Cast.Size, Is.EqualTo(standard.Cast.Size));
            Assert.That(tuning.Cast.Anchor, Is.EqualTo(standard.Cast.Anchor));
        }

        [Test]
        public void TheAuthoredLists_CannotBeWrittenThroughTheReturnedView()
        {
            // With domain reload disabled a write to the ScriptableObject survives Stop.
            Assert.That(config.Locations, Is.Not.InstanceOf<LocationConfig[]>());
            Assert.That(config.CurrencyIcons, Is.Not.InstanceOf<System.Collections.Generic.List<Sprite>>());
            Assert.That(((System.Collections.IList)config.Locations).IsReadOnly, Is.True);
        }

        [Test]
        public void Locations_AreTheTwoAuthoredAssets()
        {
            Assert.That(config.Locations, Has.Count.EqualTo(2));

            foreach (var location in config.Locations)
                Assert.That(location, Is.Not.Null);
        }
    }
}
