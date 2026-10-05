using NUnit.Framework;
using Submodules.Utility.Persistence;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// The starter kit: a created hero gets the template's authored items and coins, placed the way a Drop
    /// is; a loaded hero never does; an empty, unknown or oversized kit leaves a valid hero.
    /// </summary>
    [TestFixture]
    public sealed class StarterKitTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private string gearId;
        private string potionId;
        private readonly InMemorySaveStore store = new();

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);

            var catalog = GameBoot.Load().Catalog;
            gearId = catalog.OfCategory(ItemCategory.Equipment).First().Id;
            potionId = catalog.OfCategory(ItemCategory.Consumable).First().Id;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        // The config's default template with a kit authored on a copy; the shipped asset is never touched.
        private void AuthorKit(int goldCoins, params (string Id, uint Amount)[] items)
        {
            var template = UnityEngine.Object.Instantiate(config.DefaultHero);
            created.Add(template);

            var so = new SerializedObject(template);
            var kit = so.FindProperty("starterItems");
            kit.arraySize = items.Length;
            for (var i = 0; i < items.Length; i++)
            {
                var element = kit.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("<DefinitionId>k__BackingField").stringValue = items[i].Id;
                element.FindPropertyRelative("<Rarity>k__BackingField").intValue = (int)ItemRarity.Common;
                element.FindPropertyRelative("<Amount>k__BackingField").intValue = (int)items[i].Amount;
            }

            so.FindProperty("starterCoins").FindPropertyRelative("<Gold>k__BackingField").intValue = goldCoins;
            _ = so.ApplyModifiedPropertiesWithoutUndo();

            var configSo = new SerializedObject(config);
            configSo.FindProperty("<DefaultHero>k__BackingField").objectReferenceValue = template;
            _ = configSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private (TestGame Game, HeroSaveService Saves) NewGame()
        {
            var game = TestGame.Create(config);
            var saves = new HeroSaveService(game.Session, game.Items, config, game.Simulation,
                store, new JsonUtilitySerializer());

            return (game, saves);
        }

        private static int Held(Hero hero, string id) =>
            hero.Equipment.StoredPackages.Values.Concat(hero.Inventory.StoredPackages.Values)
                .Where(package => package.Item.DefinitionId == id)
                .Sum(package => (int)package.Amount);

        [Test]
        public void ACreatedHero_HoldsTheAuthoredItemsAndCoins_WithGearWornInAnEmptySlot()
        {
            AuthorKit(goldCoins: 2, (gearId, 1u), (potionId, 3u));
            var (game, saves) = NewGame();

            var hero = saves.Create("Aria");
            _ = saves.Load(hero.Id);

            Assert.That(game.Hero.Equipment.StoredPackages.Values.Any(p => p.Item.DefinitionId == gearId), Is.True,
                "an Equipment item takes the empty slot it fits");
            Assert.That(Held(game.Hero, potionId), Is.EqualTo(3));
            Assert.That(game.Hero.Wallet.Balance.Total, Is.EqualTo(2u * Currency.ironToGold));
        }

        [Test]
        public void ALoadedHero_NeverReceivesTheKitAgain()
        {
            AuthorKit(goldCoins: 2, (potionId, 3u));
            var (first, saves) = NewGame();
            var hero = saves.Create("Aria");

            _ = saves.Load(hero.Id);
            Assert.That(saves.Save(), Is.True);
            var (second, secondSaves) = NewGame();
            _ = secondSaves.Load(hero.Id);

            Assert.That(Held(second.Hero, potionId), Is.EqualTo(3), "the kit once, not once per load");
            Assert.That(second.Hero.Wallet.Balance.Total, Is.EqualTo(2u * Currency.ironToGold));
            Assert.That(Held(first.Hero, potionId), Is.EqualTo(3));
        }

        [Test]
        public void ALoadedTemplate_IsNotACreatedHero_AndGetsNoKit()
        {
            AuthorKit(goldCoins: 2, (potionId, 3u));
            var (game, _) = NewGame();

            Assert.That(game.Session.TryLoad(config.DefaultHero), Is.True);

            Assert.That(Held(game.Hero, potionId), Is.Zero);
            Assert.That(game.Hero.Wallet.Balance.Total, Is.Zero);
        }

        [Test]
        public void AnEmptyKit_CreatesAHeroWithAnEmptyBag_AndNoError()
        {
            AuthorKit(goldCoins: 0);
            var (game, saves) = NewGame();

            var hero = saves.Create("Bare");
            _ = saves.Load(hero.Id);

            Assert.That(game.Hero.Inventory.StoredPackages, Is.Empty);
            Assert.That(game.Hero.Equipment.StoredPackages, Is.Empty);
            Assert.That(game.Hero.Wallet.Balance.Total, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ADefinitionTheCatalogDoesNotHold_IsLoggedAndLeftOut_AndTheRestOfTheKitStays()
        {
            AuthorKit(goldCoins: 0, ("deleted-in-a-patch", 1u), (potionId, 2u));
            LogAssert.Expect(LogType.Warning, new Regex("no item 'deleted-in-a-patch'"));
            var (game, saves) = NewGame();

            var hero = saves.Create("Aria");
            _ = saves.Load(hero.Id);

            Assert.That(Held(game.Hero, potionId), Is.EqualTo(2));
        }

        [Test]
        public void AKitThatCannotFit_IsLogged_AndTheHeroStaysValid()
        {
            var tooMany = Enumerable.Repeat((gearId, 1u), 400).ToArray();
            AuthorKit(goldCoins: 0, tooMany);
            LogAssert.Expect(LogType.Warning, new Regex("no room for"));
            var (game, saves) = NewGame();

            var hero = saves.Create("Hoarder");
            var result = saves.Load(hero.Id);

            Assert.That(result.Entered, Is.True);
            Assert.That(Held(game.Hero, gearId), Is.GreaterThan(0));
            Assert.That(Held(game.Hero, gearId), Is.LessThan(400));
        }

        [Test]
        public void TheKit_TopsTheHeroUp_SoGearThatRaisesTheMaximumsDoesNotLeaveItWounded()
        {
            var game = TestGame.Create(config);
            _ = game.Hero.GetResource(StatName.Health).RemoveFromCurrent(5f);
            _ = game.Hero.GetResource(StatName.Resource).RemoveFromCurrent(5f);

            StarterKit.Apply(game.Hero, config.DefaultHero, game.Items);

            Assert.That(game.Hero.GetResource(StatName.Health).CurrentValue, Is.EqualTo(game.Hero.GetResource(StatName.Health).TotalValue));
            Assert.That(game.Hero.GetResource(StatName.Resource).CurrentValue, Is.EqualTo(game.Hero.GetResource(StatName.Resource).TotalValue));
        }
    }
}
