using NUnit.Framework;
using Submodules.Utility.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.TestTools;

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

        private bool Enter(string scene)
        {
            requested = scene;
            return true;
        }

        private static bool Refuse(string scene) => false;

        // Counts the reads of each file, to show a lookup of one hero does not read the others.
        private sealed class CountingStore : ISaveStore
        {
            private readonly ISaveStore real;

            public CountingStore(ISaveStore real) => this.real = real;

            public List<string> Reads { get; } = new();

            public bool TryRead(string key, out string content)
            {
                Reads.Add(key);
                return real.TryRead(key, out content);
            }

            public bool TryReadBackup(string key, out string content) => real.TryReadBackup(key, out content);
            public bool Delete(string key) => real.Delete(key);
            public IReadOnlyCollection<string> Keys() => real.Keys();
            public bool SetAside(string key) => real.SetAside(key);
            public void AppendSideFile(string fileName, string text) => real.AppendSideFile(fileName, text);
            public void Write(string key, string content) => real.Write(key, content);
        }

        // Writes fail on demand, over a real in-memory store: a first save the disk refuses.
        private sealed class WriteFailingStore : ISaveStore
        {
            private readonly ISaveStore real;

            public WriteFailingStore(ISaveStore real) => this.real = real;

            public bool FailWrites { get; set; }

            public bool TryRead(string key, out string content) => real.TryRead(key, out content);
            public bool TryReadBackup(string key, out string content) => real.TryReadBackup(key, out content);
            public bool Delete(string key) => real.Delete(key);
            public IReadOnlyCollection<string> Keys() => real.Keys();
            public bool SetAside(string key) => real.SetAside(key);
            public void AppendSideFile(string fileName, string text) => real.AppendSideFile(fileName, text);

            public void Write(string key, string content)
            {
                if (FailWrites)
                    throw new System.IO.IOException("the disk is full");

                real.Write(key, content);
            }
        }

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
        public void Play_ReportsFalse_WhenTheSceneLoaderRefuses_SoTheButtonCanStayLive()
        {
            var id = saves.Create("Aria").Id;
            _ = saves.Load(id);

            Assert.That(MainMenuFlow.Play(saves, Refuse), Is.False);
            Assert.That(MainMenuFlow.CanPlay(saves), Is.True, "nothing about the selection changed");
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

        [Test]
        public void CreateHero_MakesADraft_LoadsAndSelectsIt_UnderTheTrimmedName_AndWritesNothing()
        {
            var check = MainMenuFlow.CreateHero(saves, "  Aria ");

            Assert.That(check.IsValid, Is.True);
            Assert.That(saves.TryGetDraft(out var draft), Is.True);
            Assert.That(draft.Name, Is.EqualTo("Aria"));
            Assert.That(draft.IsDraft, Is.True);
            Assert.That(saves.ActiveHeroId, Is.EqualTo(draft.Id), "loaded");
            Assert.That(saves.LastSelectedHeroId, Is.EqualTo(draft.Id), "selected");
            Assert.That(MainMenuFlow.CanPlay(saves), Is.True);
            Assert.That(saves.List(), Is.Empty, "a draft is no saved hero");
            Assert.That(store.Keys(), Is.Empty, "no hero file and no Account entry before the game starts");
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("this name is far too long to keep")]
        [TestCase("a/b")]
        public void CreateHero_WithARefusedName_CreatesNothing_AndSaysWhy(string name)
        {
            var check = MainMenuFlow.CreateHero(saves, name);

            Assert.That(check.IsValid, Is.False);
            Assert.That(check.Reason, Is.Not.Empty);
            Assert.That(saves.List(), Is.Empty, "no hero before a valid name is confirmed");
            Assert.That(saves.TryGetDraft(out _), Is.False, "and no draft either");
            Assert.That(saves.ActiveHeroId, Is.Null);
        }

        [Test]
        public void CreateHero_AcceptsADuplicateName_AndANewDraftReplacesTheOldOne()
        {
            _ = MainMenuFlow.CreateHero(saves, "Aria");
            var first = saves.ActiveHeroId;

            var check = MainMenuFlow.CreateHero(saves, "Aria");

            Assert.That(check.IsValid, Is.True);
            Assert.That(saves.ActiveHeroId, Is.Not.EqualTo(first), "the new hero is the selected one");
            Assert.That(saves.TryGet(first, out _), Is.False, "the first draft was never written and is gone");
            Assert.That(store.Keys(), Is.Empty);
        }

        [Test]
        public void ADraft_IsTheLastHeroSlot_AfterTheSavedHeroes()
        {
            var saved = saves.Create("Bram");
            now = now.AddMinutes(1);
            _ = MainMenuFlow.CreateHero(saves, "Aria");

            var ordered = HeroSlots.Ordered(saves);

            Assert.That(ordered.Select(hero => hero.Name), Is.EqualTo(new[] { "Bram", "Aria" }));
            Assert.That(ordered[0].Id, Is.EqualTo(saved.Id));
            Assert.That(HeroSlots.At(ordered, 2).Kind, Is.EqualTo(HeroSlotKind.Create), "the create slot follows the draft");
        }

        [Test]
        public void ADraft_StaysTheLastHeroSlot_WhenAHeroIsSavedAfterIt()
        {
            _ = MainMenuFlow.CreateHero(saves, "Aria");
            now = now.AddMinutes(1);
            _ = saves.Create("Bram");

            Assert.That(HeroSlots.Ordered(saves).Select(hero => hero.Name), Is.EqualTo(new[] { "Bram", "Aria" }));
        }

        [Test]
        public void PlayLabel_StartsADraft_AndContinuesASavedHero()
        {
            _ = MainMenuFlow.CreateHero(saves, "Aria");
            Assert.That(MainMenuFlow.PlayLabel(saves), Is.EqualTo("Start Game"));

            var saved = saves.Create("Bram").Id;
            _ = MainMenuFlow.Select(saves, saved);
            Assert.That(MainMenuFlow.PlayLabel(saves), Is.EqualTo("Continue"));
        }

        [Test]
        public void Play_OnADraft_WritesTheHeroFirst_ThenEntersItAsASavedHero()
        {
            _ = MainMenuFlow.CreateHero(saves, "Aria");
            var id = saves.ActiveHeroId;

            Assert.That(MainMenuFlow.Play(saves, Enter), Is.True);

            Assert.That(requested, Is.EqualTo(GameEntryGuard.GameScene));
            Assert.That(saves.List().Select(hero => hero.Id), Is.EqualTo(new[] { id }), "the first save");
            Assert.That(saves.TryGetDraft(out _), Is.False, "no longer a draft");
            Assert.That(saves.ActiveHeroId, Is.EqualTo(id));
            Assert.That(MainMenuFlow.PlayLabel(saves), Is.EqualTo("Continue"));
        }

        [Test]
        public void Play_OnADraft_StaysOnTheMenuAsADraft_WhenTheFirstWriteFails()
        {
            var failing = new WriteFailingStore(store);
            saves = game.SavesOver(config, failing, () => now);
            _ = MainMenuFlow.CreateHero(saves, "Aria");
            failing.FailWrites = true;
            LogAssert.Expect(LogType.Error, new Regex("Could not save hero"));

            Assert.That(MainMenuFlow.Play(saves, Enter), Is.False);

            Assert.That(requested, Is.Null, "no scene change on a hero that could not be saved");
            Assert.That(saves.TryGetDraft(out _), Is.True, "still a draft, to try again");
            Assert.That(store.Keys(), Is.Empty);
        }

        [Test]
        public void ADraft_IsNotWrittenBy_AnySaveButStart()
        {
            _ = MainMenuFlow.CreateHero(saves, "Aria");

            var result = saves.Save();

            Assert.That(result.Failure, Is.EqualTo(SaveFailure.NotFromSave), "the quit save must not make a save of an unstarted hero");
            Assert.That(store.Keys(), Is.Empty);
        }

        [Test]
        public void ADraft_IsNeverRemembered_SoALaunchContinuesTheLastRealHero()
        {
            var saved = saves.Create("Bram").Id;
            _ = saves.Load(saved);
            _ = MainMenuFlow.CreateHero(saves, "Aria");

            Assert.That(MainMenuFlow.SavedLine(saves, TimeZoneInfo.Utc), Is.Null, "nothing of the draft is saved");

            var relaunched = game.SavesOver(config, store);
            Assert.That(relaunched.LastSelectedHeroId, Is.EqualTo(saved));
        }

        [Test]
        public void DeleteSelected_OnADraft_DiscardsIt_AndSelectsTheNewestSavedHero()
        {
            var saved = saves.Create("Bram").Id;
            _ = MainMenuFlow.CreateHero(saves, "Aria");

            Assert.That(MainMenuFlow.DeleteSelected(saves), Is.True);

            Assert.That(saves.TryGetDraft(out _), Is.False);
            Assert.That(saves.ActiveHeroId, Is.EqualTo(saved));
            Assert.That(saves.List().Count, Is.EqualTo(1), "the saved hero stays");
        }

        [Test]
        public void SelectingASavedHero_KeepsTheDraft_ForWhenItIsSelectedAgain()
        {
            var saved = saves.Create("Bram").Id;
            _ = MainMenuFlow.CreateHero(saves, "Aria");
            _ = saves.TryGetDraft(out var draft);

            Assert.That(MainMenuFlow.Select(saves, saved), Is.True);
            Assert.That(saves.TryGetDraft(out _), Is.True);
            Assert.That(MainMenuFlow.Select(saves, draft.Id), Is.True);

            Assert.That(saves.ActiveHeroId, Is.EqualTo(draft.Id));
            Assert.That(saves.Save().Failure, Is.EqualTo(SaveFailure.NotFromSave));
        }

        // ── selecting, including a hero that cannot be read ─────────────────

        private const string DamagedId = "deadbeef";

        private void Damage() => store.Write(DamagedId, "this is not a save");

        [Test]
        public void Select_AReadableHero_EntersItOnce_AndASecondSelectDoesNotLoadItAgain()
        {
            var id = saves.Create("Aria").Id;

            Assert.That(MainMenuFlow.Select(saves, id), Is.True);
            Assert.That(saves.ActiveHeroId, Is.EqualTo(id));
            Assert.That(saves.LastSelectedHeroId, Is.EqualTo(id));

            var heroBefore = game.Session.Hero;

            Assert.That(MainMenuFlow.Select(saves, id), Is.True);
            Assert.That(game.Session.Hero, Is.SameAs(heroBefore), "already the Session's");
        }

        [Test]
        public void Select_AnUnreadableHero_IsSelectedNotEntered_PlayIsOff_AndTheFileIsKept()
        {
            var aria = saves.Create("Aria").Id;
            _ = MainMenuFlow.Select(saves, aria);
            var heroBefore = game.Session.Hero;
            Damage();

            Assert.That(MainMenuFlow.Select(saves, DamagedId), Is.False);

            Assert.That(saves.LastSelectedHeroId, Is.EqualTo(DamagedId));
            Assert.That(game.Session.Hero, Is.SameAs(heroBefore), "the Session was not touched");
            Assert.That(MainMenuFlow.CanPlay(saves), Is.False, "the previous hero is still loaded, but it is not the selection");
            Assert.That(MainMenuFlow.Play(saves, Enter), Is.False);
            Assert.That(requested, Is.Null);
            Assert.That(store.Keys(), Does.Contain(DamagedId), "not set aside, so it can be deleted");
            Assert.That(MainMenuFlow.TrySelected(saves, out var selected), Is.True);
            Assert.That(selected.Id, Is.EqualTo(DamagedId));
            Assert.That(selected.UnreadableReason, Is.EqualTo("File damaged and no backup could be read"));
        }

        [Test]
        public void Select_AnUnreadableHero_IsNotRememberedAsTheLastSelected_SoTheNextLaunchLeavesItAlone()
        {
            var aria = saves.Create("Aria").Id;
            _ = MainMenuFlow.Select(saves, aria);
            Damage();

            _ = MainMenuFlow.Select(saves, DamagedId);

            var relaunched = game.SavesOver(config, store, () => now);
            Assert.That(relaunched.LastSelectedHeroId, Is.EqualTo(aria), "the damaged slot was only looked at");
            Assert.That(relaunched.LoadLast().Entered, Is.True);
            Assert.That(relaunched.ActiveHeroId, Is.EqualTo(aria));
            Assert.That(store.Keys(), Does.Contain(DamagedId), "so it was not set aside, and still has its slot");
        }

        [Test]
        public void Select_AHeroThatReadsButWillNotLoad_IsSelectedWithTheReason_AndNeverEntersTheSession()
        {
            var id = saves.Create("Aria").Id;
            var refusing = game.RefusingSavesOver(config, store, () => now);
            LogAssert.Expect(LogType.Error, new Regex("could not be loaded"));

            Assert.That(MainMenuFlow.Select(refusing, id), Is.False);

            Assert.That(refusing.ActiveHeroId, Is.Null);
            Assert.That(MainMenuFlow.CanPlay(refusing), Is.False);
            Assert.That(MainMenuFlow.TrySelected(refusing, out var selected), Is.True);
            Assert.That(selected.UnreadableReason, Is.EqualTo("This save could not be loaded"));
        }

        [Test]
        public void Select_AHeroWhoseFileIsGone_SelectsNothing()
        {
            Assert.That(MainMenuFlow.Select(saves, "nosuchhero"), Is.False);
            Assert.That(saves.LastSelectedHeroId, Is.Null);
        }

        // ── deleting ────────────────────────────────────────────────────────

        [Test]
        public void LookingUpTheSelectedHero_ReadsItsFileAlone_NotEveryHeroSave()
        {
            var counting = new CountingStore(store);
            var counted = game.SavesOver(config, counting, () => now);
            _ = counted.Create("Aria");
            _ = counted.Create("Bram");
            var selected = counted.Create("Cass").Id;
            _ = MainMenuFlow.Select(counted, selected);
            counting.Reads.Clear();

            Assert.That(MainMenuFlow.TrySelected(counted, out var hero), Is.True);
            Assert.That(MainMenuFlow.SavedLine(counted, TimeZoneInfo.Utc), Does.StartWith("Saved: Cass"));

            Assert.That(hero.Id, Is.EqualTo(selected));
            Assert.That(counting.Reads.Where(key => key != "account" && HeroFileKey.IdOf(key) != selected), Is.Empty,
                "the other heroes' files were not read");
        }

        [Test]
        public void TheDeleteQuestion_NamesTheHero_AndAnUnreadableOneIsCalledWhatItIs()
        {
            var aria = new HeroSummary("a", "Aria", 3u, now, LoadStatus.Loaded);
            var damaged = new HeroSummary("b", string.Empty, 0u, now, LoadStatus.Corrupt);

            Assert.That(MainMenuFlow.DeleteQuestion(aria), Does.Contain("Aria"));
            Assert.That(MainMenuFlow.DeleteQuestion(damaged), Does.Contain("unreadable"));
        }

        [Test]
        public void DeleteSelected_RemovesTheHero_AndSelectsTheNewestOfTheOthers()
        {
            var oldest = saves.Create("Aria").Id;
            now = now.AddHours(1);
            var newer = saves.Create("Bram").Id;
            now = now.AddHours(1);
            var selected = saves.Create("Cass").Id;
            _ = MainMenuFlow.Select(saves, selected);

            Assert.That(MainMenuFlow.DeleteSelected(saves), Is.True);

            Assert.That(saves.List().Select(hero => hero.Id), Is.EquivalentTo(new[] { oldest, newer }));
            Assert.That(saves.ActiveHeroId, Is.EqualTo(newer));
            Assert.That(saves.LastSelectedHeroId, Is.EqualTo(newer));
            Assert.That(MainMenuFlow.CanPlay(saves), Is.True);
        }

        [Test]
        public void DeleteSelected_TheOnlyHero_LeavesNoneSelected_SoNothingCanRecreateIt()
        {
            var id = saves.Create("Aria").Id;
            _ = MainMenuFlow.Select(saves, id);

            Assert.That(MainMenuFlow.DeleteSelected(saves), Is.True);

            Assert.That(saves.List(), Is.Empty);
            Assert.That(saves.ActiveHeroId, Is.Null);
            Assert.That(saves.LastSelectedHeroId, Is.Null);
            Assert.That(MainMenuFlow.CanPlay(saves), Is.False);
            Assert.That(MainMenuFlow.TrySelected(saves, out _), Is.False);
            Assert.That(saves.Save().Wrote, Is.False, "a later save cannot write the deleted hero back");
            Assert.That(store.Keys().Where(key => key != "account"), Is.Empty);
        }

        [Test]
        public void DeleteSelected_AnUnreadableHero_RemovesItsFiles_AndSelectsTheNewestReadableOne()
        {
            var oldest = saves.Create("Aria").Id;
            now = now.AddHours(1);
            var newest = saves.Create("Bram").Id;
            _ = MainMenuFlow.Select(saves, oldest);
            Damage();
            _ = MainMenuFlow.Select(saves, DamagedId);

            Assert.That(MainMenuFlow.DeleteSelected(saves), Is.True);

            Assert.That(store.Keys(), Does.Not.Contain(DamagedId));
            Assert.That(store.TryReadBackup(DamagedId, out _), Is.False);
            Assert.That(saves.ActiveHeroId, Is.EqualTo(newest));
            Assert.That(saves.LastSelectedHeroId, Is.EqualTo(newest));
        }

        [Test]
        public void DeleteSelected_WithNothingSelected_DeletesNothing()
        {
            _ = saves.Create("Aria");

            Assert.That(MainMenuFlow.DeleteSelected(saves), Is.False);

            Assert.That(saves.List().Count, Is.EqualTo(1));
        }
    }
}
