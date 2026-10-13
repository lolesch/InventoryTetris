using NUnit.Framework;
using Submodules.Utility.Persistence;
using System.Collections.Generic;
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
    /// What the game scene tells the player on entry after a load that went wrong (issue #256): the notice
    /// picked from the load status and the restore report, and the service holding it until it is taken once.
    /// </summary>
    [TestFixture]
    public sealed class EntryNoticeTests
    {
        private static SkippedPackage Skipped(SavedContainer container, string definitionId, uint amount = 1u) =>
            new(container,
                new PackageDto
                {
                    amount = amount,
                    instance = new ItemInstance(definitionId, ItemRarity.Common, 1, null).ToDto(),
                },
                SkipReason.UnknownDefinition);

        private static RestoreReport Report(params SkippedPackage[] skipped) => new(skipped);

        // ── the pick ────────────────────────────────────────────────────────

        [Test]
        public void ACleanLoad_HasNothingToSay()
        {
            Assert.That(EntryNotice.From(LoadStatus.Loaded, Report()), Is.Null);
        }

        [TestCase(LoadStatus.Missing)]
        [TestCase(LoadStatus.Corrupt)]
        [TestCase(LoadStatus.NewerVersion)]
        public void AFileThatDidNotLoad_HasNoReport_AndNothingToSay(LoadStatus status)
        {
            Assert.That(EntryNotice.From(status, null), Is.Null);
        }

        [Test]
        public void ABackupRestore_SaysTheLatestSaveWasDamaged()
        {
            var notice = EntryNotice.From(LoadStatus.RestoredFromBackup, Report());

            Assert.That(notice.RestoredFromBackup, Is.True);
            Assert.That(EntryNotice.BackupText, Is.EqualTo("Your latest save was damaged. Loaded the previous one, so recent progress may be missing."));
            Assert.That(notice.UnplacedCount, Is.Zero);
        }

        [Test]
        public void APartialLoad_CountsTheItems_AndNamesThemBehindShow()
        {
            var notice = EntryNotice.From(LoadStatus.Loaded, Report(
                Skipped(SavedContainer.Stash, "deleted-in-a-patch"),
                Skipped(SavedContainer.Inventory, "old-potion", 3u),
                Skipped(SavedContainer.Corpse, "lost-ring")));

            Assert.That(notice.RestoredFromBackup, Is.False);
            Assert.That(notice.UnplacedCount, Is.EqualTo(3));
            Assert.That(notice.UnplacedText, Is.EqualTo("3 items couldn't be restored"));
            Assert.That(notice.UnplacedNames, Is.EqualTo(new[]
            {
                "deleted-in-a-patch (Stash)",
                "old-potion x3 (Inventory)",
                "lost-ring (Corpse)",
            }));
        }

        [Test]
        public void TheNames_ReadOnePerLine()
        {
            var notice = EntryNotice.From(LoadStatus.Loaded, Report(
                Skipped(SavedContainer.Stash, "a"), Skipped(SavedContainer.Stash, "b")));

            Assert.That(notice.NamesText, Is.EqualTo("a (Stash)\nb (Stash)"));
        }

        [Test]
        public void ALongList_ShowsTheFirstEight_AndCountsTheRest()
        {
            var many = Enumerable.Range(0, 15).Select(index => Skipped(SavedContainer.Stash, "item" + index)).ToArray();

            var lines = EntryNotice.From(LoadStatus.Loaded, Report(many)).NamesText.Split('\n');

            Assert.That(lines.Length, Is.EqualTo(9));
            Assert.That(lines[7], Is.EqualTo("item7 (Stash)"));
            Assert.That(lines[8], Is.EqualTo("and 7 more"));
        }

        [Test]
        public void OneItem_IsNotPlural()
        {
            var notice = EntryNotice.From(LoadStatus.Loaded, Report(Skipped(SavedContainer.Stash, "gone")));

            Assert.That(notice.UnplacedText, Is.EqualTo("1 item couldn't be restored"));
        }

        [Test]
        public void ABackupRestoreThatAlsoLeftItemsOut_SaysBoth()
        {
            var notice = EntryNotice.From(LoadStatus.RestoredFromBackup, Report(Skipped(SavedContainer.Stash, "gone")));

            Assert.That(notice.RestoredFromBackup, Is.True);
            Assert.That(notice.UnplacedCount, Is.EqualTo(1));
        }

        // ── held by the service until taken ─────────────────────────────────

        [TestFixture]
        public sealed class Held
        {
            private readonly List<UnityEngine.Object> created = new();
            private GameConfig config;
            private InMemorySaveStore store;

            [SetUp]
            public void SetUp()
            {
                config = TestGameConfig.Create(created);
                store = new InMemorySaveStore();
            }

            [TearDown]
            public void TearDown()
            {
                foreach (var asset in created)
                    Object.DestroyImmediate(asset);

                created.Clear();
            }

            private HeroSaveService NewSaves() => TestGame.Create(config).SavesOver(config, store);

            private string KeyOf(string id) => store.Keys().Single(key => HeroFileKey.IdOf(key) == id);

            private SaveSlot<HeroDto> HeroSlot(string id) => new(store, new JsonUtilitySerializer(), KeyOf(id), 1);

            // A hero whose file holds a stash item no catalog has.
            private string HeroWithAnItemWhoseDefinitionIsGone(IHeroSaveService saves)
            {
                var hero = saves.Create("Aria");
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

            private string HeroWithADamagedFile(IHeroSaveService saves)
            {
                var hero = saves.Create("Aria");
                store.Write(KeyOf(hero.Id), "{ truncated");
                return hero.Id;
            }

            [Test]
            public void ABackupRestore_IsTakenOnce_ThenGone()
            {
                var saves = NewSaves();
                _ = saves.Load(HeroWithADamagedFile(saves));
                LogAssert.Expect(LogType.Warning, new Regex("was damaged; loaded its backup"));

                Assert.That(saves.TakeEntryNotice().RestoredFromBackup, Is.True);
                Assert.That(saves.TakeEntryNotice(), Is.Null, "the notice shows once");
            }

            [Test]
            public void APartialLoad_IsHeldWithItsNames()
            {
                var saves = NewSaves();
                LogAssert.Expect(LogType.Warning, new Regex("could not be restored"));
                _ = saves.Load(HeroWithAnItemWhoseDefinitionIsGone(saves));

                var notice = saves.TakeEntryNotice();

                Assert.That(notice.UnplacedText, Is.EqualTo("1 item couldn't be restored"));
                Assert.That(notice.UnplacedNames, Is.EqualTo(new[] { "deleted-in-a-patch (Stash)" }));
            }

            [Test]
            public void ACleanLoad_HoldsNothing()
            {
                var saves = NewSaves();

                _ = saves.Load(saves.Create("Aria").Id);

                Assert.That(saves.TakeEntryNotice(), Is.Null);
            }

            [Test]
            public void NothingLoaded_HoldsNothing()
            {
                Assert.That(NewSaves().TakeEntryNotice(), Is.Null);
            }

            [Test]
            public void ACleanReloadOfTheSameHero_KeepsTheNoticeTheFirstLoadMade()
            {
                // Selecting the hero on the menu loads it and writes the quarantined items out of the file; Play
                // then loads it again, clean. The player must still be told on entering.
                var saves = NewSaves();
                LogAssert.Expect(LogType.Warning, new Regex("could not be restored"));
                var id = HeroWithAnItemWhoseDefinitionIsGone(saves);
                _ = saves.Load(id);

                var reload = saves.Load(id);

                Assert.That(reload.Report.IsClean, Is.True, "premise: the second load is clean");
                Assert.That(saves.TakeEntryNotice().UnplacedCount, Is.EqualTo(1));
            }

            [Test]
            public void ANoticeIsHeldPerHero_AndOnlyTheEnteredHerosIsTaken()
            {
                var saves = NewSaves();
                LogAssert.Expect(LogType.Warning, new Regex("could not be restored"));
                var damaged = HeroWithAnItemWhoseDefinitionIsGone(saves);
                _ = saves.Load(damaged);
                var other = saves.Create("Bran");

                _ = saves.Load(other.Id);

                Assert.That(saves.TakeEntryNotice(), Is.Null, "Bran loaded clean");

                _ = saves.Load(damaged);

                Assert.That(saves.TakeEntryNotice().UnplacedCount, Is.EqualTo(1), "Aria's is still waiting for Aria");
            }

            [Test]
            public void DeletingTheHero_DropsItsNotice()
            {
                var saves = NewSaves();
                LogAssert.Expect(LogType.Warning, new Regex("could not be restored"));
                var id = HeroWithAnItemWhoseDefinitionIsGone(saves);
                _ = saves.Load(id);
                var other = saves.Create("Bran");

                _ = saves.Delete(id);
                _ = saves.Load(other.Id);

                Assert.That(saves.TakeEntryNotice(), Is.Null);
            }
        }
    }
}
