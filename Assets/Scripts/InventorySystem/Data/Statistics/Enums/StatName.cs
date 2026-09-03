namespace ToolSmiths.InventorySystem.Data.Enums
{
    [System.Serializable]
    /// The identifier of stats that can appear on gear.
    public enum StatName
    {
        Experience = -1,

        /// OFFENSIVE
        AttackSpeed = 1,
        PhysicalDamage = 2,
        //CooldownReduction
        MagicalDamage = 3,
        ArmorPenetration = 4,
        MagicPenetration = 5,

        //LifeSteal
        //CritStrikeChance,         
        //CritStrikeDamage,         
        //AttackRange

        /// DEFENSIVE
        Health = 11,
        HealthRegeneration = 12,
        //HealAndShieldPower,

        Armor = 13,
        MagicResist = 14,

        //Tenacity = 15,           
        //SlowResistance = 16,      

        Shield = 17,
        //ShieldRecharge

        /// UTILITY
        MovementSpeed = 21,

        Resource = 22,
        ResourceRegeneration = 23,

        IncreasedItemRarity = 24,
        IncreasedItemQuantity = 25,
        //Sockets,

        // ON HIT PROCS 
        //LifeOnHit,
    }
}