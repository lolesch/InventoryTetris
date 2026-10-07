using NUnit.Framework;
using Submodules.Utility.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// The hero slots' rules, which live in the Services assembly so they are tested without a scene:
    /// creation order, the hero / create / empty split, the next free name, and the
    /// <see cref="IHeroSaveService.HeroesChanged"/> event the slot toggles follow. The service runs over an
    /// in-memory store, like <see cref="HeroSaveServiceTests"/>.
    /// </summary>
    [TestFixture]
    public sealed class HeroSlotTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private InMemorySaveStore store;
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);
            var thornwood = TestLocations.Create(config, created, "thornwood");
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

        private HeroSaveService NewSaves() => TestGame.Create(config).SavesOver(config, store, () => now);

        private void Tick() => now = now.AddMinutes(1);

        private static HeroSummary Summary(string id, long createdAtTicks, LoadStatus status = LoadStatus.Loaded) =>
            new(id, id, 1u, DateTime.UtcNow, status, null, createdAtTicks);

        // ── order ───────────────────────────────────────────────────────────

        [Test]
        public void Ordered_IsCreationOrder_AndASaveDoesNotMoveAHero()
        {
            var saves = NewSaves();
            var first = saves.Create("First");
            Tick();
            var second = saves.Create("Second");
            Tick();

            // Saving the first hero makes it the newest save; its slot must not change.
            _ = saves.Load(first.Id);
            Assert.That(saves.Save(), Is.True);

            Assert.That(saves.List().First().Id, Is.EqualTo(first.Id), "the list is newest save first");
            Assert.That(HeroSlots.Ordered(saves.List()).Select(hero => hero.Id), Is.EqualTo(new[] { first.Id, second.Id }));
        }

        [Test]
        public void Ordered_PutsSavesWithoutAStampFirst_ByIdThenTheStamped()
        {
            var ordered = HeroSlots.Ordered(new[] { Summary("c", 5), Summary("b", 0), Summary("a", 0) });

            Assert.That(ordered.Select(hero => hero.Id), Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void Create_StampsTheClock_AndASaveKeepsIt()
        {
            var saves = NewSaves();
            var hero = saves.Create("Aria");

            Assert.That(hero.CreatedAtTicks, Is.EqualTo(now.Ticks));

            Tick();
            _ = saves.Load(hero.Id);
            Assert.That(saves.Save(), Is.True);

            Assert.That(saves.List().Single().CreatedAtTicks, Is.EqualTo(now.AddMinutes(-1).Ticks));
        }

        // ── slots ───────────────────────────────────────────────────────────

        [Test]
        public void At_IsAHeroPerSavedHero_ThenOneCreateSlot_ThenEmptyOnes()
        {
            var ordered = HeroSlots.Ordered(new[] { Summary("a", 1), Summary("b", 2) });

            Assert.That(HeroSlots.At(ordered, 0).Kind, Is.EqualTo(HeroSlotKind.Hero));
            Assert.That(HeroSlots.At(ordered, 1).Hero.Id, Is.EqualTo("b"));
            Assert.That(HeroSlots.At(ordered, 2).Kind, Is.EqualTo(HeroSlotKind.Create));
            Assert.That(HeroSlots.At(ordered, 3).Kind, Is.EqualTo(HeroSlotKind.Empty));
        }

        [Test]
        public void At_WithNoHeroes_MakesTheFirstSlotTheCreateSlot()
        {
            var none = HeroSlots.Ordered(Array.Empty<HeroSummary>());

            Assert.That(HeroSlots.At(none, 0).Kind, Is.EqualTo(HeroSlotKind.Create));
            Assert.That(HeroSlots.At(none, 1).Kind, Is.EqualTo(HeroSlotKind.Empty));
        }

        [Test]
        public void IsUsable_IsTrueForTheCreateSlotAndAReadableHero_OnlyThose()
        {
            Assert.That(new HeroSlot(HeroSlotKind.Create).IsUsable, Is.True);
            Assert.That(new HeroSlot(HeroSlotKind.Hero, Summary("a", 1)).IsUsable, Is.True);
            Assert.That(new HeroSlot(HeroSlotKind.Hero, Summary("a", 1, LoadStatus.RestoredFromBackup)).IsUsable, Is.True);
            Assert.That(new HeroSlot(HeroSlotKind.Hero, Summary("a", 1, LoadStatus.Corrupt)).IsUsable, Is.False);
            Assert.That(new HeroSlot(HeroSlotKind.Hero, Summary("a", 1, LoadStatus.NewerVersion)).IsUsable, Is.False);
            Assert.That(new HeroSlot(HeroSlotKind.Empty).IsUsable, Is.False);
        }

        // ── creating ────────────────────────────────────────────────────────

        [Test]
        public void NextName_IsTheSmallestFreeNumber_SoADeleteNeverMakesADuplicate()
        {
            var saves = NewSaves();
            var one = saves.Create(saves.NextName());
            _ = saves.Create(saves.NextName());

            Assert.That(saves.List().Select(hero => hero.Name), Is.EquivalentTo(new[] { "Hero 1", "Hero 2" }));
            Assert.That(saves.NextName(), Is.EqualTo("Hero 3"));

            _ = saves.Delete(one.Id);

            Assert.That(saves.NextName(), Is.EqualTo("Hero 1"));
        }

        [Test]
        public void CreateAndLoad_WritesTheHero_AndMakesItTheSessions()
        {
            var saves = NewSaves();

            var result = saves.CreateAndLoad();

            Assert.That(result.Entered, Is.True);
            Assert.That(saves.ActiveHeroId, Is.EqualTo(saves.List().Single().Id));
            Assert.That(saves.List().Single().Name, Is.EqualTo("Hero 1"));
        }

        // ── the event ───────────────────────────────────────────────────────

        [Test]
        public void HeroesChanged_IsRaisedByCreateLoadRenameAndDelete()
        {
            var saves = NewSaves();
            var raised = 0;
            saves.HeroesChanged += () => raised++;

            var hero = saves.Create("Aria");
            Assert.That(raised, Is.EqualTo(1), "create");

            _ = saves.Load(hero.Id);
            Assert.That(raised, Is.EqualTo(2), "load");

            _ = saves.Rename(hero.Id, "Brienne");
            Assert.That(raised, Is.EqualTo(3), "rename");

            _ = saves.Delete(hero.Id);
            Assert.That(raised, Is.EqualTo(4), "delete");
        }

        [Test]
        public void HeroesChanged_IsNotRaisedByALoadThatDoesNotEnter()
        {
            var saves = NewSaves();
            var raised = 0;
            saves.HeroesChanged += () => raised++;

            _ = saves.Load("nobody");

            Assert.That(raised, Is.Zero);
        }

        [Test]
        public void AHandlerThatThrows_IsLogged_AndDoesNotUndoTheChange()
        {
            var saves = NewSaves();
            saves.HeroesChanged += () => throw new InvalidOperationException("a view broke");

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("a view broke"));
            var hero = saves.Create("Aria");

            Assert.That(saves.List().Single().Id, Is.EqualTo(hero.Id));
        }
    }
}
