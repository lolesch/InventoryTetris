using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The runtime <see cref="IRollSource"/>: <see cref="Random.value"/>. The generator takes
    /// randomness as a dependency so every roll is a deterministic unit test; this is the one
    /// adapter that reaches the real RNG.
    /// </summary>
    internal sealed class UnityRollSource : IRollSource
    {
        public float Next() => Random.value;
    }
}
