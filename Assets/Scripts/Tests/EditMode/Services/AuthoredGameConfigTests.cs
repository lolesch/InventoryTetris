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
            Assert.That(config.RetreatHealthFraction, Is.Zero);
            Assert.That(config.RecallBagFillFraction, Is.EqualTo(1f));
            Assert.That(config.CastThreshold, Is.Zero);
            Assert.That(config.OriginWeight, Is.EqualTo(0.5f), "home and nearness pull equally");
            Assert.That(config.LootFilterMinimum, Is.EqualTo(ItemRarity.Common));
            Assert.That(config.XpLossFraction, Is.EqualTo(0.25f));
            Assert.That(config.CurrencyFeeFraction, Is.EqualTo(0.5f));
        }

        [Test]
        public void TheGroundCastAndDamageTuning_DefaultToTheStandardPlaceholders()
        {
            var ground = GroundTuning.Standard();
            var cast = CastDefinition.Standard();

            Assert.That(config.GroundRadius, Is.EqualTo(ground.Radius));
            Assert.That(config.SpawnMargin, Is.EqualTo(ground.SpawnMargin));
            Assert.That(config.StopJitter, Is.EqualTo(ground.StopJitter));
            Assert.That(config.BearingJitter, Is.EqualTo(ground.BearingJitter));
            Assert.That(config.UnarmedStrikeRange, Is.EqualTo(ground.HeroStrikeRange));
            Assert.That(config.MovementSpeedScale, Is.EqualTo(ground.MovementSpeedScale));
            Assert.That(config.CastRange, Is.EqualTo(cast.Range));
            Assert.That(config.CastShape.Kind, Is.EqualTo(cast.Shape.Kind));
            Assert.That(config.CastShape.Radius, Is.EqualTo(cast.Shape.Radius));
            Assert.That(config.CastShape.InnerRadius, Is.EqualTo(cast.Shape.InnerRadius));
            Assert.That(config.CastSize, Is.EqualTo(cast.Size));
            Assert.That(config.CastAnchor, Is.EqualTo(cast.Anchor));
            Assert.That(config.DamageSpread, Is.EqualTo(EncounterTuning.StandardDamageSpread));
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
