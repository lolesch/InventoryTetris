using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>
    /// What one affix row of the hovered item does to the hero's stat if the item is equipped: the stat's
    /// total after the swap, minus the total as it stands. The hovered affix and the worn ones it replaces
    /// are mutually exclusive - never summed together. Shown on the hovered item; the worn item's side of
    /// the same swap would be its exact mirror, so it only carries the stats the hovered item lacks
    /// (<see cref="OfLoss"/>) - what the unequip alone would cost.
    /// <para>An equip into a free slot replaces nothing, and a stat the displaced gear lacks has nothing
    /// of its own to replace: either way the difference is the full effect of the incoming affix.</para>
    /// </summary>
    public readonly struct StatComparison
    {
        /// <summary>In the stat's own units, after the hero's other modifiers have had their say; positive when the swap raises the total.</summary>
        public float Delta { get; }

        private StatComparison(float delta) => Delta = delta;

        /// <param name="row">The hovered item's affix being drawn.</param>
        /// <param name="displaced">Every affix on the worn items the equip would push out, together: a
        /// two-hander replaces the weapon and the off-hand, so they come off at once. Empty for a free slot.</param>
        /// <param name="swapDifference">Stat, the modifiers going on, the worn modifiers coming off: the
        /// change in the hero's total.</param>
        public static StatComparison Of(
            CharacterStatModifier row,
            IEnumerable<CharacterStatModifier> displaced,
            Func<StatName, IReadOnlyList<StatModifier>, IReadOnlyList<StatModifier>, float> swapDifference)
        {
            var replaced = new List<StatModifier>();

            foreach (var affix in displaced)
                if (affix.Stat == row.Stat)
                    replaced.Add(affix.Modifier);

            // Rounded to what is displayed, so a difference too small to print can never tint a row.
            return new StatComparison((float)Math.Round(swapDifference(row.Stat, new[] { row.Modifier }, replaced), 3));
        }

        /// <summary>
        /// The other half of the swap, for a row of a worn item that is about to be displaced: what taking it
        /// off does to a stat the hovered item has no say in. A stat the hovered item also carries is its
        /// own row's business - the swap is already measured there - so it gets no difference here.
        /// </summary>
        /// <param name="row">The worn item's affix being drawn.</param>
        /// <param name="hovered">Every affix of the hovered item that would replace it.</param>
        public static StatComparison? OfLoss(
            CharacterStatModifier row,
            IEnumerable<CharacterStatModifier> hovered,
            Func<StatName, IReadOnlyList<StatModifier>, IReadOnlyList<StatModifier>, float> swapDifference)
        {
            foreach (var affix in hovered)
                if (affix.Stat == row.Stat)
                    return null;

            return new StatComparison((float)Math.Round(swapDifference(row.Stat, Array.Empty<StatModifier>(), new[] { row.Modifier }), 3));
        }

        /// <summary>The signed difference.</summary>
        public string Format() =>
            Delta == 0f ? "±0" : $"{Delta:+ #.###;- #.###}";

        /// <summary>Green when the swap raises the stat, red when it lowers it, neutral when it changes nothing.</summary>
        public ComparisonVerdict Verdict =>
            Delta == 0f ? ComparisonVerdict.Neutral
            : Delta < 0f ? ComparisonVerdict.Worse
            : ComparisonVerdict.Better;
    }

    public enum ComparisonVerdict
    {
        Neutral,
        Better,
        Worse,
    }
}
