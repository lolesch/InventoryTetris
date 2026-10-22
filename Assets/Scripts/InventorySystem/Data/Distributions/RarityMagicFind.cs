using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    /// <summary>
    /// Binds <see cref="MagicFindCascade"/> to <see cref="ItemRarity"/>: the fail slot,
    /// the rarest-first order, and Diablo II's per-tier factors (Unique 250, Rare 600,
    /// Magic &amp; Common linear — no diminishing returns). Public so the inspector
    /// preview (Assembly-CSharp-Editor) can call it. Indices resolve by enum value, so
    /// a still-commented tier is simply skipped rather than shifting the others.
    /// </summary>
    public static class RarityMagicFind
    {
        // rarest first; 0f factor == linear
        private static readonly (ItemRarity tier, float factor)[] Ladder =
        {
            (ItemRarity.Unique, 250f),
            (ItemRarity.Rare,   600f),
            (ItemRarity.Magic,    0f),
            (ItemRarity.Common,   0f),
        };

        private static readonly int FailIndex;
        private static readonly int[] RarityOrder;
        private static readonly float[] Factors;

        static RarityMagicFind()
        {
            var values = ProbabilityTable<ItemRarity>.Outcomes;

            int IndexOf(ItemRarity r)
            {
                for (var i = 0; i < values.Count; i++)
                    if (EqualityComparer<ItemRarity>.Default.Equals(values[i], r))
                        return i;
                return -1;
            }

            FailIndex = IndexOf(default); // default(ItemRarity) == NoDrop

            var order = new List<int>();
            var factors = new List<float>();
            foreach (var (tier, factor) in Ladder)
            {
                var idx = IndexOf(tier);
                if (idx < 0) continue; // tier still commented out in ItemRarity.cs
                order.Add(idx);
                factors.Add(factor);
            }

            RarityOrder = order.ToArray();
            Factors = factors.ToArray();
        }

        public static float[] Apply(IReadOnlyList<float> baseProbabilities, float magicFind) =>
            MagicFindCascade.Apply(baseProbabilities, FailIndex, RarityOrder, Factors, magicFind);
    }
}
