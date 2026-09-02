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
        // Random.value is [0, 1] inclusive; IRollSource is [0, 1). Nudge the endpoint in so
        // the adapter matches the contract (and the seeded test source's half-open range).
        public float Next() => Mathf.Min(Random.value, 0.99999994f);
    }
}
