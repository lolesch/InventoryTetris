using System;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// The plain-object form of an <see cref="ItemInstance"/> - no Unity types, so it can be
    /// written to and read from a save file by whatever <c>ISaveStore</c> is built later
    /// (issue #8 names that seam; no file is written in Phase 1). The shape is frozen by the
    /// foundational-rework spec: <c>{ definitionId, rarity, itemLevel, affixes: [...] }</c>.
    ///
    /// Enums are stored by <em>name</em>, not by numeric value, so a save stays readable and
    /// survives an enum being renumbered. <see cref="ItemInstance.FromDto"/> fails loud on a
    /// name it cannot parse rather than silently defaulting.
    /// </summary>
    [Serializable]
    public sealed class ItemInstanceDto
    {
        public string definitionId;
        public string rarity;
        public int itemLevel;
        public AffixDto[] affixes;
    }

    /// <summary>One rolled or implicit modifier, decomposed to primitives. Part of <see cref="ItemInstanceDto"/>.</summary>
    [Serializable]
    public sealed class AffixDto
    {
        public string stat;
        public float value;
        public string type;
        public int rangeMin;
        public int rangeMax;
    }
}
