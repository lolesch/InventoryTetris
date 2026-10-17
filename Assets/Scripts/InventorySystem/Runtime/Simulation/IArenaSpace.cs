using ToolSmiths.InventorySystem.Geometry;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// How the Field map is drawn, for the one thing outside an Encounter that has to move across it: the Hero
    /// figure's Travel (ADR-0021). Implemented by <see cref="EnemyArena"/>.
    /// </summary>
    public interface IArenaSpace
    {
        /// <summary>Canvas units per arena unit and the depth tilt the arena is drawn with.</summary>
        ArenaProjection Projection { get; }

        /// <summary>Canvas units per second the hero walks, so a walk outside the Encounter keeps the pace of a fight.</summary>
        float HeroCanvasSpeed { get; }

        /// <summary>
        /// The selected Location's anchor on the map: where its Origin is drawn, and so where a Send walks the
        /// hero. Null while no Location is selected.
        /// </summary>
        RectTransform LocationAnchor { get; }
    }
}
