namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The two enemy shapes every Location fields (ADR-0010 second amendment). Both are
    /// Strike-only and fully parametric off the Location's source level; a Location decides
    /// which one it <em>Packs</em> and trickles the other in singly.
    /// </summary>
    public enum EnemyArchetype
    {
        /// <summary>Bulky, slow, armored, low XP. Its Strike is physical. A Pack of them is a clump for the Cast to catch.</summary>
        Brute,

        /// <summary>Fragile, fast, no armor, high XP. Its Strike is magical. The Strike (lowest-HP targeting) picks it off.</summary>
        Skirmisher,
    }
}
