using UnityEngine;

namespace ToolSmiths.InventorySystem.Geometry
{
    /// <summary>
    /// The hit flash's envelope (issue #179): full intensity the moment a hit lands, then a straight
    /// ramp back to zero. Pure on purpose - no Transform, no clock - so the view hands in its sim-time
    /// delta and a pause (delta 0) freezes the flash with the sim.
    /// <para>
    /// A hit <i>sets</i> the intensity rather than adding to it, so the several hits of one coarse
    /// frame (up to <c>MaxTicksPerAdvance</c> ticks at x8) flash once instead of stacking.
    /// </para>
    /// </summary>
    public sealed class HitFlashRamp
    {
        /// <summary>0 = normal, 1 = fully flashed.</summary>
        public float Intensity { get; private set; }

        /// <summary>Whether a flash is in flight, so an idle view can skip its per-frame work.</summary>
        public bool IsActive => Intensity > 0f;

        /// <summary>
        /// Whether a health change is a hit. Takes the event's own arguments: the health event raises
        /// <i>before</i> the new value is written, so the enemy still holds the old one.
        /// </summary>
        public static bool IsHit(float previous, float current) => current < previous;

        /// <summary>Flash from the top, however far the last flash had ramped.</summary>
        public void Hit() => Intensity = 1f;

        /// <summary>Ramp back by <paramref name="simDelta"/> of a flash that lasts <paramref name="duration"/>.</summary>
        /// <param name="simDelta">Sim seconds since the last step; 0 while paused.</param>
        /// <param name="duration">Sim seconds a full flash takes to fade. Zero or less ends it at once.</param>
        public void Advance(float simDelta, float duration)
        {
            if (!IsActive)
                return;

            Intensity = duration > 0f
                ? Mathf.Max(0f, Intensity - (Mathf.Max(0f, simDelta) / duration))
                : 0f;
        }

        /// <summary>Back to normal at once: a pooled view must not carry a flash to its next enemy.</summary>
        public void Reset() => Intensity = 0f;
    }
}
