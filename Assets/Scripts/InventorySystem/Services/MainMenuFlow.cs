using System;
using System.Globalization;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// What the Main Menu's buttons ask: whether Play is available, what Play does, whether Exit is offered
    /// and the line that tells a browser player their hero is saved. Static and scene-free, so the buttons stay
    /// thin and a test needs no scene. The hero slots own which hero is selected: selecting loads it into the
    /// Session, so the selected hero is <see cref="IHeroSaveService.ActiveHeroId"/>.
    /// </summary>
    public static class MainMenuFlow
    {
        /// <summary>Whether a hero that read is selected. Without one Play has nothing to enter.</summary>
        public static bool CanPlay(IHeroSaveService saves) => saves.ActiveHeroId != null;

        /// <summary>
        /// Loads the selected hero from disk again, then asks for the game scene. The load replaces the Hero
        /// and the World together, so nothing of the previous trip leaks and the Supply is restocked. A hero
        /// that no longer loads keeps the player on the menu.
        /// </summary>
        /// <param name="loadScene">The scene change, so a test supplies its own and the menu its loader.</param>
        /// <returns>Whether the game scene was asked for.</returns>
        public static bool Play(IHeroSaveService saves, Action<string> loadScene)
        {
            if (!CanPlay(saves) || !saves.Load(saves.ActiveHeroId).Entered)
                return false;

            loadScene(GameEntryGuard.GameScene);
            return true;
        }

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
        public static bool ShowsExit(RuntimePlatform platform) => platform != RuntimePlatform.WebGLPlayer;

        /// <summary>
        /// "Saved: Aria, Level 12, 14:32" for the last-selected hero, at its save's time in
        /// <paramref name="zone"/> (the machine's by default); <c>null</c> when there is none.
        /// </summary>
        public static string SavedLine(IHeroSaveService saves, TimeZoneInfo zone = null)
        {
            var last = saves.LastSelectedHeroId;

            foreach (var hero in saves.List())
            {
                if (hero.Id != last)
                    continue;

                var local = TimeZoneInfo.ConvertTimeFromUtc(hero.SavedAtUtc, zone ?? TimeZoneInfo.Local);
                return $"Saved: {hero.Name}, Level {hero.Level}, {local.ToString("HH:mm", CultureInfo.InvariantCulture)}";
            }

            return null;
        }
    }
}
