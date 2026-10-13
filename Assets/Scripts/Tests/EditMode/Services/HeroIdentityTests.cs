using NUnit.Framework;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// What a hero is called on a screen: its name, its class and its level, for the hero the player selected and
    /// nothing for a Session that holds a hero nobody chose.
    /// </summary>
    [TestFixture]
    public sealed class HeroIdentityTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private Submodules.Utility.Persistence.InMemorySaveStore store;
        private TestGame game;
        private HeroSaveService saves;

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);
            store = new Submodules.Utility.Persistence.InMemorySaveStore();
            game = TestGame.Create(config);
            saves = game.SavesOver(config, store);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        [Test]
        public void ADraftHero_IsNamedByItsNameClassAndLevel()
        {
            _ = MainMenuFlow.CreateHero(saves, "Aria");

            var identity = HeroIdentity.Of(saves, game.Session.Hero);

            Assert.That(identity.Exists, Is.True);
            Assert.That(identity.Name, Is.EqualTo("Aria"));
            Assert.That(identity.ClassName, Is.EqualTo(config.DefaultHero.DisplayName));
            Assert.That(identity.Level, Is.EqualTo(game.Session.Hero.Level));
        }

        [Test]
        public void ASavedHero_IsNamedByItsFile()
        {
            _ = saves.Load(saves.Create("Bram").Id);

            Assert.That(HeroIdentity.Of(saves, game.Session.Hero).Name, Is.EqualTo("Bram"));
        }

        [Test]
        public void TheLevel_FollowsTheHero_NotTheFile()
        {
            _ = saves.Load(saves.Create("Bram").Id);
            game.Session.Hero.RestoreLevel(game.Session.Hero.Level + 2);

            Assert.That(HeroIdentity.Of(saves, game.Session.Hero).Level, Is.EqualTo(game.Session.Hero.Level));
        }

        [Test]
        public void WithNoHeroSelected_TheSessionsHeroHasNoIdentity()
        {
            Assert.That(HeroIdentity.Of(saves, game.Session.Hero).Exists, Is.False);
        }

        [Test]
        public void WhileAnUnreadableSaveIsSelected_NobodyIsNamed()
        {
            _ = saves.Load(saves.Create("Bram").Id);
            store.Write("deadbeef", "this is not a save");
            _ = MainMenuFlow.Select(saves, "deadbeef");

            Assert.That(HeroIdentity.Of(saves, game.Session.Hero).Exists, Is.False, "the Session still holds Bram, who is not the selected hero");
        }
    }
}
