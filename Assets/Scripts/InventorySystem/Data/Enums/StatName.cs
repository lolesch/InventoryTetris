using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Enums
{
    [System.Serializable]
    /// The identifier of all stats in the game.
    public enum StatName
    {
        /// D3
        // damage
        //AreaDamagePercent,
        [InspectorName("Attack Speed %")]
        AttackSpeedPercent,
        //CritChancePercent,
        //CritDamagePercent,
        [InspectorName("Damage %")]
        DamagePercent,
        //DamageTypePercent, // foreach damageType
        //MinElementalDamage,
        //MaxElementalDamage,
        [InspectorName("Damage Min")]
        MinDamage,
        [InspectorName("Damage Max")]
        MaxDamage,
        //DamageAgainstElitesPercent,

        //ReducedResourceCostPercent,
        //ReducedCooldownPercent,

        // defense
        [InspectorName("Block Chance %")]
        BlockChancePercent,
        [InspectorName("Armor")]
        BonusArmor,
        //ReducedDamageFromElites,
        //ReducedDamageFromRanged,
        //ReducedDamageFromMelee,

        //ReducedLossOfControll,

        //Thorns,

        // resistance
        [InspectorName("All Resistance")]
        ResistanceToAll,
        //DamageTypeResistance, // foreach damageType

        // healing
        [InspectorName("Health %")]
        LifePercent,
        //LifePerHit,
        [InspectorName("Health Regen")]
        LifePerSecond,
        //LifePerKill,
        //ExtraLifeFromHealing,

        // adventure
        [InspectorName("Move Speed %")]
        MovementSpeedPercent,
        //Sockets,
        //BonusExperiencePercent,
        //BonusExperience,
        //GoldFindPercent,
        [InspectorName("Increased Rarity %")]
        IncreasedItemRarityPercent,
        [InspectorName("Increased Quantity %")]
        IncreasedItemQuantityPercent,
        //PickupRadius,
        //ReducedLevelRequirements,

        // OnHit 
        //ChanceToBlindPercent, // and many more...
    }
}