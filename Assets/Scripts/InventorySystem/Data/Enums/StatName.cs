using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Enums
{
    [System.Serializable]
    /// The identifier of all stats in the game.
    public enum StatName
    {
        // DAMAGE
        [InspectorName("Weapon Damage")]
        WeaponDamage,
        [InspectorName("Attack Speed")]
        AttackSpeed,
        //CritChance,
        //CritDamage,
        //DamageType, // foreach damageType
        [InspectorName("Physical Damage")]
        PhysicalDamage,
        [InspectorName("Elemental Damage")]
        ElementalDamage,
        //DamageAgainstElites,
        //SplashDamage,

        // DEFENSE
        [InspectorName("Block Chance")]
        BlockChance,
        [InspectorName("Armor")]
        BonusArmor,
        //ReducedDamageFromElites,
        //ReducedDamageFromRanged,
        //ReducedDamageFromMelee,

        //ReducedLossOfControll,

        //Thorns,

        // RESISTANCE
        //DamageTypeResistance, // foreach damageType
        [InspectorName("Physical Resistance")]
        PhysicalResistance,
        [InspectorName("Elemental Resistance")]
        ElementalResistance,
        //[InspectorName("All Resistance")]
        //ResistanceToAll,

        // HEALING
        [InspectorName("Health")]
        MaxLife,
        //LifePerHit,
        [InspectorName("Health Regen")]
        LifePerSecond,
        //LifePerKill,
        //IncreasedHealing,

        // UTILITY
        [InspectorName("Move Speed")]
        MovementSpeed,
        //ReducedResourceCost,
        //ReducedCooldown,

        //Sockets, ???
        //BonusExperience,
        //GoldFind,
        [InspectorName("Increased Rarity %")]
        IncreasedItemRarity,
        [InspectorName("Increased Quantity %")]
        IncreasedItemQuantity,
        //PickupRadius,
        //ReducedLevelRequirements,

        // ONHIT PROCS 
        //ChanceToBlind, // and many more...
    }
}