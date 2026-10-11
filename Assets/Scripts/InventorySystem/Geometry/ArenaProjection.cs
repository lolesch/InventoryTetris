using Submodules.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Geometry
{
    /// <summary>
    /// How the arena view draws the sim's arena (spatial-combat spec, ADR-0018): an arena position becomes a
    /// canvas offset from the arena's origin. The x axis scales by <see cref="CanvasUnitsPerArenaUnit"/>;
    /// the arena's z axis (away from the viewer, up the canvas) scales by the same amount times
    /// <see cref="Tilt"/>, where 1 draws the arena top-down and less than 1 flattens the depth axis.
    /// <para>
    /// Pure like <see cref="ArenaLayout"/>: no Transform, no sim types. The sim never sees the tilt and every
    /// rule distance stays an arena distance, so a flattened view looks closer vertically than the rules
    /// treat it. An area shape drawn later goes through <see cref="ToCanvas"/> as well.
    /// </para>
    /// </summary>
    public readonly struct ArenaProjection
    {
        /// <summary>Canvas units one arena unit spans along the x axis.</summary>
        public readonly float CanvasUnitsPerArenaUnit;

        /// <summary>The depth axis' scale against the x axis: 1 is top-down, 0 collapses the arena onto a line.</summary>
        public readonly float Tilt;

        public ArenaProjection(float canvasUnitsPerArenaUnit, float tilt)
        {
            CanvasUnitsPerArenaUnit = canvasUnitsPerArenaUnit;
            Tilt = tilt;
        }

        /// <summary>The canvas offset of <paramref name="position"/> from where <paramref name="origin"/> is drawn.</summary>
        public Vector2 ToCanvas(Coordinate position, Coordinate origin)
        {
            var offset = position - origin;
            return new Vector2(offset.x * CanvasUnitsPerArenaUnit, offset.z * CanvasUnitsPerArenaUnit * Tilt);
        }

        /// <summary>
        /// Draw order by depth: negative when <paramref name="a"/> draws before (behind) <paramref name="b"/>.
        /// The farther side of the arena, the higher z, is behind. It reads the arena, not the canvas, so a
        /// tilt of zero still orders figures; equal depth is zero and the caller breaks the tie.
        /// </summary>
        public static int DepthOrder(Coordinate a, Coordinate b) => b.z.CompareTo(a.z);
    }
}
