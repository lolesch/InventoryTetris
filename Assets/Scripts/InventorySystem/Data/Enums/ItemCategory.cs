namespace ToolSmiths.InventorySystem.Data.Enums
{
    public enum ItemCategory
    {
        NONE = 0,
        // LOOT TYPES:
        // Equipment/Skills:                nonStackable packageItems that can be equiped via rightclick
        // Consumables/Materials/Currency:     stackable packageItems that can be consumed via rightclick / get consumed (i.e. ammunition)
        Consumable = 1,
        Equipment = 2,
    }
}