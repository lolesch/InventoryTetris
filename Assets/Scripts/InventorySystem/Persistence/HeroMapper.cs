using System;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>The hero-to-Dto half of the round trip; <see cref="HeroRestore"/> is the way back.</summary>
    public static class HeroMapper
    {
        /// <summary>
        /// Snapshots <paramref name="hero"/>. The hero holds neither an id nor a name, so the caller,
        /// which knows which save slot it is writing, hands them in.
        /// </summary>
        public static HeroDto ToDto(Hero hero, string id, string name, ILocationIndex locations)
        {
            if (hero == null)
                throw new ArgumentNullException(nameof(hero));

            if (locations == null)
                throw new ArgumentNullException(nameof(locations));

            var behaviour = hero.Behaviour;

            return new HeroDto
            {
                id = id ?? string.Empty,
                name = name ?? string.Empty,
                templateId = hero.Template != null && hero.Template.Id != null ? hero.Template.Id : string.Empty,
                level = hero.Level,

                health = hero.GetResource(StatName.Health).CurrentValue,
                resource = hero.GetResource(StatName.Resource).CurrentValue,
                shield = hero.GetResource(StatName.Shield).CurrentValue,
                experience = hero.GetResource(StatName.Experience).CurrentValue,

                behaviour = new BehaviourDto
                {
                    simSpeed = behaviour.SimSpeed,
                    engagement = behaviour.Engagement,
                    retreatHealthFraction = behaviour.RetreatHealthFraction,
                    recallBagFillFraction = behaviour.RecallBagFillFraction,
                    castThreshold = behaviour.CastThreshold,
                    lootFilterMinimum = behaviour.LootFilterMinimum.ToString(),
                },

                selectedLocationId = hero.SelectedLocation != null ? hero.SelectedLocation.Id : string.Empty,

                corpse = CorpseMapper.ToDto(hero.Corpse, locations),

                equipment = ContainerMapper.ToDto(hero.Equipment),
                inventory = ContainerMapper.ToDto(hero.Inventory),
                stash = ContainerMapper.ToDto(hero.Stash),
            };
        }
    }
}
