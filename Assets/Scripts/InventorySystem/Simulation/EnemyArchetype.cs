namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The two enemy shapes every Location fields (ADR-0010 second amendment). Both are
    /// Strike-only and fully parametric off the Location's source level; a Location decides
    /// which one it <em>Packs</em> and trickles the other in singly.
    /// </summary>
    public enum EnemyArchetype
    {
        /// <summary>Bulky, slow, armored, low XP. The Cast (highest-HP targeting) lands on it.</summary>
        Brute,

        /// <summary>Fragile, fast, no armor, high XP. The Strike (lowest-HP targeting) picks it off.</summary>
        Skirmisher,
    }
}
