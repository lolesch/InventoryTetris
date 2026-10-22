using System.Globalization;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Geometry
{
    /// <summary>
    /// The damage number's amount, label and motion (issue #181). Pure on purpose - no Transform, no
    /// clock - so the view hands in its sim-time elapsed and a pause (delta 0) freezes the number with
    /// the sim.
    /// </summary>
    public static class DamageNumberMotion
    {
        /// <summary>
        /// The damage of one health change, from the event's own arguments: the health event raises
        /// <i>before</i> the new value is written, so the enemy still holds the old one. A heal or no
        /// change is 0, because <see cref="DamageAccumulator{TKey}"/> would sum a negative amount too.
        /// </summary>
        public static float Damage(float previous, float current) => Mathf.Max(0f, previous - current);

        /// <summary>The text of a number: at most one decimal, the way the health bar shows its figures.</summary>
        public static string Label(float amount) => amount.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>
        /// How far the number has risen after <paramref name="elapsed"/> of <paramref name="duration"/> sim
        /// seconds: 0 at the start, <paramref name="height"/> at the end, easing out so it leaves fast and settles.
        /// </summary>
        public static float Rise(float elapsed, float duration, float height)
        {
            var remaining = 1f - Progress(elapsed, duration);

            return height * (1f - (remaining * remaining));
        }

        /// <summary>
        /// Opaque for the first <paramref name="holdFraction"/> of the life, then a straight fade to 0 at the end.
        /// </summary>
        public static float Alpha(float elapsed, float duration, float holdFraction)
        {
            var progress = Progress(elapsed, duration);
            var hold = Mathf.Clamp01(holdFraction);

            if (progress <= hold)
                return 1f;

            return Mathf.Clamp01(1f - ((progress - hold) / (1f - hold)));
        }

        /// <summary>Whether the number's life is over. A zero duration is over at once.</summary>
        public static bool IsFinished(float elapsed, float duration) => duration <= 0f || elapsed >= duration;

        private static float Progress(float elapsed, float duration) =>
            duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
    }
}
