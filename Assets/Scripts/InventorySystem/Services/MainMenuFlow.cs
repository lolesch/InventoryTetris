using System;
using System.Globalization;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// What the Main Menu's buttons ask: whether Play is available, what Play does, whether Exit is offered
    /// and the line that tells a browser player their hero is saved. Static and scene-free, so the buttons stay
    /// thin and a test needs no scene. The hero slots own which hero is selected: selecting loads it into the
    /// Session, and the selection is <see cref="IHeroSaveService.LastSelectedHeroId"/>. For a hero that reads it
    /// is also <see cref="IHeroSaveService.ActiveHeroId"/>; for one that cannot be read it is the id alone, so
    /// the slot can show its reason and be deleted while the Session keeps whatever it held.
    /// </summary>
    public static class MainMenuFlow
    {
        /// <summary>Whether a hero that read is selected. Without one, or with an unreadable one selected, Play has
        /// nothing to enter.</summary>
        public static bool CanPlay(IHeroSaveService saves) =>
            saves.ActiveHeroId != null && saves.ActiveHeroId == saves.LastSelectedHeroId;

        /// <summary>
        /// Selects the hero <paramref name="id"/>. A hero that reads is loaded into the Session; one that cannot be
        /// read (its file is damaged or from a newer build, or it read but would not load) is only selected, never
        /// loaded, so its file stays as it is. Selecting what is selected already changes nothing.
        /// </summary>
        /// <returns>Whether the hero is now the Session's.</returns>
        public static bool Select(IHeroSaveService saves, string id)
        {
            if (!saves.TryGet(id, out var hero))
                return false;

            var unreadable = hero.UnreadableReason != null;

            if (saves.LastSelectedHeroId == id && (unreadable || saves.ActiveHeroId == id))
                return !unreadable;

            if (!unreadable && saves.Load(id).Entered)
                return true;

            // A load that failed has marked the hero unreadable by now; one that was only refused (a Run is in
            // the Field) has not, and selects nothing.
            if (unreadable || saves.TryGet(id, out hero) && hero.UnreadableReason != null)
                saves.SetLastSelected(id);

            return false;
        }

        /// <summary>The selected hero as the list shows it; <c>false</c> with none selected.</summary>
        public static bool TrySelected(IHeroSaveService saves, out HeroSummary hero)
        {
            var selected = saves.LastSelectedHeroId;

            if (selected != null)
                return saves.TryGet(selected, out hero);

            hero = default;
            return false;
        }

        /// <summary>The question the delete confirmation asks: it names the hero, and calls a file with no readable
        /// name what it is.</summary>
        public static string DeleteQuestion(HeroSummary hero) =>
            (string.IsNullOrEmpty(hero.Name) ? "Delete this unreadable save?" : $"Delete {hero.Name}?") + "\nThis cannot be undone.";

        /// <summary>
        /// Deletes the selected hero's file and its backup, then selects the newest hero that reads, or none.
        /// Deleting clears the last-selected and active hero with it, so a later quit cannot write it back.
        /// </summary>
        /// <returns>Whether a hero was selected, and its file removed.</returns>
        public static bool DeleteSelected(IHeroSaveService saves)
        {
            var selected = saves.LastSelectedHeroId;

            if (selected == null)
                return false;

            var deleted = saves.Delete(selected);

            // The list is newest save first.
            foreach (var hero in saves.List())
            {
                if (hero.UnreadableReason == null && saves.Load(hero.Id).Entered)
                    break;
            }

            return deleted;
        }

        /// <summary>
        /// Loads the selected hero from disk again, then asks for the game scene. The load replaces the Hero
        /// and the World together, so nothing of the previous trip leaks and the Supply is restocked. A hero
        /// that no longer loads keeps the player on the menu.
        /// </summary>
        /// <param name="loadScene">The scene change, so a test supplies its own and the menu its loader. It answers
        /// whether the load started; a loader that declines leaves the player on the menu to press Play again.</param>
        /// <returns>Whether the game scene was asked for and the loader took it.</returns>
        public static bool Play(IHeroSaveService saves, Func<string, bool> loadScene) =>
            CanPlay(saves) && saves.Load(saves.ActiveHeroId).Entered && loadScene(GameEntryGuard.GameScene);

        /// <summary>
        /// The create slot's confirm: checks <paramref name="name"/> (<see cref="HeroNameRules"/>) and only
        /// then creates the hero from the default template, loads it and so selects it. A refused name
        /// creates nothing and says why in the result.
        /// </summary>
        public static HeroNameCheck CreateHero(IHeroSaveService saves, string name)
        {
            var check = HeroNameRules.Check(name);

            if (check.IsValid)
                _ = saves.Load(saves.Create(check.Name).Id);

            return check;
        }

        /// <summary>Whether the menu offers Exit: not in a browser, where quitting does nothing.</summary>
        public static bool ShowsExit(RuntimePlatform platform) => GameExit.CanQuit(platform);

        /// <summary>
        /// "Saved: Aria, Level 12, 14:32" for the last-selected hero, at its save's time in
        /// <paramref name="zone"/> (the machine's by default); <c>null</c> when there is none.
        /// </summary>
        public static string SavedLine(IHeroSaveService saves, TimeZoneInfo zone = null)
        {
            if (!TrySelected(saves, out var hero))
                return null;

            var local = TimeZoneInfo.ConvertTimeFromUtc(hero.SavedAtUtc, zone ?? TimeZoneInfo.Local);
            return $"Saved: {hero.Name}, Level {hero.Level}, {local.ToString("HH:mm", CultureInfo.InvariantCulture)}";
        }
    }
}
