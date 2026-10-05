using NUnit.Framework;
using Submodules.Utility.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// The save service over an in-memory store - the one seam of the persistence spec. A game is built,
    /// changed and saved through the service; a second game, with its own service over the same store,
    /// loads it back and is compared by what a player can see. No test touches the real saves folder.
    /// </summary>
    [TestFixture]
    public sealed class HeroSaveServiceTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private LocationConfig thornwood;
        private InMemorySaveStore store;
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);
            thornwood = TestLocations.Create(config, created, "thornwood");
            TestLocations.Author(config, thornwood);
            store = new InMemorySaveStore();
            now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private sealed class Setup
        {
            public TestGame Game;
            public HeroSaveService Saves;
        }

        // A game and its save service over the shared store, as one boot builds them.
        private Setup NewSetup() => NewSetup(store);

        private Setup NewSetup(ISaveStore over)
        {
            var game = TestGame.Create(config);
            var saves = new HeroSaveService(game.Session, game.Items, config, game.Simulation.Locations, over,
                new JsonUtilitySerializer(), () => now);

            return new Setup { Game = game, Saves = saves };
        }

        private void Tick(int minutes = 1) => now = now.AddMinutes(minutes);

        // ── create, list, delete ────────────────────────────────────────────

        [Test]
        public void Create_WritesANewHeroUnderAGeneratedId_AndListShowsIt()
        {
            var saves = NewSetup().Saves;

            var hero = saves.Create("Aria");

            Assert.That(hero.Id, Is.Not.Empty);
            Assert.That(hero.Name, Is.EqualTo("Aria"));
            Assert.That(hero.Level, Is.EqualTo(1u));
            Assert.That(hero.Status, Is.EqualTo(LoadStatus.Loaded));
            Assert.That(hero.SavedAtUtc, Is.EqualTo(now));
            Assert.That(store.Keys(), Is.EqualTo(new[] { hero.Id }));
            Assert.That(saves.List().Single().Id, Is.EqualTo(hero.Id));
        }

        [Test]
        public void TwoHeroesWithTheSameName_GetTwoFiles()
        {
            var saves = NewSetup().Saves;

            var first = saves.Create("Twin");
            var second = saves.Create("Twin");

            Assert.That(second.Id, Is.Not.EqualTo(first.Id));
            Assert.That(saves.List().Count, Is.EqualTo(2));
        }

        [Test]
        public void List_PutsTheNewestSaveFirst_AndHidesTheAccountFile()
        {
            var saves = NewSetup().Saves;
            var older = saves.Create("Older");
            Tick();
            var newer = saves.Create("Newer");
            saves.SetLastSelected(older.Id);

            var list = saves.List();

            Assert.That(list.Select(hero => hero.Name), Is.EqualTo(new[] { "Newer", "Older" }));
            Assert.That(list.Select(hero => hero.Id), Does.Not.Contain("account"));
            Assert.That(newer.Id, Is.EqualTo(list[0].Id));
        }

        [Test]
        public void List_ShowsAFileItCannotRead_WithItsStatusAndNoName()
        {
            var saves = NewSetup().Saves;
            store.Write("deadbeef", "this is not a save");

            var hero = saves.List().Single();

            Assert.That(hero.Id, Is.EqualTo("deadbeef"));
            Assert.That(hero.Status, Is.EqualTo(LoadStatus.Corrupt));
            Assert.That(hero.Name, Is.Empty);
            Assert.That(hero.Level, Is.Zero);
        }

        [Test]
        public void Create_RefusesAnEmptyName()
        {
            var saves = NewSetup().Saves;

            Assert.Throws<ArgumentException>(() => saves.Create("   "));
            Assert.That(store.Keys(), Is.Empty);
        }

        [Test]
        public void Delete_RemovesTheHero_AndForgetsItAsTheLastSelected()
        {
            var saves = NewSetup().Saves;
            var hero = saves.Create("Gone");
            saves.SetLastSelected(hero.Id);

            Assert.That(saves.Delete(hero.Id), Is.True);

            Assert.That(saves.List(), Is.Empty);
            Assert.That(saves.LastSelectedHeroId, Is.Null);
            Assert.That(saves.Delete(hero.Id), Is.False, "nothing left to delete");
        }

        [Test]
        public void TheAccountFile_IsNeverAHeroId()
        {
            var saves = NewSetup().Saves;

            Assert.Throws<ArgumentException>(() => saves.Delete("account"));
            Assert.Throws<ArgumentException>(() => saves.Load("account"));
        }

        // ── rename ──────────────────────────────────────────────────────────

        [Test]
        public void Rename_ChangesTheDisplayName_AndNeverMovesTheFile()
        {
            var saves = NewSetup().Saves;
            var hero = saves.Create("Before");
            var filesBefore = store.Keys().ToArray();

            var renamed = saves.Rename(hero.Id, "  After ");

            Assert.That(renamed, Is.True);
            Assert.That(store.Keys(), Is.EqualTo(filesBefore));
            Assert.That(saves.List().Single().Name, Is.EqualTo("After"));
            Assert.That(saves.List().Single().Id, Is.EqualTo(hero.Id));
        }

        [Test]
        public void Rename_LeavesAFileItCannotReadAlone()
        {
            var saves = NewSetup().Saves;
            store.Write("deadbeef", "this is not a save");

            Assert.That(saves.Rename("deadbeef", "Mended"), Is.False);

            store.TryRead("deadbeef", out var text);
            Assert.That(text, Is.EqualTo("this is not a save"));
        }

        [Test]
        public void ARenameOfTheActiveHero_IsWhatItsNextSaveWrites()
        {
            var setup = NewSetup();
            var hero = setup.Saves.Create("Before");
            _ = setup.Saves.Load(hero.Id);

            _ = setup.Saves.Rename(hero.Id, "After");
            Assert.That(setup.Saves.Save(), Is.True);

            Assert.That(setup.Saves.List().Single().Name, Is.EqualTo("After"));
        }

        // ── last selected ───────────────────────────────────────────────────

        [Test]
        public void TheLastSelectedHero_LivesInTheAccountFile_AndSurvivesANewService()
        {
            var first = NewSetup().Saves;
            var hero = first.Create("Chosen");
            Assert.That(first.LastSelectedHeroId, Is.Null);

            first.SetLastSelected(hero.Id);

            Assert.That(NewSetup().Saves.LastSelectedHeroId, Is.EqualTo(hero.Id));
            Assert.That(store.Keys(), Is.EquivalentTo(new[] { hero.Id, "account" }));
        }

        [Test]
        public void SetLastSelected_RefusesAHeroThatDoesNotExist_AndAcceptsNone()
        {
            var saves = NewSetup().Saves;
            var hero = saves.Create("Chosen");
            saves.SetLastSelected(hero.Id);

            Assert.Throws<ArgumentException>(() => saves.SetLastSelected("nobody"));
            Assert.That(saves.LastSelectedHeroId, Is.EqualTo(hero.Id));

            saves.SetLastSelected(null);
            Assert.That(saves.LastSelectedHeroId, Is.Null);
        }

        // ── load and save ───────────────────────────────────────────────────

        [Test]
        public void LoadingASavedHero_GoesThroughTheSession_AndRestoresWhatAPlayerCanSee()
        {
            var playing = NewSetup();
            var hero = playing.Saves.Create("Aria");
            _ = playing.Saves.Load(hero.Id);

            var potions = new Package(playing.Game.Hero.Inventory, new ItemInstance(
                playing.Game.Items.Catalog.OfCategory(ItemCategory.Consumable).First().Id, ItemRarity.Common, 1, null), 3u);
            Assert.That(playing.Game.Hero.Inventory.TryAddToContainer(ref potions), Is.True);
            playing.Game.Hero.Wallet.Deposit(new Currency(777u));
            playing.Game.Hero.GainExperience(1500f, playing.Game.Hero.Level);
            playing.Game.Hero.SelectedLocation = thornwood;
            Tick();
            Assert.That(playing.Saves.Save(), Is.True);

            var next = NewSetup();
            var result = next.Saves.Load(hero.Id);

            Assert.That(result.Entered, Is.True);
            Assert.That(result.Status, Is.EqualTo(LoadStatus.Loaded));
            Assert.That(result.Report.IsClean, Is.True);
            Assert.That(next.Game.Hero.Wallet.Balance.Total, Is.EqualTo(777u));
            Assert.That(next.Game.Hero.Level, Is.EqualTo(playing.Game.Hero.Level));
            Assert.That(next.Game.Hero.Level, Is.GreaterThan(1u));
            Assert.That(next.Game.Hero.Inventory.StoredPackages.Values.Any(p => p.Amount == 3u), Is.True);
            Assert.That(next.Game.Hero.SelectedLocation, Is.SameAs(thornwood));
            Assert.That(next.Saves.ActiveHeroId, Is.EqualTo(hero.Id));
        }

        [Test]
        public void ALoad_MakesTheHeroTheLastSelected()
        {
            var setup = NewSetup();
            var hero = setup.Saves.Create("Aria");

            _ = setup.Saves.Load(hero.Id);

            Assert.That(setup.Saves.LastSelectedHeroId, Is.EqualTo(hero.Id));
        }

        [Test]
        public void LoadingAHeroThatIsNotThere_ReportsMissing_AndChangesNothing()
        {
            var setup = NewSetup();
            var hero = setup.Game.Hero;

            var result = setup.Saves.Load("nobody");

            Assert.That(result.Status, Is.EqualTo(LoadStatus.Missing));
            Assert.That(result.Entered, Is.False);
            Assert.That(setup.Game.Hero, Is.SameAs(hero));
            Assert.That(setup.Saves.ActiveHeroId, Is.Null);
        }

        [Test]
        public void ALoad_DuringARunInTheField_IsRefused_AndTheHeroIsNotMadeActive()
        {
            var setup = NewSetup();
            var hero = setup.Saves.Create("Aria");
            setup.Game.Simulation.Send(thornwood);

            var result = setup.Saves.Load(hero.Id);

            Assert.That(result.Entered, Is.False);
            Assert.That(result.Status, Is.EqualTo(LoadStatus.Loaded), "the file reads; the Session said no");
            Assert.That(setup.Saves.ActiveHeroId, Is.Null);
            Assert.That(setup.Saves.Save(), Is.False);
        }

        [Test]
        public void Save_WritesOverTheActiveHeroesOwnFile_AndNoOther()
        {
            var setup = NewSetup();
            var active = setup.Saves.Create("Active");
            var other = setup.Saves.Create("Other");
            _ = setup.Saves.Load(active.Id);
            setup.Game.Hero.Wallet.Deposit(new Currency(50u));
            Tick();

            Assert.That(setup.Saves.Save(), Is.True);

            var reloaded = NewSetup();
            _ = reloaded.Saves.Load(active.Id);
            Assert.That(reloaded.Game.Hero.Wallet.Balance.Total, Is.EqualTo(50u));
            _ = reloaded.Saves.Load(other.Id);
            Assert.That(reloaded.Game.Hero.Wallet.Balance.Total, Is.Zero);
        }

        [Test]
        public void Save_DoesNothing_ForAHeroThatDidNotComeFromASave()
        {
            var setup = NewSetup();
            var saved = setup.Saves.Create("Saved");
            _ = setup.Saves.Load(saved.Id);
            var files = store.Keys().ToArray();

            // Another hero takes the Session behind the save service's back.
            Assert.That(setup.Game.Session.TryLoad(config.DefaultHero), Is.True);

            Assert.That(setup.Saves.Save(), Is.False, "the template hero must not overwrite the saved one");
            Assert.That(store.Keys(), Is.EqualTo(files));
        }

        [Test]
        public void Save_DoesNothing_BeforeAnyHeroIsLoaded()
        {
            var setup = NewSetup();

            Assert.That(setup.Saves.Save(), Is.False);
            Assert.That(store.Keys(), Is.Empty);
        }

        // ── the first launch ────────────────────────────────────────────────

        [Test]
        public void AFirstLaunch_CreatesOneHeroNamedHero_AndWritesItAtOnce()
        {
            var setup = NewSetup();

            var result = setup.Saves.LoadLastOrCreate();

            Assert.That(result.Entered, Is.True);
            var hero = setup.Saves.List().Single();
            Assert.That(hero.Name, Is.EqualTo("Hero"));
            Assert.That(hero.Level, Is.EqualTo(1u));
            Assert.That(setup.Saves.ActiveHeroId, Is.EqualTo(hero.Id));
            Assert.That(setup.Saves.LastSelectedHeroId, Is.EqualTo(hero.Id));
        }

        [Test]
        public void ASecondLaunch_ContinuesTheLastHero_AndCreatesNoNewOne()
        {
            var first = NewSetup();
            _ = first.Saves.LoadLastOrCreate();
            var id = first.Saves.ActiveHeroId;

            var second = NewSetup();
            var result = second.Saves.LoadLastOrCreate();

            Assert.That(result.Entered, Is.True);
            Assert.That(second.Saves.ActiveHeroId, Is.EqualTo(id));
            Assert.That(second.Saves.List().Count, Is.EqualTo(1));
        }

        [Test]
        public void ALaunchWithHeroesButNoAccountFile_ContinuesTheNewestSave()
        {
            var seeding = NewSetup().Saves;
            _ = seeding.Create("Older");
            Tick();
            var newest = seeding.Create("Newest");

            var setup = NewSetup();
            _ = setup.Saves.LoadLastOrCreate();

            Assert.That(setup.Saves.ActiveHeroId, Is.EqualTo(newest.Id));
            Assert.That(setup.Saves.List().Count, Is.EqualTo(2));
        }

        [Test]
        public void ALaunchWhileARunIsInTheField_CreatesNothing()
        {
            var setup = NewSetup();
            setup.Game.Simulation.Send(thornwood);

            var result = setup.Saves.LoadLastOrCreate();

            // A first launch with no file creates and loads a hero; mid-Run the load is refused,
            // but the hero it created is on disk.
            Assert.That(result.Entered, Is.False);
            Assert.That(setup.Saves.List().Count, Is.EqualTo(1));
        }

        // ── the boot ────────────────────────────────────────────────────────

        [Test]
        public void TheBoot_RegistersTheSaveService_OverTheStoreItWasGiven()
        {
            var saves = new InMemorySaveStore();

            var registry = GameBoot.Build(config, saves);
            var service = registry.Get<IHeroSaveService>();
            _ = service.Create("Booted");

            Assert.That(saves.Keys().Count, Is.EqualTo(1));
        }

        [Test]
        public void ABootWithNoStore_UsesAnInMemoryOne_SoATestNeverTouchesDisk()
        {
            var first = GameBoot.Build(config).Get<IHeroSaveService>();
            _ = first.Create("Ephemeral");

            var second = GameBoot.Build(config).Get<IHeroSaveService>();

            Assert.That(second.List(), Is.Empty);
        }
    }
}
