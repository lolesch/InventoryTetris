using System;
using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// The item types the hero's loot filter switches on or off one by one - one flag per toggle in the
    /// Sim Settings panel. A one-handed weapon is any 1H weapon (Sword, Bow), a two-handed one any 2H.
    /// </summary>
    [Flags]
    public enum LootKind
    {
        None = 0,

        Amulet = 1 << 0,
        Ring = 1 << 1,
        Cloak = 1 << 2,
        Helmet = 1 << 3,
        Shoulders = 1 << 4,
        Gloves = 1 << 5,
        Chest = 1 << 6,
        Bracers = 1 << 7,
        Belt = 1 << 8,
        Pants = 1 << 9,
        Boots = 1 << 10,
        Weapon1H = 1 << 11,
        Weapon2H = 1 << 12,
        Shield = 1 << 13,
        Quiver = 1 << 14,
        Potion = 1 << 15,
        Book = 1 << 16,
        PortalScroll = 1 << 17,
        Currency = 1 << 18,

        All = (1 << 19) - 1,
    }

    /// <summary>Which <see cref="LootKind"/> an <see cref="ItemDefinition"/> belongs to.</summary>
    public static class LootKinds
    {
        public static LootKind Of(ItemDefinition definition)
        {
            switch (definition.Category)
            {
                case ItemCategory.Currency:
                    return LootKind.Currency;

                case ItemCategory.Consumable:
                    return definition.ConsumableType switch
                    {
                        ConsumableType.Potion => LootKind.Potion,
                        ConsumableType.Book => LootKind.Book,
                        ConsumableType.PortalScroll => LootKind.PortalScroll,
                        _ => LootKind.None,
                    };

                default:
                    return OfEquipment(definition.EquipmentType);
            }
        }

        private static LootKind OfEquipment(EquipmentType type) => type switch
        {
            EquipmentType.Amulet => LootKind.Amulet,
            EquipmentType.Ring => LootKind.Ring,
            EquipmentType.Cloak => LootKind.Cloak,
            EquipmentType.Helm => LootKind.Helmet,
            EquipmentType.Shoulders => LootKind.Shoulders,
            EquipmentType.Gloves => LootKind.Gloves,
            EquipmentType.Chest => LootKind.Chest,
            EquipmentType.Bracers => LootKind.Bracers,
            EquipmentType.Belt => LootKind.Belt,
            EquipmentType.Pants => LootKind.Pants,
            EquipmentType.Boots => LootKind.Boots,
            EquipmentType.Shield => LootKind.Shield,
            EquipmentType.Quiver => LootKind.Quiver,
            >= EquipmentType.ONEHANDEDWEAPONS and < EquipmentType.TWOHANDEDWEAPONS => LootKind.Weapon1H,
            >= EquipmentType.TWOHANDEDWEAPONS and < EquipmentType.OFFHANDS => LootKind.Weapon2H,
            _ => LootKind.None,
        };
    }
}
