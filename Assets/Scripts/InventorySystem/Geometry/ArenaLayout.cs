using UnityEngine;

namespace ToolSmiths.InventorySystem.Geometry
{
    /// <summary>
    /// What the enemy arena works out for a figure beyond where it stands: which way it faces and how
    /// opaque it is while it fades out. Where it stands is the sim's (spatial-combat spec, ADR-0018),
    /// projected by <see cref="ArenaProjection"/>.
    /// <para>
    /// Pure on purpose: no Transform, no Random, no sim types.
    /// </para>
    /// </summary>
    public static class ArenaLayout
    {
        /// <summary>
        /// The opacity of a dying figure: 1 when it starts to fall, linearly down to 0 after
        /// <paramref name="duration"/> seconds of sim time. With no duration it is already gone.
        /// </summary>
        /// <param name="elapsed">Sim seconds since the death.</param>
        public static float DyingAlpha(float elapsed, float duration) =>
            duration <= 0f ? 0f : Mathf.Clamp01(1f - (elapsed / duration));

        /// <summary>
        /// The sprite's <c>scale.x</c>: +1 when the hero stands to the right of the enemy, -1 to the left. Both
        /// x are arena x, the sim's, so the facing is the enemy's side of the hero and not an artefact of the
        /// projection. Inside the dead zone (<c>|dx| &lt; deadZone</c>) the current sign is kept, so an enemy
        /// standing straight above or below the hero does not flicker. Art is authored facing right.
        /// </summary>
        /// <param name="deadZone">Arena units, the same unit as the two x.</param>
        public static int FacingSign(float enemyX, float heroX, int currentSign, float deadZone)
        {
            var dx = heroX - enemyX;
            if (Mathf.Abs(dx) < deadZone)
                return currentSign;

            return dx > 0f ? 1 : -1;
        }
    }
}
