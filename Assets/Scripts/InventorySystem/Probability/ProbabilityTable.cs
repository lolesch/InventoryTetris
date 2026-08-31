using System;
using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Probability
{
    /// <summary>
    /// The drop-table maths, pure and Unity-free. A weight vector in, a probability
    /// vector out — enum-declaration order, summing to 1, computed once at construction
    /// and cached. <see cref="Sample"/> takes the roll as a parameter (no Random inside),
    /// so every behaviour is a plain unit test.
    ///
    /// The fail bucket is <c>default(T)</c>, identified by value, never by index. Its
    /// probability is the designer's <c>failWeight / (failWeight + successSum)</c> raised
    /// to <c>failExponent</c> (Diablo II's ally scaling) — evaluated here, not baked by an
    /// editor callback, so editor and player build agree.
    /// </summary>
    public sealed class ProbabilityTable<T> where T : Enum
    {
        /// <summary>Enum members, declaration order. Allocated once for the whole closed generic type.</summary>
        public static readonly IReadOnlyList<T> Outcomes =
            Array.AsReadOnly((T[])Enum.GetValues(typeof(T)));

        /// <summary>Index of <c>default(T)</c> in <see cref="Outcomes"/>, or -1 when the enum has no zero member.</summary>
        private static readonly int FailIndex = IndexOfValue(default);

        private readonly float[] _probabilities;                 // parallel to Outcomes
        private readonly IReadOnlyList<float> _probabilitiesView; // non-mutable wrapper, cached

        public ProbabilityTable(IReadOnlyList<float> weights, float failWeight, float failExponent)
        {
            if (weights is null)
                throw new ArgumentNullException(nameof(weights));
            if (weights.Count != Outcomes.Count)
                throw new ArgumentException(
                    $"expected {Outcomes.Count} weights for {typeof(T).Name} in enum order, got {weights.Count}",
                    nameof(weights));

            _probabilities = Compute(weights, failWeight, failExponent);
            _probabilitiesView = Array.AsReadOnly(_probabilities);
        }

        /// <summary>The probability vector, enum-declaration order. Read-only — never the backing array.</summary>
        public IReadOnlyList<float> Probabilities => _probabilitiesView;

        public float ProbabilityOf(T outcome) => _probabilities[IndexOfValue(outcome)];

        /// <summary>
        /// Single-pass CDF walk. <paramref name="roll"/> is expected in [0, 1]. A roll of 0
        /// returns the first non-zero-probability outcome; a roll at or past the final
        /// threshold returns the last non-zero outcome — never a phantom default(T).
        /// </summary>
        public T Sample(float roll) => Sample(_probabilities, roll);

        /// <summary>
        /// Samples an arbitrary probability vector in enum-declaration order — the path the
        /// magic-find cascade takes, so the game has exactly one sampler.
        /// </summary>
        public static T Sample(IReadOnlyList<float> probabilities, float roll)
        {
            if (probabilities is null)
                throw new ArgumentNullException(nameof(probabilities));

            var cumulative = 0f;
            var lastNonZero = -1;

            for (var i = 0; i < probabilities.Count; i++)
            {
                if (probabilities[i] <= 0f)
                    continue;

                lastNonZero = i;
                cumulative += probabilities[i];

                if (roll <= cumulative)
                    return Outcomes[i];
            }

            return Outcomes[lastNonZero >= 0 ? lastNonZero : probabilities.Count - 1];
        }

        private static float[] Compute(IReadOnlyList<float> weights, float failWeight, float failExponent)
        {
            var result = new float[weights.Count];

            var successSum = 0f;
            for (var i = 0; i < weights.Count; i++)
            {
                if (i == FailIndex)
                    continue;
                var w = weights[i] > 0f ? weights[i] : 0f;
                result[i] = w;               // raw success weight; normalized below
                successSum += w;
            }

            if (successSum <= 0f)
            {
                // No success mass — nothing can drop. Fail owns the vector, or the vector
                // is all-zero when the enum has no default member. Either way: no NaN.
                var flat = new float[weights.Count];
                if (FailIndex >= 0)
                    flat[FailIndex] = 1f;
                return flat;
            }

            var f = failWeight > 0f ? failWeight : 0f;
            var pFail = FailIndex >= 0 && f > 0f ? f / (f + successSum) : 0f; // Task 3 adds the exponent
            if (pFail < 0f) pFail = 0f;
            else if (pFail > 1f) pFail = 1f;

            var successScale = (1f - pFail) / successSum;
            for (var i = 0; i < result.Length; i++)
                result[i] *= successScale;   // the fail slot was 0, stays 0

            if (FailIndex >= 0)
                result[FailIndex] = pFail;

            return result;
        }

        private static int IndexOfValue(T outcome)
        {
            for (var i = 0; i < Outcomes.Count; i++)
                if (EqualityComparer<T>.Default.Equals(Outcomes[i], outcome))
                    return i;
            return -1;
        }
    }
}
