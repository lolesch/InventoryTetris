using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// How opaque a Drop on the floor is drawn, from its age rank among the drops lying there: the newest
    /// is fully opaque, the oldest sits at a minimum, the rest are spaced evenly between. Relative to the
    /// other drops, never to time, so the fade means the same at any sim speed.
    /// </summary>
    public static class GroundFade
    {
        /// <param name="rank">0 for the oldest drop, <c>count - 1</c> for the newest.</param>
        /// <param name="count">How many drops lie on the floor.</param>
        /// <param name="minimum">The oldest drop's alpha.</param>
        public static float Alpha(int rank, int count, float minimum) =>
            count <= 1 ? 1f : Mathf.Lerp(minimum, 1f, rank / (float)(count - 1));
    }
}
