using System;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The cost of a Death, as two fractions: the share of the hero's progress toward the next
    /// level that is forfeited, and the share of the currency banked <em>this Run</em> that is
    /// withdrawn from the Wallet. Equipped gear and everything already picked up are untouched
    /// (ADR-0009) — the Corpse hand-off is issue #22, not part of this number.
    ///
    /// The exact percentages are unfrozen balance (spec <i>Out of Scope</i>), so they are
    /// injected into <see cref="RunState"/> rather than baked in. <c>default</c> is
    /// <see cref="None"/> — a Run with no configured penalty simply costs nothing extra.
    /// </summary>
    public readonly struct RunPenalty
    {
        /// <summary>A penalty that takes nothing — the <c>default</c>.</summary>
        public static readonly RunPenalty None = default;

        /// <param name="xpLossFraction">Share of progress-to-next-level lost on Death, in <c>[0, 1]</c>.</param>
        /// <param name="currencyFeeFraction">Share of currency banked this Run withdrawn on Death, in <c>[0, 1]</c>.</param>
        public RunPenalty(float xpLossFraction, float currencyFeeFraction)
        {
            XpLossFraction = RequireFraction(xpLossFraction, nameof(xpLossFraction));
            CurrencyFeeFraction = RequireFraction(currencyFeeFraction, nameof(currencyFeeFraction));
        }

        /// <summary>Share of the hero's progress toward the next level forfeited on Death.</summary>
        public float XpLossFraction { get; }

        /// <summary>Share of the currency banked this Run withdrawn from the Wallet on Death.</summary>
        public float CurrencyFeeFraction { get; }

        /// <summary>
        /// Requires a real fraction in <c>[0, 1]</c>. Written as the negation of the valid range
        /// (rather than <c>value &lt; 0f || value &gt; 1f</c>) so <c>NaN</c> is rejected too —
        /// every comparison against <c>NaN</c> is false, so the inverted form is the only one
        /// that catches it.
        /// </summary>
        private static float RequireFraction(float value, string paramName)
        {
            if (!(value >= 0f && value <= 1f))
                throw new ArgumentOutOfRangeException(paramName, value, "Must be a fraction in [0, 1].");
            return value;
        }
    }
}
