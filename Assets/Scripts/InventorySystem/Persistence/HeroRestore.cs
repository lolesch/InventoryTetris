using System;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>
    /// Puts a saved hero onto a freshly built one. The hero already has its template stats and its
    /// empty containers, so what is restored is only what a save holds, in the order that makes it
    /// land: the containers and gear first, so a maximum raised by gear is in place; then the level
    /// and its Experience thresholds; then the current resource values, last, so none is clamped
    /// against a maximum that has not grown yet.
    /// </summary>
    public static class HeroRestore
    {
        /// <summary>
        /// Restores <paramref name="dto"/> onto <paramref name="hero"/>, which must be new and
        /// outfitted. Returns what could not be placed. A selected Location that is no longer authored
        /// leaves none selected. An enum name that does not parse throws, as the item round trip does.
        /// </summary>
        public static RestoreReport Restore(HeroDto dto, Hero hero, ILocationIndex locations)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (hero == null)
                throw new ArgumentNullException(nameof(hero));

            if (locations == null)
                throw new ArgumentNullException(nameof(locations));

            // 1. Containers and gear. Everything that no longer fits goes to the Inventory, after the
            //    saved Inventory has claimed its own cells.
            var containers = new ContainerRestore(hero.Inventory);
            containers.Place(SavedContainer.Equipment, dto.equipment, hero.Equipment);
            containers.Place(SavedContainer.Inventory, dto.inventory, hero.Inventory);
            containers.Place(SavedContainer.Stash, dto.stash, hero.Stash);
            var report = containers.Settle();

            // 2. The Corpse, onto the profile the simulation service runs, so a Send recovers it.
            report = report.Plus(CorpseMapper.Restore(dto.corpse, hero.Corpse, locations, hero.Inventory.Catalog));

            // 3. The level and the Experience thresholds it carries, from the one function the level-up
            //    uses. A template whose level has since passed the saved one keeps its own.
            if (hero.Level < dto.level)
                hero.RestoreLevel(dto.level);

            // 4. Current values, once every maximum is in place.
            hero.GetResource(StatName.Health).RestoreCurrent(dto.health);
            hero.GetResource(StatName.Resource).RestoreCurrent(dto.resource);
            hero.GetResource(StatName.Shield).RestoreCurrent(dto.shield);
            hero.GetResource(StatName.Experience).RestoreCurrent(dto.experience);

            // 5. The sliders and the Location the next Send goes to.
            RestoreBehaviour(dto.behaviour, hero);
            hero.SelectedLocation = locations.Find(dto.selectedLocationId);

            return report;
        }

        private static void RestoreBehaviour(BehaviourDto saved, Hero hero)
        {
            if (saved == null)
                return;

            var behaviour = hero.Behaviour;
            behaviour.SimSpeed = saved.simSpeed;
            behaviour.Engagement = saved.engagement;
            behaviour.RetreatHealthFraction = saved.retreatHealthFraction;
            behaviour.RecallBagFillFraction = saved.recallBagFillFraction;
            behaviour.CastThreshold = saved.castThreshold;

            if (!string.IsNullOrEmpty(saved.lootFilterMinimum))
                behaviour.LootFilterMinimum = (ItemRarity)Enum.Parse(typeof(ItemRarity), saved.lootFilterMinimum);
        }
    }
}
