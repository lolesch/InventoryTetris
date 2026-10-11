using System;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// How opaque a Drop on the ground is drawn, from its age among the drops lying there: the newest few
    /// are fully opaque, and each drop landed after those fades it by one fixed step, down to a minimum
    /// where it stays. Counted in drops, never in time, so the fade means the same at any sim speed, and a
    /// Drop is not dimmed further by the drops that came before it.
    /// </summary>
    public static class GroundFade
    {
        /// <param name="age">How many drops landed after this one: 0 for the newest.</param>
        /// <param name="fullOpacityCount">How many of the newest drops stay fully opaque.</param>
        /// <param name="step">The alpha each drop beyond those takes off.</param>
        /// <param name="minimum">The alpha a drop stops fading at.</param>
        public static float Alpha(int age, int fullOpacityCount, float step, float minimum) =>
            Mathf.Max(minimum, 1f - (Mathf.Max(0, age - fullOpacityCount + 1) * step));
    }

    /// <summary>The authored numbers of <see cref="GroundFade"/>, so each view of the ground (the grid's slots, the list's rows) fades by the same rule.</summary>
    [Serializable]
    public sealed class GroundFadeSettings
    {
        [Range(1, 20), Tooltip("How many of the newest drops stay fully opaque.")]
        public int fullOpacityCount = 5;

        [Range(0f, 1f), Tooltip("What each drop beyond the fully opaque ones takes off a drop's alpha.")]
        public float fadeStep = 0.15f;

        [Range(0f, 1f), Tooltip("The alpha an old drop stops fading at.")]
        public float minimumAlpha = 0.25f;

        /// <param name="age">How many drops landed after this one: 0 for the newest.</param>
        public float AlphaOf(int age) => GroundFade.Alpha(age, fullOpacityCount, fadeStep, minimumAlpha);
    }
}
