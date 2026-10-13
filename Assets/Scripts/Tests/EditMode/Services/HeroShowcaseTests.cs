using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using UnityEditor;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// What the Main Menu's text area says about a hero (issue #253), tested without a scene: the average item
    /// level of the worn Equipment as a pure function, and the four lines read off the Session's hero.
    /// </summary>
    [TestFixture]
    public sealed class HeroShowcaseTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private TestGame game;
        private LocationConfig thornwood;

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);
            thornwood = TestLocations.Create(config, created, "thornwood");
            var so = new SerializedObject(thornwood);
            so.FindProperty("displayName").stringValue = "Thornwood";
            _ = so.ApplyModifiedPropertiesWithoutUndo();
            TestLocations.Author(config, thornwood);
            game = TestGame.Create(config);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private IItemCatalog Catalog => game.Items.Catalog;

        private ItemInstance Gear(EquipmentType type, int itemLevel) =>
            new(Catalog.OfCategory(ItemCategory.Equipment).First(d => d.EquipmentType == type).Id, ItemRarity.Common, itemLevel, null);

        private ItemInstance Coin()
        {
            var definition = Catalog.OfCategory(ItemCategory.Currency).First();
            return ItemInstance.Coin(definition.Id, definition.CurrencyType);
        }

        private void Wear(EquipmentType type, int itemLevel)
        {
            var package = new Package(game.Hero.Equipment, Gear(type, itemLevel), 1u);
            Assert.That(game.Hero.Equipment.TryAddToContainer(ref package), Is.True, "premise: the item is worn");
        }

        private string Text() => HeroShowcase.Text(game.Hero, game.Simulation.Locations, Catalog);

        // ── average worn item level ─────────────────────────────────────────

        [Test]
        public void AverageWornItemLevel_IsTheMeanOverTheItems()
        {
            var average = HeroShowcase.AverageWornItemLevel(new[] { Gear(EquipmentType.Helm, 10), Gear(EquipmentType.Chest, 25) }, Catalog);

            Assert.That(average, Is.EqualTo(17.5f));
        }

        [Test]
        public void AverageWornItemLevel_LeavesCoinsOut()
        {
            var average = HeroShowcase.AverageWornItemLevel(new[] { Gear(EquipmentType.Helm, 10), Coin() }, Catalog);

            Assert.That(average, Is.EqualTo(10f), "a coin is level 0 and would drag the mean to 5");
        }

        [Test]
        public void AverageWornItemLevel_IsBlank_WithNothingWorn_OrOnlyCoins()
        {
            Assert.That(HeroShowcase.AverageWornItemLevel(new ItemInstance[0], Catalog), Is.Null);
            Assert.That(HeroShowcase.AverageWornItemLevel(new[] { Coin() }, Catalog), Is.Null);
        }

        // ── the text ────────────────────────────────────────────────────────

        [Test]
        public void Text_OfAHeroNeverSentWithNothingWorn_LeavesLocationAndGearLevelBlank()
        {
            Assert.That(game.Hero.Equipment.StoredPackages, Is.Empty, "premise: a new hero wears nothing");

            Assert.That(Text(), Is.EqualTo("Location:\nCorpse: none\nKills: 0\nGear level:"));
        }

        [Test]
        public void Text_NamesTheLocationTheHeroIsSetToSendTo()
        {
            game.Hero.SelectedLocation = thornwood;

            Assert.That(Text(), Does.Contain("Location: Thornwood\n"));
        }

        [Test]
        public void Text_NamesTheLocationAWaitingCorpseLiesAt()
        {
            game.Hero.Corpse.Bury(game.Simulation.Locations.ProfileFor(thornwood), null);

            Assert.That(Text(), Does.Contain("Corpse: Thornwood\n"));
        }

        [Test]
        public void Text_ShowsTheLifetimeKills()
        {
            game.Hero.AddKills(42);

            Assert.That(Text(), Does.Contain("Kills: 42\n"));
        }

        [Test]
        public void Text_AveragesTheWornItemsOnly_NotTheThirteenSlots()
        {
            Wear(EquipmentType.Helm, 10);
            Wear(EquipmentType.Chest, 15);

            Assert.That(Text(), Does.EndWith("Gear level: 12.5"));
        }
    }
}
