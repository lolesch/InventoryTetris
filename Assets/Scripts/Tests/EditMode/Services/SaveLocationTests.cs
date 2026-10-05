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
            new(game.Session, game.Items, config, game.Simulation, store, new JsonUtilitySerializer());

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
            Assert.That(onDisk.Keys(), Is.EqualTo(new[] { existing.Id }), "the saves were not touched");
        }
    }
}
