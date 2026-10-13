using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// What the Main Menu's text area says about the selected hero: where it is set to go, whether a Corpse
    /// waits and where, its lifetime Kills and the average level of what it wears. Read off the Session's
    /// <see cref="Hero"/> as it stands, never off a second read of the save file. Static and scene-free, so a
    /// test needs no scene and the view stays a thin label.
    /// </summary>
    public static class HeroShowcase
    {
        /// <summary>
        /// The four lines for <paramref name="hero"/>. A value with nothing to say is left blank: the Location
        /// before a first Send, the gear level with nothing worn. <paramref name="locations"/> names the Corpse's
        /// Location, which the Corpse holds only as the profile the simulation runs.
        /// </summary>
        public static string Text(Hero hero, ILocationIndex locations, IItemCatalog catalog)
        {
            var gearLevel = AverageWornItemLevel(hero.Equipment.StoredPackages.Values.Select(package => package.Item), catalog);

            return string.Join("\n",
                Line("Location", hero.SelectedLocation ? hero.SelectedLocation.DisplayName : null),
                Line("Corpse", hero.Corpse.Exists ? CorpseLocation(hero, locations) : "none"),
                Line("Kills", hero.TotalKills.ToString(CultureInfo.InvariantCulture)),
                Line("Gear level", gearLevel?.ToString("0.#", CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// The mean <see cref="ItemInstance.ItemLevel"/> over the worn items only: coins are left out, since a coin
        /// is level 0 and says nothing about gear, and an empty slot is no item at all. <c>null</c> when nothing
        /// counts.
        /// </summary>
        public static float? AverageWornItemLevel(IEnumerable<ItemInstance> worn, IItemCatalog catalog)
        {
            var levels = worn
                .Where(item => catalog.Definition(item.DefinitionId).Category != ItemCategory.Currency)
                .Select(item => item.ItemLevel)
                .ToList();

            return levels.Count == 0 ? null : (float)levels.Average();
        }

        private static string CorpseLocation(Hero hero, ILocationIndex locations) =>
            locations.TryGetId(hero.Corpse.Location, out var id) ? locations.Find(id).DisplayName : string.Empty;

        private static string Line(string label, string value) =>
            string.IsNullOrEmpty(value) ? $"{label}:" : $"{label}: {value}";
    }
}
