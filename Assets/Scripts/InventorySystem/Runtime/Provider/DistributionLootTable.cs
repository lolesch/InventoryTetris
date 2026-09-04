using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Distributions;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The runtime <see cref="LootTable"/>: a pass-through over the two authored
    /// distribution <see cref="AbstractProbabilityDistribution"/>s <c>ItemProvider</c> owns.
    /// <see cref="AbstractProbabilityDistribution.Probabilities"/> already hands back an
    /// enum-order vector summing to 1 - exactly the shape <see cref="LootTable"/> wants - so
    /// there is nothing to compute here.
    ///
    /// One global table today; per-<c>Location</c> tables are the seam this shape unblocks
    /// (foundational-rework spec, Phase 1) and land later.
    /// </summary>
    internal sealed class DistributionLootTable : LootTable
    {
        private readonly AbstractProbabilityDistribution category;
        private readonly AbstractProbabilityDistribution rarity;

        public DistributionLootTable(AbstractProbabilityDistribution category, AbstractProbabilityDistribution rarity)
        {
            this.category = category != null ? category : throw new ArgumentNullException(nameof(category));
            this.rarity = rarity != null ? rarity : throw new ArgumentNullException(nameof(rarity));
        }

        public IReadOnlyList<float> CategoryOdds => category.Probabilities;
        public IReadOnlyList<float> RarityOdds => rarity.Probabilities;
    }
}
