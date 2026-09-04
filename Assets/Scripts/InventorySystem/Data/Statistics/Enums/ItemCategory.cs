namespace ToolSmiths.InventorySystem.Data.Enums
{
    public enum ItemCategory
    {
        NONE = 0,
        // LOOT TYPES:
        // Equipment/Skills:                 nonStackable packageItems that can be equiped via rightclick
        // Consumables/Materials:               stackable packageItems that can be consumed via rightclick / get consumed (i.e. ammunition)
        // Currency:                            ...
        Consumable = 1,
        Equipment = 2,
        Currency = 3,
    }
}