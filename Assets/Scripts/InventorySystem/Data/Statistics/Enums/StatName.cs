using System.ComponentModel;

namespace ToolSmiths.InventorySystem.Data.Enums
{
    [System.Serializable]
    /// The identifier of stats that can appear on gear.
    public enum StatName
    {
        Experience = -1,

        /// OFFENSIVE
        [Description("Attack Speed")]
        AttackSpeed = 1,
        [Description("Physical Damage")]
        PhysicalDamage = 2,
        //CooldownReduction
        [Description("Magic Damage")]
        MagicalDamage = 3,
        [Description("Armor Penetration")]
        ArmorPenetration = 4,
        [Description("Resistance Penetration")]
        MagicPenetration = 5,

        //LifeSteal
        //CritStrikeChance,         
        //CritStrikeDamage,         
        //AttackRange

        /// DEFENSIVE
        Health = 11,
        [Description("Health Regeneration")]
        HealthRegeneration = 12,
        //HealAndShieldPower,

        Armor = 13,
        [Description("Resistance")]
        MagicResist = 14,

        //Tenacity = 15,           
        //SlowResistance = 16,      

        [Description("Energy Shield")]
        Shield = 17,
        //ShieldRecharge

        /// UTILITY
        [Description("Movement Speed")]
        MovementSpeed = 21,

        [Description("Mana")]
        Resource = 22,
        [Description("Mana Regeneration")]
        ResourceRegeneration = 23,

        [Description("Increased Item Rarity")]
        IncreasedItemRarity = 24,
        [Description("Increased Item Quantity")]
        IncreasedItemQuantity = 25,
        //Sockets,

        // ON HIT PROCS 
        //LifeOnHit,
    }
}