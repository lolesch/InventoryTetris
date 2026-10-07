using UnityEngine;

namespace ToolSmiths.InventorySystem.Geometry
{
    /// <summary>
    /// One enemy's walk-in (issue #177): from outside the ring, in a straight line toward the anchor,
    /// onto its slot - and once it has arrived, it stays there. A value type the view holds and resets
    /// by assigning <c>default</c>; a default walk has not <see cref="Started"/>, so the view hides itself
    /// until it is given a start.
    /// <para>
    /// Pure like <see cref="ArenaLayout"/>: the distance is handed in (speed times the sim's delta), so
    /// the view's time source stays at the call site and a zero step - SimSpeed 0 - freezes the walk.
    /// </para>
    /// </summary>
    public struct ArenaWalk
    {
        /// <summary>Where the walker is, as an offset from the hero anchor in arena-local canvas units.</summary>
        public Vector2 Offset { get; private set; }

        /// <summary>Whether it has reached the ring. Sticks: once true, <see cref="Step"/> only keeps it on its slot.</summary>
        public bool Arrived { get; private set; }

        /// <summary>Whether <see cref="Begin"/> gave this walk a start.</summary>
        public bool Started { get; private set; }

        /// <summary>A walk starting <paramref name="margin"/> canvas units beyond the slot at <paramref name="angle"/>.</summary>
        public static ArenaWalk Begin(float angle, float rx, float ry, float margin) => new()
        {
            Offset = ArenaLayout.SpawnPoint(angle, rx, ry, margin),
            Started = true,
        };

        /// <summary>Walk <paramref name="distance"/> canvas units toward the anchor, stopping on the slot.</summary>
        public void Step(float distance, float angle, float rx, float ry)
        {
            if (Arrived)
            {
                Offset = ArenaLayout.SlotPoint(angle, rx, ry);
                return;
            }

            var step = ArenaLayout.Approach(Offset, distance, rx, ry);
            Offset = step.Position;
            Arrived = step.Arrived;
        }
    }
}
