using System;
using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Probability
{
    /// <summary>
    /// Diablo II's magic-find cascade as a pure vector transform. Quality is checked
    /// rarest-first; each rung carries its own diminishing-returns factor; first hit wins.
    /// Operates on the success mass only, scaled by <c>1 - P(fail)</c>, so <c>P(fail)</c>
    /// is invariant under magic find by construction. Magic find of 0 reproduces the input.
    ///
    /// Effective magic find per rung is Diablo II's <c>eff = mf * F / (mf + F)</c>; a
    /// non-positive factor means linear (no diminishing returns), as Diablo II's Magic
    /// quality. The conditional rung probabilities are derived from the base vector, not
    /// authored, which is what makes magic find of 0 an identity.
    /// </summary>
    public static class MagicFindCascade
    {
        public static float[] Apply(
            IReadOnlyList<float> baseProbabilities,
            int failIndex,
            IReadOnlyList<int> rarityOrder,
            IReadOnlyList<float> diminishingFactors,
            float magicFind)
        {
            if (baseProbabilities is null) throw new ArgumentNullException(nameof(baseProbabilities));
            if (rarityOrder is null) throw new ArgumentNullException(nameof(rarityOrder));
            if (diminishingFactors is null) throw new ArgumentNullException(nameof(diminishingFactors));
            if (rarityOrder.Count != diminishingFactors.Count)
                throw new ArgumentException("rarityOrder and diminishingFactors must be the same length");

            var result = new float[baseProbabilities.Count];

            var pFail = failIndex >= 0 && failIndex < baseProbabilities.Count ? baseProbabilities[failIndex] : 0f;
            var successMass = 1f - pFail;

            if (successMass <= 0f || magicFind <= 0f)
            {
                for (var i = 0; i < baseProbabilities.Count; i++)
                    result[i] = baseProbabilities[i];
                return result;
            }

            var rungs = rarityOrder.Count;
            var boosted = new float[rungs];

            // Base conditional rung probabilities, rarest first:
            //   cond[k] = p[order[k]] / (successMass - sum of p[order[j]] for j < k)
            var remaining = successMass;
            for (var k = 0; k < rungs; k++)
            {
                var p = baseProbabilities[rarityOrder[k]];
                var cond = remaining > 1e-9f ? p / remaining : 0f;

                var factor = diminishingFactors[k];
                var eff = factor > 0f ? magicFind * factor / (magicFind + factor) : magicFind;
                cond *= 1f + eff / 100f;

                boosted[k] = cond < 1f ? cond : 1f; // clamp to 1
                remaining -= p;
            }

            // Re-expand rarest-first into absolute success probabilities, then scale by the
            // success mass. The least-rare rung takes the remainder.
            //   P(order[0]) = b[0];  P(order[k]) = prod(1 - b[j], j < k) * b[k]
            var complement = 1f;
            for (var k = 0; k < rungs; k++)
            {
                var share = k < rungs - 1 ? complement * boosted[k] : complement;
                if (share < 0f) share = 0f;
                result[rarityOrder[k]] = share * successMass;
                complement -= share;
            }

            if (failIndex >= 0 && failIndex < result.Length)
                result[failIndex] = pFail;

            return result;
        }
    }
}
