using NUnit.Framework;
using Submodules.Utility.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// What the save service does when a file is damaged, from a newer build, partly unplaceable, or
    /// cannot be written. A bad file is set aside or left alone, never deleted and never loaded as a
    /// hero; a failed write never takes the game down. EditMode fails a test on an unexpected error
    /// log, so each test names the log it expects.
    /// </summary>
    [TestFixture]
    public sealed class HeroSaveFailureTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private InMemorySaveStore inner;
        private FaultyStore store;
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);
            inner = new InMemorySaveStore();
            store = new FaultyStore(inner);
            now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        // A store that fails its writes on demand, over a real in-memory one.
        private sealed class FaultyStore : ISaveStore
        {
            private readonly ISaveStore real;

            public FaultyStore(ISaveStore real) => this.real = real;

            public bool FailWrites { get; set; }

            /// <summary>Fails the writes of this one key only.</summary>
            public string FailKey { get; set; }

            public bool FailSideFiles { get; set; }

            public bool TryRead(string key, out string content) => real.TryRead(key, out content);
            public bool TryReadBackup(string key, out string content) => real.TryReadBackup(key, out content);
            public bool Delete(string key) => real.Delete(key);
            public IReadOnlyCollection<string> Keys() => real.Keys();
            public bool SetAside(string key) => real.SetAside(key);
            public void AppendSideFile(string fileName, string text)
            {
                if (FailSideFiles)
                    throw new IOException("the disk is full");

                real.AppendSideFile(fileName, text);
            }

            public void Write(string key, string content)
            {
                if (FailWrites || key == FailKey)
                    throw new IOException("the disk is full");

                real.Write(key, content);
            }
        }

        private sealed class Setup
        {
            public TestGame Game;
            public HeroSaveService Saves;
        }

        private Setup NewSetup()
        {
            var game = TestGame.Create(config);

            return new Setup { Game = game, Saves = game.SavesOver(config, store, () => now) };
        }

        private SaveSlot<HeroDto> HeroSlot(string id, int version = 1) =>
            new(store, new JsonUtilitySerializer(), id, version);

        private static void ExpectLog(LogType type, string pattern) =>
            LogAssert.Expect(type, new Regex(pattern));

        // ── a damaged primary ───────────────────────────────────────────────

        [Test]
        public void ADamagedPrimaryWithAGoodBackup_LoadsTheBackup_AndSaysSo()
        {
            var setup = NewSetup();
            var hero = setup.Saves.Create("Aria");
            store.Write(hero.Id, "{ truncated");
            ExpectLog(LogType.Warning, "was damaged; loaded its backup");

            var result = setup.Saves.Load(hero.Id);

            Assert.That(result.Entered, Is.True);
            Assert.That(result.Status, Is.EqualTo(LoadStatus.RestoredFromBackup));
            Assert.That(setup.Saves.ActiveHeroId, Is.EqualTo(hero.Id));
        }

        [Test]
        public void TheNextSave_ReplacesADamagedPrimaryThatWasRestoredFromItsBackup()
        {
            var setup = NewSetup();
            var hero = setup.Saves.Create("Aria");
            store.Write(hero.Id, "{ truncated");
            ExpectLog(LogType.Warning, "was damaged; loaded its backup");
            _ = setup.Saves.Load(hero.Id);

            Assert.That(setup.Saves.Save(), Is.True);

            Assert.That(NewSetup().Saves.List().Single().Status, Is.EqualTo(LoadStatus.Loaded));
        }

        // ── a corrupt file ──────────────────────────────────────────────────

        [Test]
        public void ACorruptFile_IsSetAsideNotDeleted_AndNeverLoadedAsAHero()
        {
            var setup = NewSetup();
            store.Write("deadbeef", "this is not a save");
            ExpectLog(LogType.Error, "deadbeef.*damaged");

            var result = setup.Saves.Load("deadbeef");

            Assert.That(result.Entered, Is.False);
            Assert.That(result.Status, Is.EqualTo(LoadStatus.Corrupt));
            Assert.That(setup.Saves.ActiveHeroId, Is.Null);
            Assert.That(inner.Keys(), Is.Empty, "the damaged file is out of the way");
            Assert.That(inner.SetAsideValues, Is.EqualTo(new[] { "this is not a save" }), "and still readable by a person");
        }

        [Test]
        public void ACorruptLastSelectedHero_IsSetAside_AndALaunchStartsAFreshHeroUnderANewId()
        {
            store.Write("deadbeef", "this is not a save");
            var setup = NewSetup();
            setup.Saves.SetLastSelected("deadbeef");
            ExpectLog(LogType.Error, "deadbeef.*damaged");

            var result = setup.Saves.LoadLastOrCreate();

            Assert.That(result.Entered, Is.True);
            Assert.That(setup.Saves.ActiveHeroId, Is.Not.EqualTo("deadbeef"));
            Assert.That(setup.Saves.LastSelectedHeroId, Is.EqualTo(setup.Saves.ActiveHeroId));
            Assert.That(inner.SetAsideValues, Is.EqualTo(new[] { "this is not a save" }));
            Assert.That(setup.Saves.List().Select(hero => hero.Id), Is.EqualTo(new[] { setup.Saves.ActiveHeroId }));
        }

        // ── a newer version ─────────────────────────────────────────────────

        [Test]
        public void AFileFromANewerBuild_IsNotLoaded_AndNotOverwritten()
        {
            var setup = NewSetup();
            var hero = setup.Saves.Create("Aria");
            HeroSlot(hero.Id, version: 2).Save(new HeroDto { id = hero.Id, name = "FromTheFuture" });
            inner.TryRead(hero.Id, out var before);
            ExpectLog(LogType.Warning, "newer version");

            var result = setup.Saves.Load(hero.Id);

            Assert.That(result.Entered, Is.False);
            Assert.That(result.Status, Is.EqualTo(LoadStatus.NewerVersion));
            Assert.That(setup.Saves.ActiveHeroId, Is.Null);
            Assert.That(setup.Saves.Save(), Is.False, "no active hero, so nothing can overwrite it");
            inner.TryRead(hero.Id, out var after);
            Assert.That(after, Is.EqualTo(before));
            Assert.That(inner.SetAsideValues, Is.Empty);
        }

        [Test]
        public void ALaunchWhoseLastHeroIsFromANewerBuild_StartsAFreshHeroUnderADifferentId_AndLeavesTheOldFile()
        {
            var seeding = NewSetup();
            var future = seeding.Saves.Create("FromTheFuture");
            HeroSlot(future.Id, version: 2).Save(new HeroDto { id = future.Id, name = "FromTheFuture" });
            seeding.Saves.SetLastSelected(future.Id);
            inner.TryRead(future.Id, out var before);
            var setup = NewSetup();
            ExpectLog(LogType.Warning, "newer version");

            var result = setup.Saves.LoadLastOrCreate();

            Assert.That(result.Entered, Is.True);
            Assert.That(setup.Saves.ActiveHeroId, Is.Not.EqualTo(future.Id));
            inner.TryRead(future.Id, out var after);
            Assert.That(after, Is.EqualTo(before));
        }

        // ── a failed write ──────────────────────────────────────────────────

        [Test]
        public void AFailingWrite_ReturnsFalse_LeavesThePreviousSaveIntact_AndTheNextSaveSucceeds()
        {
            var setup = NewSetup();
            var hero = setup.Saves.Create("Aria");
            _ = setup.Saves.Load(hero.Id);
            setup.Game.Hero.Wallet.Deposit(new Currency(100u));
            Assert.That(setup.Saves.Save(), Is.True);

            setup.Game.Hero.Wallet.Deposit(new Currency(900u));
            store.FailWrites = true;
            ExpectLog(LogType.Error, "Could not save hero .*the disk is full");

            Assert.That(setup.Saves.Save(), Is.False);

            store.FailWrites = false;
            var survivor = NewSetup();
            _ = survivor.Saves.Load(hero.Id);
            Assert.That(survivor.Game.Hero.Wallet.Balance.Total, Is.EqualTo(100u), "the previous save is untouched");

            Assert.That(setup.Saves.Save(), Is.True, "the next save point is the retry");

            var retried = NewSetup();
            _ = retried.Saves.Load(hero.Id);
            Assert.That(retried.Game.Hero.Wallet.Balance.Total, Is.EqualTo(1000u));
        }

        // ── the quarantine sidecar ──────────────────────────────────────────

        private string SaveWithAnItemWhoseDefinitionIsGone(Setup setup)
        {
            var hero = setup.Saves.Create("Aria");
            var slot = HeroSlot(hero.Id);
            var saved = slot.Load().Payload;
            saved.stash.packages = saved.stash.packages.Append(new PackageDto
            {
                x = 7,
                y = 5,
                instance = new ItemInstance("deleted-in-a-patch", ItemRarity.Common, 1, null).ToDto(),
                amount = 1u,
            }).ToArray();
            slot.Save(saved);

            return hero.Id;
        }

        [Test]
        public void AnItemThatCouldNotBeRestored_IsWrittenToTheSidecar_WithItsContainerReasonAndItem()
        {
            var setup = NewSetup();
            var id = SaveWithAnItemWhoseDefinitionIsGone(setup);
            ExpectLog(LogType.Warning, "could not be restored");

            var result = setup.Saves.Load(id);

            Assert.That(result.Entered, Is.True);
            var lines = inner.SideFiles[$"{id}.quarantine.json"].Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines.Length, Is.EqualTo(1));
            var entry = JsonUtility.FromJson<QuarantineEntryDto>(lines[0]);
            Assert.That(entry.container, Is.EqualTo(SavedContainer.Stash.ToString()));
            Assert.That(entry.reason, Is.EqualTo(SkipReason.UnknownDefinition.ToString()));
            Assert.That(entry.instance.definitionId, Is.EqualTo("deleted-in-a-patch"));
            Assert.That(entry.x, Is.EqualTo(7));
            Assert.That(entry.y, Is.EqualTo(5));
            Assert.That(entry.quarantinedAtUtc, Is.EqualTo(now.ToString("o")));
        }

        [Test]
        public void TheSidecar_IsNotAHeroFile_AndALaterSaveNeverRewritesIt()
        {
            var setup = NewSetup();
            var id = SaveWithAnItemWhoseDefinitionIsGone(setup);
            ExpectLog(LogType.Warning, "could not be restored");
            _ = setup.Saves.Load(id);
            var sidecar = inner.SideFiles[$"{id}.quarantine.json"];

            Assert.That(setup.Saves.Save(), Is.True);

            Assert.That(inner.SideFiles[$"{id}.quarantine.json"], Is.EqualTo(sidecar));
            Assert.That(inner.Keys(), Is.EquivalentTo(new[] { id, "account" }));
            Assert.That(setup.Saves.List().Single().Id, Is.EqualTo(id));

            // The item is out of the hero for good: the saved hero no longer carries it.
            Assert.That(HeroSlot(id).Load().Payload.stash.packages.Any(p => p.instance.definitionId == "deleted-in-a-patch"), Is.False);
        }

        [Test]
        public void ACleanLoad_WritesNoSidecar()
        {
            var setup = NewSetup();
            var hero = setup.Saves.Create("Aria");

            _ = setup.Saves.Load(hero.Id);

            Assert.That(inner.SideFiles, Is.Empty);
        }

        [Test]
        public void AnAccountWriteThatFails_DoesNotUndoALoad_AndOnlyWarns()
        {
            var setup = NewSetup();
            var hero = setup.Saves.Create("Aria");
            store.FailKey = "account";
            ExpectLog(LogType.Warning, "Could not remember the last-selected hero");

            var result = setup.Saves.Load(hero.Id);

            Assert.That(result.Entered, Is.True);
            Assert.That(setup.Saves.ActiveHeroId, Is.EqualTo(hero.Id));
        }

        [Test]
        public void ALoadThatQuarantinedSomething_WritesTheHeroAtOnce_SoTheNextLoadAppendsNothingMore()
        {
            var setup = NewSetup();
            var id = SaveWithAnItemWhoseDefinitionIsGone(setup);
            ExpectLog(LogType.Warning, "could not be restored");
            _ = setup.Saves.Load(id);
            var sidecar = inner.SideFiles[$"{id}.quarantine.json"];

            Assert.That(HeroSlot(id).Load().Payload.stash.packages.Any(p => p.instance.definitionId == "deleted-in-a-patch"), Is.False,
                "the item is no longer in the hero file");
            _ = NewSetup().Saves.Load(id);

            Assert.That(inner.SideFiles[$"{id}.quarantine.json"], Is.EqualTo(sidecar));
        }

        [Test]
        public void ASidecarThatCannotBeWritten_KeepsTheItemsInTheHeroFile()
        {
            var setup = NewSetup();
            var id = SaveWithAnItemWhoseDefinitionIsGone(setup);
            store.FailSideFiles = true;
            ExpectLog(LogType.Error, "could not be written to");

            var result = setup.Saves.Load(id);

            Assert.That(result.Entered, Is.True);
            Assert.That(HeroSlot(id).Load().Payload.stash.packages.Any(p => p.instance.definitionId == "deleted-in-a-patch"), Is.True,
                "nothing was lost: the file still has it");
        }

        [Test]
        public void ANormaliserThatThrows_SkipsThisSave_KeepsThePreviousFile_AndTheNextSaveRetries()
        {
            var setup = NewSetup();
            var hero = setup.Saves.Create("Aria");
            _ = setup.Saves.Load(hero.Id);
            var fail = true;
            setup.Saves.AddBeforeSave(() =>
            {
                if (fail)
                    throw new System.InvalidOperationException("nowhere to put it");
            });
            setup.Game.Hero.Wallet.Deposit(new Currency(5u));
            ExpectLog(LogType.Error, "this save was skipped.*nowhere to put it");

            Assert.That(setup.Saves.Save(), Is.False);

            var survivor = NewSetup();
            _ = survivor.Saves.Load(hero.Id);
            Assert.That(survivor.Game.Hero.Wallet.Balance.Total, Is.Zero, "the previous file is kept");

            fail = false;
            Assert.That(setup.Saves.Save(), Is.True);
        }
    }
}
