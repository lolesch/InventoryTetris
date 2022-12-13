namespace ToolSmiths.InventorySystem.Data.Enums
{
    [System.Serializable]
    /// The identifier of all stats in the game.
    public enum StatName
    {
        Health = 0,
        HealthRegen = 1,
        MoveSpeed = 2,
        Damage = 3,
        CurrentHealth = 4,
        AttackSpeed = 5,

        MagicFind = 100,

        /// D3
        // damage
        AreaDamagePercent,
        AttackSpeedPercent,
        CritChancePercent,
        CritDamagePercent,
        DamagePercent,
        DamageTypePercent, // foreach damageType
        MinElementalDamage,
        MaxElementalDamage,
        MinDamage,
        MaxDamage,
        DamageAgainstElitesPercent,

        ReducedResourceCostPercent,
        ReducedCooldownPercent,
        // defense
        BlockChancePercent,
        BonusArmor,
        ReducedDamageFromElites,
        ReducedDamageFromRanged,
        ReducedDamageFromMelee,

        ReducedLossOfControll,

        Thorns,

        // resistance
        ResistanceToAll,
        DamageTypeResistance, // foreach damageType

        // healing
        LifePercent,
        LifePerHit,
        LifePerSecond,
        LifePerKill,
        ExtraLifeFromHealing,

        // adventure
        MovementSpeedPercent,
        Sockets,
        BonusExperiencePercent,
        BonusExperience,
        GoldFindPercent,
        IncreasedItemRarityPercent,
        IncreasedItemQuantityPercent,
        PickupRadius,
        ReducedLevelRequirements,

        // OnHit 
        ChanceToBlindPercent, // and many more...
    }
}