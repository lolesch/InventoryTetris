using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// Binds <see cref="MagicFindCascade"/> to <see cref="ItemRarity"/> for the roll path:
    /// the fail slot (<c>NoDrop</c>), the rarest-first order, and Diablo II's per-tier
    /// saturation factors (Unique 250, Rare 600, Magic and Common linear - no diminishing
    /// returns). See ADR-0004.
    ///
    /// A magic find of 0 returns the authored vector unchanged, so the generator's rarity
    /// roll reproduces the authored table exactly (the shared invariant with the
    /// probability rebuild). Indices resolve by enum value at load, so a still-commented
    /// tier in <c>ItemRarity.cs</c> is skipped rather than shifting the others.
    ///
    /// This mirrors <c>RarityMagicFind</c> in <c>Assembly-CSharp</c>, which the inspector
    /// preview and the not-yet-cut <c>ItemRarityDistribution.Roll(magicFind)</c> still call;
    /// the two copies converge when issue #8 routes <c>ItemProvider</c> through the
    /// generator - the same parallel-copy arrangement <see cref="ItemView"/> has with
    /// <c>AbstractItem</c>.
    /// </summary>
    public static class RarityCascade
    {
        // rarest first; 0f factor == linear (no diminishing returns)
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

        static RarityCascade()
        {
            var outcomes = ProbabilityTable<ItemRarity>.Outcomes;

            int IndexOf(ItemRarity rarity)
            {
                for (var i = 0; i < outcomes.Count; i++)
                    if (EqualityComparer<ItemRarity>.Default.Equals(outcomes[i], rarity))
                        return i;
                return -1;
            }

            FailIndex = IndexOf(default); // default(ItemRarity) == NoDrop

            var order = new List<int>();
            var factors = new List<float>();
            foreach (var (tier, factor) in Ladder)
            {
                var index = IndexOf(tier);
                if (index < 0)
                    continue; // tier still commented out in ItemRarity.cs
                order.Add(index);
                factors.Add(factor);
            }

            RarityOrder = order.ToArray();
            Factors = factors.ToArray();
        }

        /// <summary>
        /// The authored rarity odds with magic find applied. <paramref name="magicFind"/>
        /// of 0 (or less) returns <paramref name="authoredOdds"/> itself; otherwise the
        /// rarest-first cascade over the success mass, with <c>P(NoDrop)</c> preserved.
        /// </summary>
        public static IReadOnlyList<float> Apply(IReadOnlyList<float> authoredOdds, float magicFind) =>
            magicFind <= 0f
                ? authoredOdds
                : MagicFindCascade.Apply(authoredOdds, FailIndex, RarityOrder, Factors, magicFind);
    }
}
