namespace ToolSmiths.InventorySystem.Data.Enums
{
    [System.Serializable]
    /// The identifier of all stats in the game.
    public enum StatName
    {
        // DAMAGE
        WeaponDamage,
        AttackSpeed,
        //CritChance,
        //CritDamage,
        //DamageType, // foreach damageType
        PhysicalDamage,
        ElementalDamage,
        //DamageAgainstElites,
        //SplashDamage,

        // DEFENSE
        BlockChance,
        Armor,
        //ReducedDamageFromElites,
        //ReducedDamageFromRanged,
        //ReducedDamageFromMelee,

        //ReducedLossOfControll,

        //Thorns,

        // RESISTANCE
        //DamageTypeResistance, // foreach damageType
        PhysicalResistance,
        ElementalResistance,
        //[InspectorName("All Resistance")]
        //ResistanceToAll,

        // HEALING
        MaxLife,
        //LifePerHit,
        LifePerSecond,
        //LifePerKill,
        //IncreasedHealing,

        // UTILITY
        MovementSpeed,
        //ReducedResourceCost,
        //ReducedCooldown,

        //Sockets, ???
        //BonusExperience,
        //GoldFind,
        IncreasedItemRarity,
        IncreasedItemQuantity,
        //PickupRadius,
        //ReducedLevelRequirements,

        // ONHIT PROCS 
        //ChanceToBlind, // and many more...
    }
}