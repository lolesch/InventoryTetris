using System.Collections.Generic;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Geometry
{
    /// <summary>
    /// Where the enemy arena puts enemies and which way they face. Positions are offsets from the
    /// hero anchor in arena-local canvas units, on an ellipse of radii (rx, ry). Angles are radians,
    /// measured the way <see cref="Mathf.Cos(float)"/> / <see cref="Mathf.Sin(float)"/> read them.
    /// <para>
    /// Pure on purpose: no Transform, no Random, no sim types. The jitter is handed in, so the view's
    /// <c>UnityEngine.Random</c> stays at the call site and the sim's <c>IRollSource</c> is never
    /// drawn from (that would shift every seeded outcome).
    /// </para>
    /// </summary>
    public static class ArenaLayout
    {
        private const float Tau = 2f * Mathf.PI;

        /// <summary>
        /// The angle for a new enemy: the middle of the widest gap between the living enemies'
        /// angles, plus <paramref name="jitter"/>. A Pack batch placed one after another spreads out,
        /// because each pick sees the ones already placed. Nobody is moved when one falls, so a
        /// slot keeps its angle for the enemy's whole life.
        /// </summary>
        /// <param name="livingAngles">Angles of the enemies standing now, any order.</param>
        /// <param name="jitter">Radians added to the gap's middle; the caller draws it.</param>
        /// <returns>The slot angle, wrapped to [0, 2pi).</returns>
        public static float PickSlotAngle(IReadOnlyList<float> livingAngles, float jitter)
        {
            var sorted = new List<float>(livingAngles.Count);
            foreach (var angle in livingAngles)
                sorted.Add(Wrap(angle));
            sorted.Sort();

            /// no one stands yet: the whole circle is the gap
            var middle = 0f;
            var widest = -1f;
            for (var i = 0; i < sorted.Count; i++)
            {
                var from = sorted[i];
                var to = i + 1 < sorted.Count ? sorted[i + 1] : sorted[0] + Tau;
                var gap = to - from;
                if (gap <= widest)
                    continue;

                widest = gap;
                middle = from + (gap * 0.5f);
            }

            return Wrap(middle + jitter);
        }

        private static float Wrap(float angle)
        {
            var wrapped = angle % Tau;
            return wrapped < 0f ? wrapped + Tau : wrapped;
        }

        /// <summary>Where a walking enemy ends up after one step, and whether that is the ring.</summary>
        public readonly struct ApproachStep
        {
            public readonly Vector2 Position;
            /// <summary>On or inside the ellipse: <c>|(x/rx, y/ry)| &lt;= 1</c>.</summary>
            public readonly bool Arrived;

            public ApproachStep(Vector2 position, bool arrived)
            {
                Position = position;
                Arrived = arrived;
            }
        }

        /// <summary>The point on the ellipse at <paramref name="angle"/>, as an offset from the anchor.</summary>
        public static Vector2 SlotPoint(float angle, float rx, float ry) =>
            new(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);

        /// <summary>
        /// The slot pushed <paramref name="margin"/> canvas units straight out from the anchor. It stays
        /// on the slot's ray, so a straight walk to the anchor stops on the slot itself.
        /// </summary>
        public static Vector2 SpawnPoint(float angle, float rx, float ry, float margin)
        {
            var slot = SlotPoint(angle, rx, ry);
            return slot + (slot.normalized * margin);
        }

        /// <summary>
        /// One walk-in step toward the anchor (the origin), in a straight line. It stops on the ellipse
        /// and never goes past it; from on or inside the ring it does not move.
        /// </summary>
        /// <param name="position">Offset from the anchor.</param>
        /// <param name="distance">Canvas units to walk: speed times the sim's delta.</param>
        public static ApproachStep Approach(Vector2 position, float distance, float rx, float ry)
        {
            var normalised = new Vector2(position.x / rx, position.y / ry).magnitude;
            if (normalised <= 1f)
                return new ApproachStep(position, true);

            /// the ray to the origin meets the ring at position / normalised
            var toRing = position.magnitude * (1f - (1f / normalised));
            if (distance >= toRing)
                return new ApproachStep(position / normalised, true);

            return new ApproachStep(position - (position.normalized * Mathf.Max(0f, distance)), false);
        }

        /// <summary>
        /// The sprite's <c>scale.x</c>: +1 when the anchor is to the right of the enemy, -1 to the left.
        /// Inside the dead zone (<c>|dx| &lt; deadZone</c>) the current sign is kept, so an enemy standing
        /// straight above or below the hero does not flicker. Art is authored facing right.
        /// </summary>
        public static int FacingSign(float enemyX, float anchorX, int currentSign, float deadZone)
        {
            var dx = anchorX - enemyX;
            if (Mathf.Abs(dx) < deadZone)
                return currentSign;

            return dx > 0f ? 1 : -1;
        }
    }
}
