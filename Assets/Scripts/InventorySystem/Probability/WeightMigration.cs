using System;
using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Probability
{
    /// <summary>
    /// Re-homes authored weights when an enum changes. Keys on the enum <em>value</em>,
    /// so adding, removing, reordering, or uncommenting a member keeps every weight that
    /// still has a home; only a genuinely removed member loses its weight.
    /// </summary>
    public static class WeightMigration
    {
        public static float[] Remap<T>(
            IReadOnlyList<T> oldOutcomes,
            IReadOnlyList<float> oldWeights,
            IReadOnlyList<T> newOutcomes) where T : Enum
        {
            if (oldOutcomes is null) throw new ArgumentNullException(nameof(oldOutcomes));
            if (oldWeights is null) throw new ArgumentNullException(nameof(oldWeights));
            if (newOutcomes is null) throw new ArgumentNullException(nameof(newOutcomes));
            if (oldOutcomes.Count != oldWeights.Count)
                throw new ArgumentException("oldOutcomes and oldWeights must be the same length");

            var byValue = new Dictionary<T, float>();
            for (var i = 0; i < oldOutcomes.Count; i++)
                byValue[oldOutcomes[i]] = oldWeights[i]; // last write wins on aliased values

            var result = new float[newOutcomes.Count];
            for (var i = 0; i < newOutcomes.Count; i++)
                result[i] = byValue.TryGetValue(newOutcomes[i], out var w) ? w : 0f;

            return result;
        }
    }
}
