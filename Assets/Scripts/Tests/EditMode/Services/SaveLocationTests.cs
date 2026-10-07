using Submodules.Utility.Services;
using NUnit.Framework;
using Submodules.Utility.Persistence;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>The Editor tools' logic (#171): the wipe, and what a Play entry boots over. The menu items
    /// themselves are checked by hand.</summary>
    [TestFixture]
    public sealed class SaveLocationTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;

        [SetUp]
        public void SetUp() => config = TestGameConfig.Create(created);

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private static HeroSaveService ServiceOver(TestGame game, GameConfig config, ISaveStore store) =>
            game.SavesOver(config, store);

        [Test]
        public void WipeAll_DeletesEveryHeroSaveAndTheAccountFile_ThenAPlayEntryCreatesAFreshHero()
        {
            var store = new InMemorySaveStore();
            var seeding = ServiceOver(TestGame.Create(config), config, store);
            var old = seeding.Create("Old");
            _ = seeding.Create("Other");
            seeding.SetLastSelected(old.Id);
            Assert.That(store.Keys().Count, Is.EqualTo(3), "two heroes and the Account file");

            var deleted = SaveLocation.WipeAll(store);

            Assert.That(deleted, Is.EqualTo(3));
            Assert.That(store.Keys(), Is.Empty);
            var game = TestGame.Create(config);
            var playing = ServiceOver(game, config, store);
            _ = playing.LoadLastOrCreate();
            Assert.That(playing.List().Single().Id, Is.Not.EqualTo(old.Id));
        }

        [Test]
        public void WipeAll_KeepsAQuarantineSidecar_ItIsEvidenceNotASave()
        {
            var store = new InMemorySaveStore();
            _ = ServiceOver(TestGame.Create(config), config, store).Create("Old");
            store.AppendSideFile("someid.quarantine.json", "{}\n");

            _ = SaveLocation.WipeAll(store);

            Assert.That(store.SideFiles.Keys, Is.EqualTo(new[] { "someid.quarantine.json" }));
        }

        [Test]
        public void StartFresh_BootsOverAnEmptyInMemoryStore_AndNeverTheSavesFolder()
        {
            Assert.That(GameBoot.SaveStoreForPlay(startFresh: true), Is.InstanceOf<InMemorySaveStore>());
            Assert.That(GameBoot.SaveStoreForPlay(startFresh: false), Is.InstanceOf<FileSaveStore>());
        }

        [Test]
        public void AStartFreshPlay_BuildsANewHero_AndReadsNoFileEvenWhenSavesExist()
        {
            // The saves a normal Play would continue.
            var onDisk = new InMemorySaveStore();
            var existing = ServiceOver(TestGame.Create(config), config, onDisk).Create("Veteran");

            var fresh = GameBoot.SaveStoreForPlay(startFresh: true);
            var game = TestGame.Create(config);
            var playing = ServiceOver(game, config, fresh);

            var result = playing.LoadLastOrCreate();

            Assert.That(result.Entered, Is.True);
            Assert.That(playing.ActiveHeroId, Is.Not.EqualTo(existing.Id));
            Assert.That(onDisk.Keys(), Is.EqualTo(new[] { HeroFileKey.Compose("Veteran", existing.Id) }), "the saves were not touched");
        }

        [Test]
        public void TheStartFreshToggle_IsReadFromEditorPrefs_AndIsOffByDefault()
        {
            var had = UnityEditor.EditorPrefs.HasKey(GameBoot.StartFreshKey);
            var was = UnityEditor.EditorPrefs.GetBool(GameBoot.StartFreshKey, false);

            try
            {
                UnityEditor.EditorPrefs.DeleteKey(GameBoot.StartFreshKey);
                Assert.That(GameBoot.StartFreshEachPlay, Is.False);

                UnityEditor.EditorPrefs.SetBool(GameBoot.StartFreshKey, true);
                Assert.That(GameBoot.StartFreshEachPlay, Is.True);
            }
            finally
            {
                if (had)
                    UnityEditor.EditorPrefs.SetBool(GameBoot.StartFreshKey, was);
                else
                    UnityEditor.EditorPrefs.DeleteKey(GameBoot.StartFreshKey);
            }
        }

        private static void Unboot()
        {
            GameExit.Reset();
            ServiceLocator.Reset();
            GameLoop.Uninstall();
        }

        [Test]
        public void APlayEntry_ContinuesTheLastHero_InstallsTheQuitSave_AndCreatesNoSecondHero()
        {
            var store = new InMemorySaveStore();
            var seeding = ServiceOver(TestGame.Create(config), config, store);
            var veteran = seeding.Create("Veteran");
            seeding.SetLastSelected(veteran.Id);

            try
            {
                GameBoot.Boot(config, store);

                var saves = ServiceLocator.Get<IHeroSaveService>();
                Assert.That(saves.ActiveHeroId, Is.EqualTo(veteran.Id));
                Assert.That(saves.List().Count, Is.EqualTo(1));
                Assert.That(GameExit.IsInstalled, Is.True);
            }
            finally
            {
                Unboot();
            }
        }

        [Test]
        public void AFirstPlayEntry_CreatesAHero_AndWritesItAtOnce()
        {
            var store = new InMemorySaveStore();

            try
            {
                GameBoot.Boot(config, store);

                Assert.That(ServiceLocator.Get<IHeroSaveService>().ActiveHeroId, Is.Not.Null);
                Assert.That(store.Keys().Count, Is.EqualTo(2), "the hero and the Account file");
            }
            finally
            {
                Unboot();
            }
        }
    }
}
