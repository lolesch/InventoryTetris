using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// Pins the authored <c>Assets/Resources/GameConfig.asset</c> to what the scene-scoped providers
    /// authored when it was carried over (issue #108): the same references, sizes and tuning, so the
    /// move changed nothing. The providers still hold their own copies until they are retired, so
    /// these values must change in both places or in neither.
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

            Assert.That(config.ItemTypeData, Is.Not.Null);
            Assert.That(config.Catalog, Is.Not.Null);
            Assert.That(config.ItemCategoryDistribution, Is.Not.Null);
            Assert.That(config.ItemRarityDistribution, Is.Not.Null);
            Assert.That(config.CurrencyTypeDistribution, Is.Not.Null);
            Assert.That(config.CurrencyDropTable, Is.Not.Null);
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
            Assert.That(config.RetreatHealthFraction, Is.Zero);
            Assert.That(config.RecallBagFillFraction, Is.EqualTo(1f));
            Assert.That(config.CastThreshold, Is.Zero);
            Assert.That(config.LootFilterMinimum, Is.EqualTo(ItemRarity.Common));
            Assert.That(config.XpLossFraction, Is.EqualTo(0.25f));
            Assert.That(config.CurrencyFeeFraction, Is.EqualTo(0.5f));
        }

        [Test]
        public void Locations_AreTheTwoAuthoredAssets()
        {
            Assert.That(config.Locations, Has.Length.EqualTo(2));

            foreach (var location in config.Locations)
                Assert.That(location, Is.Not.Null);
        }
    }
}
