using NUnit.Framework;
using Submodules.Utility.Persistence;
using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// What the Main Menu does about Play, Exit and the saved line, tested without a scene: the rules live in
    /// the Services assembly, the buttons only ask them.
    /// </summary>
    [TestFixture]
    public sealed class MainMenuFlowTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private InMemorySaveStore store;
        private TestGame game;
        private HeroSaveService saves;
        private DateTime now;
        private string requested;

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);
            store = new InMemorySaveStore();
            game = TestGame.Create(config);
            now = new DateTime(2026, 10, 13, 12, 32, 0, DateTimeKind.Utc);
            saves = game.SavesOver(config, store, () => now);
            requested = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private void Enter(string scene) => requested = scene;

        [Test]
        public void Play_IsDisabled_WithNoHeroSelected_AndEntersNothing()
        {
            Assert.That(MainMenuFlow.CanPlay(saves), Is.False);
            Assert.That(MainMenuFlow.Play(saves, Enter), Is.False);
            Assert.That(requested, Is.Null);
        }

        [Test]
        public void Play_IsEnabled_OnceAHeroIsSelected_AndDisabledAgainWhenItIsDeleted()
        {
            var id = saves.Create("Aria").Id;
            _ = saves.Load(id);

            Assert.That(MainMenuFlow.CanPlay(saves), Is.True);

            _ = saves.Delete(id);

            Assert.That(MainMenuFlow.CanPlay(saves), Is.False, "deleting the selected hero clears the selection");
        }

        [Test]
        public void Play_LoadsTheSelectedHeroFromDiskAgain_SoNothingOfThePreviousTripCarriesOver()
        {
            var id = saves.Create("Aria").Id;
            _ = saves.Load(id);
            var heroBefore = game.Session.Hero;
            var worldBefore = game.Session.World;

            Assert.That(MainMenuFlow.Play(saves, Enter), Is.True);

            Assert.That(requested, Is.EqualTo(GameEntryGuard.GameScene));
            Assert.That(game.Session.Hero, Is.Not.SameAs(heroBefore), "a fresh Hero read from the file");
            Assert.That(game.Session.World, Is.Not.SameAs(worldBefore), "a fresh World, restocked");
            Assert.That(saves.ActiveHeroId, Is.EqualTo(id));
        }

        [Test]
        public void Play_StaysOnTheMenu_WhenTheHeroFileIsGone()
        {
            var id = saves.Create("Aria").Id;
            _ = saves.Load(id);
            var heroBefore = game.Session.Hero;

            foreach (var key in store.Keys())
            {
                if (key != "account")
                    _ = store.Delete(key);
            }

            Assert.That(MainMenuFlow.Play(saves, Enter), Is.False);
            Assert.That(requested, Is.Null, "no scene change on a load that did not enter");
            Assert.That(game.Session.Hero, Is.SameAs(heroBefore), "the Session was not touched");
        }

        [Test]
        public void Exit_IsOffered_ExceptInWebGL()
        {
            Assert.That(MainMenuFlow.ShowsExit(RuntimePlatform.WebGLPlayer), Is.False);
            Assert.That(MainMenuFlow.ShowsExit(RuntimePlatform.WindowsPlayer), Is.True);
            Assert.That(MainMenuFlow.ShowsExit(RuntimePlatform.WindowsEditor), Is.True);
        }

        [Test]
        public void TheSavedLine_NamesTheLastSelectedHero_WithTheLocalTimeOfItsSave()
        {
            var id = saves.Create("Aria").Id;
            _ = saves.Load(id);
            var twoHoursEast = TimeZoneInfo.CreateCustomTimeZone("east", TimeSpan.FromHours(2), "east", "east");

            var line = MainMenuFlow.SavedLine(saves, twoHoursEast);

            Assert.That(line, Is.EqualTo($"Saved: Aria, Level {saves.List()[0].Level}, 14:32"));
        }

        [Test]
        public void TheSavedLine_FollowsTheLastSelectedHero_NotTheNewestSave()
        {
            var first = saves.Create("Aria").Id;
            now = now.AddHours(1);
            _ = saves.Create("Bram");
            saves.SetLastSelected(first);

            var line = MainMenuFlow.SavedLine(saves, TimeZoneInfo.Utc);

            Assert.That(line, Does.StartWith("Saved: Aria, "));
            Assert.That(line, Does.EndWith(", 12:32"));
        }

        [Test]
        public void TheSavedLine_IsNull_WithNoLastSelectedHero()
        {
            _ = saves.Create("Aria");

            Assert.That(MainMenuFlow.SavedLine(saves, TimeZoneInfo.Utc), Is.Null);
        }
    }
}
