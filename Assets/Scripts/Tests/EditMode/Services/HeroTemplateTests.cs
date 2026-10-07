using NUnit.Framework;
using Submodules.Utility.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// What a hero is built from: the template's id, name and icon, the roster a hero can be created
    /// from, and the template a save remembers. Over the save service and an in-memory store, like the
    /// rest of the persistence tests.
    /// </summary>
    [TestFixture]
    public sealed class HeroTemplateTests
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
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private HeroSaveService NewService() => TestGame.Create(config).SavesOver(config, store);

        // A second template: the default one, copied, with an id and a name of its own, listed on the config.
        private HeroData AddScout()
        {
            var scout = UnityEngine.Object.Instantiate(config.DefaultHero);
            created.Add(scout);

            var template = new SerializedObject(scout);
            template.FindProperty("id").stringValue = "scout";
            template.FindProperty("displayName").stringValue = "Scout";
            _ = template.ApplyModifiedPropertiesWithoutUndo();

            SetRoster(scout);
            return scout;
        }

        private void SetRoster(params HeroData[] heroes)
        {
            var so = new SerializedObject(config);
            var roster = so.FindProperty("heroes");
            roster.arraySize = heroes.Length;

            for (var i = 0; i < heroes.Length; i++)
                roster.GetArrayElementAtIndex(i).objectReferenceValue = heroes[i];

            _ = so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── the authored template ───────────────────────────────────────────

        [Test]
        public void TheAuthoredDefaultHero_HasAnIdAndAName()
        {
            var authored = GameBoot.Load().DefaultHero;

            Assert.That(authored.Id, Is.Not.Empty, "a save refers to the template by this");
            Assert.That(authored.DisplayName, Is.Not.Empty);
        }

        [Test]
        public void EveryAuthoredHero_HasAUniqueId()
        {
            var ids = GameBoot.Load().Heroes.Select(hero => hero.Id).ToArray();

            Assert.That(ids, Is.All.Not.Empty);
            Assert.That(ids, Is.Unique);
        }

        // ── the roster ──────────────────────────────────────────────────────

        [Test]
        public void TheRoster_OffersTheDefaultHeroFirst_ThenTheListedOnes_EachOnce()
        {
            var scout = AddScout();
            SetRoster(scout, config.DefaultHero, scout, null);

            Assert.That(config.Heroes, Is.EqualTo(new[] { config.DefaultHero, scout }));
        }

        [Test]
        public void FindHero_ReadsAnUnknownOrEmptyIdAsTheDefaultHero()
        {
            var scout = AddScout();

            Assert.That(config.FindHero("scout"), Is.SameAs(scout));
            Assert.That(config.FindHero("gone"), Is.SameAs(config.DefaultHero));
            Assert.That(config.FindHero(string.Empty), Is.SameAs(config.DefaultHero));
            Assert.That(config.FindHero(null), Is.SameAs(config.DefaultHero));
        }

        // ── a created hero ──────────────────────────────────────────────────

        [Test]
        public void ANewHero_IsBuiltFromTheDefaultTemplate_UnlessAnotherIsAsked()
        {
            var saves = NewService();

            var hero = saves.Create("Aria");

            Assert.That(hero.Template, Is.SameAs(config.DefaultHero));
            Assert.That(saves.Load(hero.Id).Entered, Is.True);
        }

        [Test]
        public void ANewHero_IsBuiltFromTheTemplateAskedFor_AndTheListShowsIt()
        {
            var scout = AddScout();
            var saves = NewService();

            var hero = saves.Create("Aria", "scout");

            Assert.That(hero.Template, Is.SameAs(scout));
            Assert.That(saves.List().Single().Template, Is.SameAs(scout));
        }

        [Test]
        public void ATemplateThatDoesNotExist_CannotBeCreatedFrom()
        {
            var saves = NewService();

            _ = Assert.Throws<ArgumentException>(() => saves.Create("Aria", "gone"));
            Assert.That(store.Keys(), Is.Empty, "nothing was written");
        }

        [Test]
        public void TheTemplate_SurvivesASaveAndALoadIntoAnotherGame()
        {
            var scout = AddScout();
            var hero = NewService().Create("Aria", "scout");
            var game = TestGame.Create(config);

            _ = game.SavesOver(config, store).Load(hero.Id);

            Assert.That(game.Hero.Template, Is.SameAs(scout));
        }

        [Test]
        public void ASave_KeepsItsTemplate_WhenTheHeroIsSavedAgain()
        {
            _ = AddScout();
            var game = TestGame.Create(config);
            var saves = game.SavesOver(config, store);
            var hero = saves.Create("Aria", "scout");
            _ = saves.Load(hero.Id);

            Assert.That(saves.Save(), Is.True);

            Assert.That(NewService().List().Single().Template.Id, Is.EqualTo("scout"));
        }

        // ── a save whose template is not there ──────────────────────────────

        [Test]
        public void ASaveWithNoTemplate_ReadsAsTheDefaultHero()
        {
            var hero = NewService().Create("Aria");
            BlankTheTemplate(hero);
            var game = TestGame.Create(config);

            var result = game.SavesOver(config, store).Load(hero.Id);

            Assert.That(result.Entered, Is.True);
            Assert.That(game.Hero.Template, Is.SameAs(config.DefaultHero));
        }

        [Test]
        public void ASaveWhoseTemplateWasRemoved_LoadsAsTheDefaultHero_AndSaysSo()
        {
            _ = AddScout();
            var hero = NewService().Create("Aria", "scout");
            SetRoster();
            var game = TestGame.Create(config);
            LogAssert.Expect(LogType.Warning, new Regex("scout.*no longer authored"));

            var result = game.SavesOver(config, store).Load(hero.Id);

            Assert.That(result.Entered, Is.True);
            Assert.That(game.Hero.Template, Is.SameAs(config.DefaultHero));
        }

        private void BlankTheTemplate(HeroSummary hero)
        {
            var key = store.Keys().Single(candidate => HeroFileKey.IdOf(candidate) == hero.Id);
            var slot = new SaveSlot<HeroDto>(store, new JsonUtilitySerializer(), key, 1);
            var saved = slot.Load().Payload;
            saved.templateId = string.Empty;
            slot.Save(saved);
        }
    }
}
