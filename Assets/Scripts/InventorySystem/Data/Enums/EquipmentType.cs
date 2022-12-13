using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Enums
{
    [System.Serializable]
    /// The identifier of equipment slots
    public enum EquipmentType
    {
        NONE = 0,

        [Tooltip("ArmamentTypes --> 2-99")]
        ARMAMENTS = 1,
        Belt = 2,
        Boots = 3,
        Bracers = 4,
        Chest = 5,
        Cloak = 6,
        Gloves = 7,
        Helm = 8,
        Pants = 9,
        Shoulders = 10,

        [Tooltip("OneHandedWeapons --> 101-199")]
        ONEHANDEDWEAPONS = 100,
        Sword = 101,

        [Tooltip("TwoHandedWeapons --> 201-299")]
        TWOHANDEDWEAPONS = 200,
        Bow = 201,
        GreatSword = 202,

        [Tooltip("Offhands --> 301-399")]
        OFFHANDS = 300,
        Shield = 301,
        Quiver = 302,

        [Tooltip("Jewelry --> 401-499")]
        JEWELRY = 400,
        Amulet = 401,
        Ring = 402,
    }

    public enum EquipmentCategory
    {
        NONE = 0,

        Armaments = 1,
        Weapons = 2,
        Jewelry = 3,
    }

    public enum WeaponCategory
    {
        NONE = 0,

        Weapon_1H = 100,
        Weapon_2H = 200,
        Offhand = 300,
    }
}