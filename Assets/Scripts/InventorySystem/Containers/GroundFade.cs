using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// How opaque a Drop on the ground is drawn, from its age among the drops lying there: the newest is
    /// fully opaque, and each drop landed since fades it by one fixed step, down to a minimum where it stays.
    /// Counted in drops, never in time, so the fade means the same at any sim speed, and a Drop is not
    /// dimmed further by the drops that came before it.
    /// </summary>
    public static class GroundFade
    {
        /// <param name="age">How many drops landed after this one: 0 for the newest.</param>
        /// <param name="step">The alpha each later drop takes off.</param>
        /// <param name="minimum">The alpha a drop stops fading at.</param>
        public static float Alpha(int age, float step, float minimum) => Mathf.Max(minimum, 1f - (age * step));
    }
}
