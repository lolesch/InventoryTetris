using NUnit.Framework;
using Submodules.Utility.Persistence;
using System;
using System.Collections.Generic;
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

            return new Setup { Game = game, Saves = game.SavesOver(config, over, () => now) };
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
            Assert.That(store.Keys(), Is.EqualTo(new[] { HeroFileKey.Compose("Aria", hero.Id) }));
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
        public void Rename_ChangesTheName_AndMovesTheFileToIt_ButNeverTheId()
        {
            var saves = NewSetup().Saves;
            var hero = saves.Create("Before");

            var renamed = saves.Rename(hero.Id, "  After ");

            Assert.That(renamed, Is.True);
            Assert.That(store.Keys(), Is.EqualTo(new[] { HeroFileKey.Compose("After", hero.Id) }), "one file, under the new name");
            Assert.That(saves.List().Single().Name, Is.EqualTo("After"));
            Assert.That(saves.List().Single().Id, Is.EqualTo(hero.Id));
        }

        [Test]
        public void ARenameThatDoesNotChangeTheFileName_WritesInPlace()
        {
            var saves = NewSetup().Saves;
            var hero = saves.Create("Aria");

            Assert.That(saves.Rename(hero.Id, "ARIA"), Is.True);

            Assert.That(store.Keys().Count, Is.EqualTo(1));
            Assert.That(saves.List().Single().Name, Is.EqualTo("ARIA"));
        }

        [Test]
        public void ARenameOfTheLastSelectedHero_KeepsItSelected()
        {
            var saves = NewSetup().Saves;
            var hero = saves.Create("Before");
            saves.SetLastSelected(hero.Id);

            _ = saves.Rename(hero.Id, "After");

            Assert.That(saves.LastSelectedHeroId, Is.EqualTo(hero.Id));
            Assert.That(saves.Load(hero.Id).Entered, Is.True);
        }

        // ── the file name ───────────────────────────────────────────────────

        [Test]
        public void AHeroFile_IsNamedAfterTheHeroAndItsId()
        {
            var saves = NewSetup().Saves;

            var hero = saves.Create("Sir Aria");

            Assert.That(store.Keys().Single(), Is.EqualTo($"Sir-Aria_{hero.Id}"));
        }

        [Test]
        public void ANameThatIsNotAFileName_StillMakesAFileThatLoads()
        {
            var saves = NewSetup().Saves;

            var hero = saves.Create("a/b:c*?\"<>|  ");

            Assert.That(saves.Load(hero.Id).Entered, Is.True);
            Assert.That(saves.List().Single().Name, Is.EqualTo("a/b:c*?\"<>|"), "the name is kept whole; only the file name is cut down");
        }

        [Test]
        public void AHeroFromBeforeNamesWereInTheFileName_LoadsAndIsMovedOnItsNextSave()
        {
            var setup = NewSetup();
            var hero = setup.Saves.Create("Veteran");
            var legacy = hero.Id;
            store.TryRead(HeroFileKey.Compose("Veteran", hero.Id), out var text);
            store.Delete(HeroFileKey.Compose("Veteran", hero.Id));
            store.Write(legacy, text);
            var again = NewSetup();

            Assert.That(again.Saves.List().Single().Name, Is.EqualTo("Veteran"));
            Assert.That(again.Saves.Load(hero.Id).Entered, Is.True);
            Assert.That(again.Saves.Save(), Is.True);

            Assert.That(store.Keys(), Is.EquivalentTo(new[] { HeroFileKey.Compose("Veteran", hero.Id), "account" }), "the bare-id file is gone");
        }

        [Test]
        public void TwoFilesOfOneId_AreOneHero_TheNewerOne()
        {
            var saves = NewSetup().Saves;
            var hero = saves.Create("Before");
            store.TryRead(HeroFileKey.Compose("Before", hero.Id), out var older);
            now = now.AddMinutes(5);
            _ = saves.Rename(hero.Id, "After");
            store.Write(HeroFileKey.Compose("Before", hero.Id), older);

            var listed = saves.List();

            Assert.That(listed.Count, Is.EqualTo(1));
            Assert.That(listed[0].Name, Is.EqualTo("After"));
            Assert.That(saves.Delete(hero.Id), Is.True);
            Assert.That(store.Keys(), Is.Empty, "a delete takes every file of the id");
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
            Assert.That(store.Keys(), Is.EquivalentTo(new[] { HeroFileKey.Compose("Chosen", hero.Id), "account" }));
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

        // ── save points: a Run settles ──────────────────────────────────────

        private static void TickUntilHome(Setup setup)
        {
            for (var i = 0; i < 100 && setup.Game.Simulation.Run.Phase == RunPhase.InField; i++)
                setup.Game.Simulation.Tick(0.5f);

            Assert.That(setup.Game.Simulation.Run.Phase, Is.EqualTo(RunPhase.InTown), "the Run never ended");
        }

        private static void PutPotionsInBag(Setup setup, uint amount)
        {
            var id = setup.Game.Items.Catalog.OfCategory(ItemCategory.Consumable).First().Id;
            var package = new Package(setup.Game.Hero.Inventory, new ItemInstance(id, ItemRarity.Common, 1, null), amount);
            Assert.That(setup.Game.Hero.Inventory.TryAddToContainer(ref package), Is.True);
        }

        private static bool BagHoldsPotions(Hero hero) =>
            hero.Inventory.StoredPackages.Values.Any(p => hero.Inventory.ViewOf(p.Item).Definition.Category == ItemCategory.Consumable);

        private Setup LoadedHero(out string id)
        {
            var setup = NewSetup();
            id = setup.Saves.Create("Aria").Id;
            _ = setup.Saves.Load(id);
            return setup;
        }

        [Test]
        public void ADeath_DrivesASave_WhoseHeroHasItsCorpseAndAnEmptyBag()
        {
            var setup = LoadedHero(out var id);
            PutPotionsInBag(setup, 3u);
            setup.Game.Simulation.Send(thornwood);
            setup.Game.Hero.GetResource(StatName.Health).DepleteCurrent();
            Tick();

            TickUntilHome(setup);

            var next = NewSetup();
            _ = next.Saves.Load(id);
            Assert.That(next.Game.Hero.Corpse.Exists, Is.True, "the bag was buried before the save was written");
            Assert.That(BagHoldsPotions(next.Game.Hero), Is.False);
            Assert.That(next.Saves.List().Single().SavedAtUtc, Is.EqualTo(now), "written by the Death, not earlier");
        }

        [Test]
        public void ARecall_DrivesASave_WithTheBankedLootInTheBag()
        {
            var setup = LoadedHero(out var id);
            setup.Game.Simulation.Send(thornwood);
            PutPotionsInBag(setup, 3u);
            Tick();

            _ = setup.Game.Simulation.Recall();

            var next = NewSetup();
            _ = next.Saves.Load(id);
            Assert.That(BagHoldsPotions(next.Game.Hero), Is.True);
            Assert.That(next.Game.Hero.Corpse.Exists, Is.False);
            Assert.That(next.Saves.List().Single().SavedAtUtc, Is.EqualTo(now));
        }

        [Test]
        public void TheSettledEvent_FiresAfterTheSettlement_InBothCases()
        {
            var setup = LoadedHero(out _);
            var seen = new List<(RunOutcome Outcome, RunPhase Phase, bool Corpse)>();
            setup.Game.Simulation.RunSettled += result =>
                seen.Add((result.Outcome, setup.Game.Simulation.Run.Phase, setup.Game.Hero.Corpse.Exists));

            PutPotionsInBag(setup, 1u);
            setup.Game.Simulation.Send(thornwood);
            _ = setup.Game.Simulation.Recall();
            setup.Game.Simulation.Send(thornwood);
            setup.Game.Hero.GetResource(StatName.Health).DepleteCurrent();
            TickUntilHome(setup);

            Assert.That(seen, Is.EqualTo(new[]
            {
                (RunOutcome.Recalled, RunPhase.InTown, false),
                (RunOutcome.Died, RunPhase.InTown, true),
            }));
        }

        [Test]
        public void NoSaveIsWritten_WhileARunIsStillInTheField()
        {
            var setup = LoadedHero(out var id);
            Tick();
            setup.Game.Simulation.Send(thornwood);
            Tick();

            Assert.That(setup.Saves.Save(), Is.False);

            Assert.That(setup.Saves.List().Single().SavedAtUtc, Is.Not.EqualTo(now));
            Assert.That(setup.Saves.ActiveHeroId, Is.EqualTo(id));
        }

        [Test]
        public void TheSubscription_SurvivesAHeroLoad_AndWritesTheHeroThatIsNowActive()
        {
            var setup = NewSetup();
            var first = setup.Saves.Create("First");
            var second = setup.Saves.Create("Second");
            _ = setup.Saves.Load(first.Id);
            _ = setup.Saves.Load(second.Id);
            Tick();
            setup.Game.Simulation.Send(thornwood);
            setup.Game.Hero.Wallet.Deposit(new Currency(50u));

            _ = setup.Game.Simulation.Recall();

            var list = setup.Saves.List().ToDictionary(hero => hero.Id);
            Assert.That(list[second.Id].SavedAtUtc, Is.EqualTo(now));
            Assert.That(list[first.Id].SavedAtUtc, Is.Not.EqualTo(now));
            var next = NewSetup();
            _ = next.Saves.Load(second.Id);
            Assert.That(next.Game.Hero.Wallet.Balance.Total, Is.EqualTo(50u));
        }

        // ── before-save normalisers ─────────────────────────────────────────

        [Test]
        public void ARegisteredNormaliser_RunsBeforeTheSnapshot()
        {
            var setup = LoadedHero(out var id);
            setup.Saves.AddBeforeSave(() => setup.Game.Hero.Wallet.Deposit(new Currency(7u)));

            Assert.That(setup.Saves.Save(), Is.True);

            var next = NewSetup();
            _ = next.Saves.Load(id);
            Assert.That(next.Game.Hero.Wallet.Balance.Total, Is.EqualTo(7u), "the normaliser's change is in the file");
        }

        // The cursor's job, without the canvas: lift a package out of the bag, then let a normaliser do
        // what the drag provider does - Return to Origin.
        private static Vector2Int LiftPotions(Setup setup, uint amount, out Package held)
        {
            PutPotionsInBag(setup, amount);
            var cell = setup.Game.Hero.Inventory.StoredPackages.Keys.Single();
            held = TestPackages.PickUp(setup.Game.Hero.Inventory, cell);
            Assert.That(held.IsValid, Is.True);
            Assert.That(PotionsInBag(setup.Game.Hero), Is.Zero, "the cursor holds them now");
            return cell;
        }

        private static int PotionsInBag(Hero hero) =>
            hero.Inventory.StoredPackages.Values
                .Where(p => hero.Inventory.ViewOf(p.Item).Definition.Category == ItemCategory.Consumable)
                .Sum(p => (int)p.Amount);

        [Test]
        public void APackageOnTheCursorAtARecall_IsBackAtItsOrigin_AndInTheSave()
        {
            var setup = LoadedHero(out var id);
            var wallet = setup.Game.Hero.Wallet.Balance.Total;
            var cell = LiftPotions(setup, 3u, out var held);
            var inventory = setup.Game.Hero.Inventory;
            setup.Saves.AddBeforeSave(() => held = ReturnToOrigin.Return(held, inventory, cell, inventory));
            setup.Game.Simulation.Send(thornwood);

            _ = setup.Game.Simulation.Recall();

            Assert.That(held.IsValid, Is.False, "the package found a home");
            var next = NewSetup();
            _ = next.Saves.Load(id);
            Assert.That(next.Game.Hero.Inventory.TryGetPackageAt(cell, out var back), Is.True, "at its origin cell");
            Assert.That(back.Amount, Is.EqualTo(3u));
            Assert.That(PotionsInBag(next.Game.Hero), Is.EqualTo(3));
            Assert.That(next.Game.Hero.Wallet.Balance.Total, Is.EqualTo(wallet));
        }

        [Test]
        public void APackageOnTheCursorWhoseOriginIsTaken_LandsElsewhereInTheBag_AndLosesNothing()
        {
            var setup = LoadedHero(out var id);
            var cell = LiftPotions(setup, 3u, out var held);
            var inventory = setup.Game.Hero.Inventory;
            var gear = new Package(inventory, new ItemInstance(
                setup.Game.Items.Catalog.OfCategory(ItemCategory.Equipment).First().Id, ItemRarity.Common, 1, null), 1u);
            Assert.That(inventory.TryAddAtPosition(cell, ref gear), Is.True, "something took the freed cell");
            setup.Saves.AddBeforeSave(() => held = ReturnToOrigin.Return(held, inventory, cell, inventory));

            Assert.That(setup.Saves.Save(), Is.True);

            Assert.That(held.IsValid, Is.False);
            var next = NewSetup();
            _ = next.Saves.Load(id);
            Assert.That(PotionsInBag(next.Game.Hero), Is.EqualTo(3));
            Assert.That(next.Game.Hero.Inventory.StoredPackages.Count, Is.EqualTo(2));
        }

        [Test]
        public void ARemovedNormaliser_NoLongerRuns_AndARegistrationMadeTwiceRunsOnce()
        {
            var setup = LoadedHero(out _);
            var runs = 0;
            void Count() => runs++;
            setup.Saves.AddBeforeSave(Count);
            setup.Saves.AddBeforeSave(Count);

            _ = setup.Saves.Save();
            Assert.That(runs, Is.EqualTo(1));

            setup.Saves.RemoveBeforeSave(Count);
            _ = setup.Saves.Save();
            Assert.That(runs, Is.EqualTo(1));
        }

        [Test]
        public void ANormaliserThatThrows_StillLetsTheOthersRun_ButSkipsTheWrite()
        {
            var setup = LoadedHero(out var id);
            var otherRan = false;
            setup.Saves.AddBeforeSave(() => throw new InvalidOperationException("boom"));
            setup.Saves.AddBeforeSave(() => otherRan = true);
            LogAssert.Expect(LogType.Error, new Regex("before-save step failed.*boom"));

            Assert.That(setup.Saves.Save(), Is.False);

            Assert.That(otherRan, Is.True);
        }

        [Test]
        public void NoNormaliserRuns_WhenNothingWillBeWritten()
        {
            var setup = NewSetup();
            var runs = 0;
            setup.Saves.AddBeforeSave(() => runs++);
            Assert.That(setup.Saves.Save(), Is.False, "no active hero");

            var id = setup.Saves.Create("Aria").Id;
            _ = setup.Saves.Load(id);
            setup.Game.Simulation.Send(thornwood);
            Assert.That(setup.Saves.Save(), Is.False, "a Run in the Field");

            Assert.That(runs, Is.Zero);
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
