using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Distributions;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Locations
{
    /// <summary>
    /// The <see cref="LootTable"/> a <see cref="LocationConfig"/> rolls against — a
    /// pass-through over that Location's two authored distribution <see cref="ScriptableObject"/>s,
    /// exactly the shape <c>DistributionLootTable</c> (issue #7) already produces for the global
    /// table. Per-Location categories and rarities are what let a harder Location's table roll a
    /// richer spread than an easy one (issue #25 acceptance).
    ///
    /// Same assembly as <see cref="LocationConfig"/>, so it is internal — the only public seam is
    /// <see cref="LocationConfig.ToProfile"/> exposing it as a <see cref="LootTable"/>.
    /// </summary>
    internal sealed class LocationLootTable : LootTable
    {
        private readonly AbstractProbabilityDistribution category;
        private readonly AbstractProbabilityDistribution rarity;

        public LocationLootTable(AbstractProbabilityDistribution category, AbstractProbabilityDistribution rarity)
        {
            this.category = category != null ? category : throw new ArgumentNullException(nameof(category));
            this.rarity = rarity != null ? rarity : throw new ArgumentNullException(nameof(rarity));
        }

        public IReadOnlyList<float> CategoryOdds => category.Probabilities;

        public IReadOnlyList<float> RarityOdds => rarity.Probabilities;
    }
}