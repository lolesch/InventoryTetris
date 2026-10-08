using Submodules.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Geometry
{
    /// <summary>
    /// How the arena draws the sim's ground (spatial-combat spec, ADR-0018): a ground position becomes a
    /// canvas offset from the ground's origin. The x axis scales by <see cref="CanvasUnitsPerGroundUnit"/>;
    /// the ground's z axis (away from the viewer, up the canvas) scales by the same amount times
    /// <see cref="Tilt"/>, where 1 draws the ground top-down and less than 1 flattens the depth axis.
    /// <para>
    /// Pure like <see cref="ArenaLayout"/>: no Transform, no sim types. The sim never sees the tilt and every
    /// rule distance stays a ground distance, so a flattened view looks closer vertically than the rules
    /// treat it. An area shape drawn later goes through <see cref="ToCanvas"/> as well.
    /// </para>
    /// </summary>
    public readonly struct ArenaProjection
    {
        /// <summary>Canvas units one ground unit spans along the x axis.</summary>
        public readonly float CanvasUnitsPerGroundUnit;

        /// <summary>The depth axis' scale against the x axis: 1 is top-down, 0 collapses the ground onto a line.</summary>
        public readonly float Tilt;

        public ArenaProjection(float canvasUnitsPerGroundUnit, float tilt)
        {
            CanvasUnitsPerGroundUnit = canvasUnitsPerGroundUnit;
            Tilt = tilt;
        }

        /// <summary>The canvas offset of <paramref name="position"/> from where <paramref name="origin"/> is drawn.</summary>
        public Vector2 ToCanvas(Coordinate position, Coordinate origin)
        {
            var offset = position - origin;
            return new Vector2(offset.x * CanvasUnitsPerGroundUnit, offset.z * CanvasUnitsPerGroundUnit * Tilt);
        }

        /// <summary>
        /// Draw order by depth: negative when <paramref name="a"/> draws before (behind) <paramref name="b"/>.
        /// The farther side of the ground, the higher z, is behind. It reads the ground, not the canvas, so a
        /// tilt of zero still orders figures; equal depth is zero and the caller breaks the tie.
        /// </summary>
        public static int DepthOrder(Coordinate a, Coordinate b) => b.z.CompareTo(a.z);
    }
}
