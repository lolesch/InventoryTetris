using NUnit.Framework;
using Submodules.Utility.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// What the Pause Menu's Save &amp; Exit does, without a scene: in Town it writes and asks for the menu, in the
    /// Field it asks first and then Recalls with the loot kept, and a failed write keeps the player in the game
    /// with the cause. The Run's own Recall is a save point too, so the explicit write that follows is the one
    /// whose result the player is told.
    /// </summary>
    [TestFixture]
    public sealed class SaveAndExitTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private LocationConfig thornwood;
        private InMemorySaveStore inner;
        private FailableStore store;
        private DateTime now;
        private TestGame game;
        private HeroSaveService saves;
        private string heroId;
        private int menuLoads;

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);
            thornwood = TestLocations.Create(config, created, "thornwood");
            TestLocations.Author(config, thornwood);
            inner = new InMemorySaveStore();
            store = new FailableStore(inner);
            now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
            game = TestGame.Create(config);
            saves = game.SavesOver(config, store, () => now);
            heroId = saves.Create("Aria").Id;
            _ = saves.Load(heroId);
            menuLoads = 0;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private sealed class FailableStore : ISaveStore
        {
            private readonly ISaveStore real;

            public FailableStore(ISaveStore real) => this.real = real;

            public bool FailWrites { get; set; }

            public bool TryRead(string key, out string content) => real.TryRead(key, out content);
            public bool TryReadBackup(string key, out string content) => real.TryReadBackup(key, out content);
            public bool Delete(string key) => real.Delete(key);
            public IReadOnlyCollection<string> Keys() => real.Keys();
            public bool SetAside(string key) => real.SetAside(key);
            public void AppendSideFile(string fileName, string text) => real.AppendSideFile(fileName, text);

            public void Write(string key, string content)
            {
                if (FailWrites)
                    throw new IOException("the disk is full");

                real.Write(key, content);
            }
        }

        private void LoadMenu() => menuLoads++;

        private void GivePotions(uint amount)
        {
            var id = game.Items.Catalog.OfCategory(ItemCategory.Consumable).First().Id;
            var package = new Package(game.Hero.Inventory, new ItemInstance(id, ItemRarity.Common, 1, null), amount);
            Assert.That(game.Hero.Inventory.TryAddToContainer(ref package), Is.True, "premise: the potions fit");
        }

        private static bool BagHoldsPotions(Hero hero) =>
            hero.Inventory.StoredPackages.Values.Any(p => hero.Inventory.ViewOf(p.Item).Definition.Category == ItemCategory.Consumable);

        [Test]
        public void InTown_NothingNeedsConfirming_TheHeroIsWritten_AndTheMenuLoads()
        {
            now = now.AddMinutes(5);

            Assert.That(SaveAndExit.NeedsConfirmation(game.Simulation), Is.False);

            var result = SaveAndExit.Leave(game.Simulation, saves, LoadMenu);

            Assert.That(result.Wrote, Is.True);
            Assert.That(menuLoads, Is.EqualTo(1));
            Assert.That(saves.List().Single().SavedAtUtc, Is.EqualTo(now));
        }

        [Test]
        public void InTheField_ALeaveNeedsConfirming()
        {
            game.Simulation.Send(thornwood);

            Assert.That(SaveAndExit.NeedsConfirmation(game.Simulation), Is.True);
        }

        [Test]
        public void InTheField_ALeaveRecallsWithTheLootKept_WritesTheHero_AndLoadsTheMenu()
        {
            game.Simulation.Send(thornwood);
            GivePotions(3u);
            now = now.AddMinutes(5);

            var result = SaveAndExit.Leave(game.Simulation, saves, LoadMenu);

            Assert.That(result.Wrote, Is.True);
            Assert.That(game.Simulation.Run.Phase, Is.EqualTo(RunPhase.InTown));
            Assert.That(menuLoads, Is.EqualTo(1));
            Assert.That(game.Hero.Corpse.Exists, Is.False, "a Recall, not a Death");

            var next = TestGame.Create(config);
            var nextSaves = next.SavesOver(config, inner, () => now);
            _ = nextSaves.Load(heroId);
            Assert.That(BagHoldsPotions(next.Hero), Is.True, "the banked loot is in the file");
            Assert.That(nextSaves.List().Single().SavedAtUtc, Is.EqualTo(now));
        }

        [Test]
        public void ALeave_RunsTheBeforeSaveStep_SoAPackageOnTheCursorGoesHome()
        {
            var normalised = 0;
            saves.AddBeforeSave(() => normalised++);

            _ = SaveAndExit.Leave(game.Simulation, saves, LoadMenu);

            Assert.That(normalised, Is.GreaterThan(0));
        }

        [Test]
        public void AFailedWrite_KeepsThePlayerInTheGame_NamesTheCause_AndTheRunIsAlreadyRecalled()
        {
            game.Simulation.Send(thornwood);
            GivePotions(3u);
            store.FailWrites = true;
            LogAssert.Expect(LogType.Error, new Regex("Could not save hero .*the disk is full"));
            LogAssert.Expect(LogType.Error, new Regex("Could not save hero .*the disk is full"));

            var result = SaveAndExit.Leave(game.Simulation, saves, LoadMenu);

            Assert.That(result.Wrote, Is.False);
            Assert.That(result.Failure, Is.EqualTo(SaveFailure.StoreFailed));
            Assert.That(result.Reason, Is.Not.Empty);
            Assert.That(menuLoads, Is.EqualTo(0), "nothing leaves with the banked loot unwritten");
            Assert.That(game.Simulation.Run.Phase, Is.EqualTo(RunPhase.InTown));
            Assert.That(BagHoldsPotions(game.Hero), Is.True, "the loot is still held");
        }

        [Test]
        public void ARetry_AfterTheStoreRecovers_WritesTheBankedLoot_AndLoadsTheMenu()
        {
            game.Simulation.Send(thornwood);
            GivePotions(3u);
            store.FailWrites = true;
            LogAssert.Expect(LogType.Error, new Regex("Could not save hero .*the disk is full"));
            LogAssert.Expect(LogType.Error, new Regex("Could not save hero .*the disk is full"));
            _ = SaveAndExit.Leave(game.Simulation, saves, LoadMenu);
            store.FailWrites = false;

            var retry = SaveAndExit.Leave(game.Simulation, saves, LoadMenu);

            Assert.That(retry.Wrote, Is.True);
            Assert.That(menuLoads, Is.EqualTo(1));

            var next = TestGame.Create(config);
            _ = next.SavesOver(config, inner, () => now).Load(heroId);
            Assert.That(BagHoldsPotions(next.Hero), Is.True);
        }

        [Test]
        public void AHeroThatDidNotComeFromASave_IsRefused_AndStaysInTheGame()
        {
            var unsaved = TestGame.Create(config);
            var orphan = unsaved.SavesOver(config, store, () => now);

            var result = SaveAndExit.Leave(unsaved.Simulation, orphan, LoadMenu);

            Assert.That(result.Failure, Is.EqualTo(SaveFailure.NotFromSave));
            Assert.That(menuLoads, Is.EqualTo(0));
        }
    }
}
