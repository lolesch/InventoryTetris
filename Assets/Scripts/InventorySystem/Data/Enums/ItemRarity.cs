namespace ToolSmiths.InventorySystem.Data.Enums
{
    public enum ItemRarity
    {
        NoDrop = 0,
        //Crafted = -1,    // Orange       => modified stats

        Common = 5,      // Gray         => highest base stats
        //Uncommon = 10,   // White        => something special but lesser base stats
        Magic = 15,      // Blue         => an extra stat but lesser base stats
        Rare = 20,       // Yellow       => an extra stat but lesser base stats

        //Set = 25,        // Green        => unique Set boni

        Unique = 30,     // Gold/Purple  => an extra stat but lesser base stats && a unique Stats
    }
}