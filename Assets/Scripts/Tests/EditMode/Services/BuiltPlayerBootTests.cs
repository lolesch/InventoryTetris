using NUnit.Framework;
using Submodules.Utility.Persistence;
using Submodules.Utility.Services;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Services;
using UnityEditor;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// What a launch does about the first hero: it continues the last hero but never writes the fallback "Hero",
    /// so a first launch finds no saves and the menu asks for a name. Only a Start Fresh Play creates one.
    /// </summary>
    [TestFixture]
    public sealed class BuiltPlayerBootTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private InMemorySaveStore store;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Reset();
            GameLoop.Uninstall();
            GameLoop.Reset();
            config = TestGameConfig.Create(created);
            store = new InMemorySaveStore();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Reset();
            GameLoop.Uninstall();
            GameEntryGuard.Reset();

            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        [Test]
        public void ABuiltPlayerBootAgainstAnEmptyStore_WritesNoHero_AndTheSessionKeepsTheTemplateHero()
        {
            GameBoot.Boot(config, store, builtPlayer: true);

            Assert.That(store.Keys(), Is.Empty, "no fallback \"Hero\" file");
            Assert.That(ServiceLocator.Get<IHeroSaveService>().ActiveHeroId, Is.Null, "the Session hero is not from a file");
            Assert.That(ServiceLocator.Get<ISession>().Hero.Level, Is.EqualTo(config.DefaultHero.Level));
        }

        [Test]
        public void TheEditorBoot_CreatesNoHero_ItsHeroesComeFromTheToolsMenu()
        {
            GameBoot.Boot(config, store);

            Assert.That(store.Keys(), Is.Empty, "no fallback \"Hero\" file");
            Assert.That(ServiceLocator.Get<IHeroSaveService>().ActiveHeroId, Is.Null);
        }

        [Test]
        public void ABootThatCreatesTheFirstHero_WritesOne_AsAStartFreshPlayDoes()
        {
            GameBoot.Boot(config, store, createFirstHero: true);

            Assert.That(store.Keys().Count(key => key != "account"), Is.EqualTo(1), "one hero file");
            Assert.That(ServiceLocator.Get<IHeroSaveService>().ActiveHeroId, Is.Not.Null);
        }

        [Test]
        public void ABuiltPlayerBootAgainstSaves_ContinuesTheLastSelectedHero()
        {
            var seeding = TestGame.Create(config).SavesOver(config, store);
            _ = seeding.Create("Aria");
            var chosen = seeding.Create("Bram").Id;
            _ = seeding.Create("Cira");
            seeding.SetLastSelected(chosen);
            var keysBefore = store.Keys().Count;

            GameBoot.Boot(config, store, builtPlayer: true);

            Assert.That(ServiceLocator.Get<IHeroSaveService>().ActiveHeroId, Is.EqualTo(chosen));
            Assert.That(store.Keys().Count, Is.EqualTo(keysBefore), "nothing was created");
        }

        [Test]
        public void LoadLast_CreatesNothing_AndSaysNothingWasFound()
        {
            var saves = TestGame.Create(config).SavesOver(config, store);

            var result = saves.LoadLast();

            Assert.That(result.Entered, Is.False);
            Assert.That(result.Status, Is.EqualTo(LoadStatus.Missing));
            Assert.That(store.Keys(), Is.Empty);
        }

        [Test]
        public void TheGameScene_SendsABuiltPlayerWithAnUnsavedHeroToTheMenu_AndNoOneElse()
        {
            var saves = TestGame.Create(config).SavesOver(config, store);

            Assert.That(GameEntryGuard.Redirect("Example", builtPlayer: true, saves), Is.EqualTo("MainMenu"));
            Assert.That(GameEntryGuard.Redirect("Example", builtPlayer: false, saves), Is.Null, "the Editor's shortcut");
            Assert.That(GameEntryGuard.Redirect("MainMenu", builtPlayer: true, saves), Is.Null);

            _ = saves.Load(saves.Create("Aria").Id);

            Assert.That(GameEntryGuard.Redirect("Example", builtPlayer: true, saves), Is.Null, "a hero from a save plays");
        }

        [Test]
        public void TheBuildOpensOnTheMainMenu_BeforeTheGameScene()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

            Assert.That(scenes[0], Is.EqualTo($"Assets/Scenes/{GameEntryGuard.MenuScene}.unity"));
            Assert.That(scenes, Does.Contain($"Assets/Scenes/{GameEntryGuard.GameScene}.unity"));
        }
    }
}
