namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// What a character must meet to equip or use an item. Fills the empty
    /// <c>#region REQUIREMENTS</c> that <c>AbstractItem</c> left as a stub.
    ///
    /// Only a character level for now - the project has no primary-attribute system yet
    /// (<c>StatName</c> carries no Strength / Dexterity / Intelligence), so an attribute
    /// requirement has nothing to check against. The struct is the seam; attributes are an
    /// additive change to it later.
    /// </summary>
    public readonly struct ItemRequirement
    {
        /// <summary>The minimum character level. Zero (the default) means no requirement.</summary>
        public int Level { get; }

        public ItemRequirement(int level) => Level = level <= 0 ? 0 : level;

        /// <summary>No requirement - equippable by anyone.</summary>
        public static ItemRequirement None => default;

        /// <summary>True when this imposes nothing.</summary>
        public bool IsNone => Level <= 0;
    }
}
