using UnityEngine;

namespace ToolSmiths.InventorySystem.Geometry
{
    /// <summary>
    /// The hit shake's envelope (issue #180): a short wobble that starts at full amplitude the moment a
    /// hit lands and decays straight to zero. Pure on purpose - no Transform, no clock, no random - so
    /// the view hands in its sim-time delta and a pause (delta 0) freezes the shake with the sim.
    /// <para>
    /// The wobble is two sines of unrelated frequency (a Lissajous), so it reads as a jolt and not as a
    /// slide, and it is a function of the phase alone: the same hit always shakes the same way.
    /// </para>
    /// <para>
    /// A hit <i>restarts</i> the shake (envelope and phase) rather than adding to it, so the several hits
    /// of one coarse frame (up to <c>MaxTicksPerAdvance</c> ticks at x8) shake once and a hit during a
    /// shake never stacks offsets.
    /// </para>
    /// </summary>
    public sealed class HitShake
    {
        // The vertical wobble runs this much faster than the horizontal one.
        private const float VerticalRatio = 1.3f;

        private float _phase;

        /// <summary>1 = just hit, 0 = settled.</summary>
        public float Intensity { get; private set; }

        /// <summary>Whether a shake is in flight, so an idle view can skip its per-frame work.</summary>
        public bool IsActive => Intensity > 0f;

        /// <summary>
        /// Whether a health change is a hit. Takes the event's own arguments: the health event raises
        /// <i>before</i> the new value is written, so the enemy still holds the old one.
        /// </summary>
        public static bool IsHit(float previous, float current) => current < previous;

        /// <summary>Shake from the top, however far the last shake had decayed.</summary>
        public void Hit()
        {
            Intensity = 1f;
            _phase = 0f;
        }

        /// <summary>Decay by <paramref name="simDelta"/> of a shake that lasts <paramref name="duration"/>.</summary>
        /// <param name="simDelta">Sim seconds since the last step; 0 while paused.</param>
        /// <param name="duration">Sim seconds a full shake takes to settle. Zero or less ends it at once.</param>
        public void Advance(float simDelta, float duration)
        {
            if (!IsActive)
                return;

            var delta = Mathf.Max(0f, simDelta);
            _phase += delta;
            Intensity = duration > 0f ? Mathf.Max(0f, Intensity - (delta / duration)) : 0f;

            if (!IsActive)
                _phase = 0f;
        }

        /// <summary>The local offset right now, zero when settled.</summary>
        /// <param name="amplitude">Canvas units of the first kick.</param>
        /// <param name="frequency">Wobbles per sim second.</param>
        public Vector2 Offset(float amplitude, float frequency)
        {
            if (!IsActive)
                return Vector2.zero;

            var turns = Mathf.PI * 2f * frequency * _phase;
            return amplitude * Intensity * new Vector2(Mathf.Cos(turns), Mathf.Sin(turns * VerticalRatio));
        }

        /// <summary>Settled at once: a pooled view must not carry a shake to its next enemy.</summary>
        public void Reset()
        {
            Intensity = 0f;
            _phase = 0f;
        }
    }
}
