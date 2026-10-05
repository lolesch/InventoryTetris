using NUnit.Framework;
using Submodules.Utility.Persistence;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// What a quit or an Editor Stop does to the hero: the logic behind <see cref="GameExit"/>'s handler,
    /// driven directly. That the real <c>Application.quitting</c> fires on an Editor Stop is not
    /// assertable here (a test cannot stop Play Mode); it is checked with the Play-exit check, and the
    /// result is in docs/agents/codebase-notes.md.
    /// </summary>
    [TestFixture]
    public sealed class GameExitTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private LocationConfig thornwood;
        private InMemorySaveStore store;

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);
            thornwood = TestLocations.Create(config, created, "thornwood");
            TestLocations.Author(config, thornwood);
            store = new InMemorySaveStore();
        }

        [TearDown]
        public void TearDown()
        {
            GameExit.Reset();

            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private (TestGame Game, HeroSaveService Saves, string Id) Playing()
        {
            var game = TestGame.Create(config);
            var saves = new HeroSaveService(game.Session, game.Items, config, game.Simulation, store, new JsonUtilitySerializer());
            var id = saves.Create("Aria").Id;
            _ = saves.Load(id);

            return (game, saves, id);
        }

        private (TestGame Game, HeroSaveService Saves) Reloaded(string id)
        {
            var game = TestGame.Create(config);
            var saves = new HeroSaveService(game.Session, game.Items, config, game.Simulation, store, new JsonUtilitySerializer());
            _ = saves.Load(id);

            return (game, saves);
        }

        private static int PotionsHeld(TestGame game)
        {
            var bag = game.Hero.Inventory;
            return bag.StoredPackages.Values
                .Where(p => bag.ViewOf(p.Item).Definition.Category == ItemCategory.Consumable)
                .Sum(p => (int)p.Amount);
        }

        private static void PutPotionsInBag(TestGame game, uint amount)
        {
            var id = game.Items.Catalog.OfCategory(ItemCategory.Consumable).First().Id;
            var package = new Package(game.Hero.Inventory, new ItemInstance(id, ItemRarity.Common, 1, null), amount);
            Assert.That(game.Hero.Inventory.TryAddToContainer(ref package), Is.True);
        }

        [Test]
        public void AQuitInTheField_RecallsTheRun_BanksWhatTheHeroHolds_AndSavesAHeroInTown()
        {
            var (game, saves, id) = Playing();
            game.Simulation.Send(thornwood);
            PutPotionsInBag(game, 3u);

            GameExit.SaveHero(game.Simulation, saves);

            Assert.That(game.Simulation.Run.Phase, Is.EqualTo(RunPhase.InTown));
            Assert.That(game.Simulation.Run.LastResult?.Outcome, Is.EqualTo(RunOutcome.Recalled));
            var next = Reloaded(id);
            Assert.That(PotionsHeld(next.Game), Is.EqualTo(3), "the Run's loot is banked in the file");
            Assert.That(next.Game.Hero.Corpse.Exists, Is.False);
        }

        [Test]
        public void AQuitInTown_WritesTheHero()
        {
            var (game, saves, id) = Playing();
            game.Hero.Wallet.Deposit(new Currency(40u));

            GameExit.SaveHero(game.Simulation, saves);

            Assert.That(Reloaded(id).Game.Hero.Wallet.Balance.Total, Is.EqualTo(40u));
        }

        [Test]
        public void AQuitWithNoSavedHero_WritesNothing_AndDoesNotThrow()
        {
            var game = TestGame.Create(config);
            var saves = new HeroSaveService(game.Session, game.Items, config, game.Simulation, store, new JsonUtilitySerializer());

            Assert.DoesNotThrow(() => GameExit.SaveHero(game.Simulation, saves));

            Assert.That(store.Keys(), Is.Empty);
        }

        [Test]
        public void TheQuitHandler_IsInstalledOnce_AndAResetReleasesIt()
        {
            var (game, saves, _) = Playing();

            GameExit.Install(game.Simulation, saves);
            GameExit.Install(game.Simulation, saves);
            Assert.That(GameExit.IsInstalled, Is.True);

            GameExit.Reset();

            Assert.That(GameExit.IsInstalled, Is.False);
        }
    }
}
