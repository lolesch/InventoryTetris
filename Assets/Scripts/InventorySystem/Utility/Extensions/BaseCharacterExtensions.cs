using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Utility.Extensions
{
    public static class BaseCharacterExtensions
    {

        public static float CalculateRequiredResource(this BaseCharacter character, DamageType damageType)
        {
            if (!character.SpendResource)
                return 0;

            var resource = GetStatValue(character, StatName.Resource);

            // implementation is just for testing
            var damageTypeMod = damageType switch
            {
                DamageType.PhysicalDamage => .03f,
                DamageType.MagicalDamage => .1f,

                _ => 0f,
            };

            return resource * damageTypeMod;
        }

        public static float CalculateDamageOutput(this BaseCharacter character, DamageType damageType)
        {
            // TODO: if attackSpeed has only percantMods and a base of 0 it will return 0 
            var attackSpeed = GetStatValue(character, StatName.AttackSpeed);
            var damageTypeMod = damageType switch
            {
                DamageType.PhysicalDamage => GetStatValue(character, StatName.PhysicalDamage),
                DamageType.MagicalDamage => GetStatValue(character, StatName.MagicalDamage),

                _ => 0f,
            };

            return damageTypeMod * (1f + attackSpeed * 0.01f); // * (1f + damageTypeMod * 0.01f);
        }

        public static float CalculateReceivingDamage(this BaseCharacter character, DamageType damageType, float incomingDamage)
        {
            if (character.IsInvincible)
                return 0f;

            var damageTypeResist = damageType switch
            {
                DamageType.PhysicalDamage => GetStatValue(character, StatName.Armor),
                DamageType.MagicalDamage => GetStatValue(character, StatName.MagicResist),

                _ => 0f,
            };

            // TODO: Desing defenses
            // Avoidance (block, dodge, barrier absorbtion)
            // Mitigation (resistances, armor)
            // Recovery

            var mitigatedDamage = incomingDamage * (1f - damageTypeResist * 0.01f);

            return mitigatedDamage;
        }

        public static CharacterStat GetStat(this BaseCharacter character, StatName stat)
        {
            // Resources first, matching the old reverse-iterated Union(stats, resources).
            var resources = character.CharacterResources;
            for (var i = resources.Length; i-- > 0;)
                if (resources[i].Stat == stat)
                    return resources[i];

            var stats = character.CharacterStats;
            for (var i = stats.Length; i-- > 0;)
                if (stats[i].Stat == stat)
                    return stats[i];

            return null;
        }
        public static CharacterResource GetResource(this BaseCharacter character, StatName resource)
        {
            for (var i = character.CharacterResources.Length; i-- > 0;)
                if (character.CharacterResources[i].Stat == resource)
                    return character.CharacterResources[i];
            return null;
        }

        public static float GetStatValue(this BaseCharacter character, StatName stat) => GetStat(character, stat).TotalValue;
    }
}