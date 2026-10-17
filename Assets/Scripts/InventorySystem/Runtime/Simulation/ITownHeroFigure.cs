using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// What the rest of the UI asks of a map's Hero figure (ADR-0021): where it stands, and where to walk it.
    /// Implemented by <see cref="TownHeroFigure"/>. A walk is only shown, so no caller waits on it.
    /// </summary>
    public interface ITownHeroFigure
    {
        /// <summary>Whether the figure is on a walk that has not reached its target yet.</summary>
        bool IsWalking { get; }

        /// <summary>Where the figure stands, in the local space of the rect it sits under.</summary>
        Vector2 Position { get; }

        /// <summary>Stand at the Waypoint with no walk, as on entering the game, a hero load or a Respawn.</summary>
        void PlaceAtWaypoint();

        /// <summary>Stand at the centre of <paramref name="point"/> with no walk, as at the Town Exit after a Return.</summary>
        void Place(RectTransform point);

        /// <summary>Walk to the centre of <paramref name="point"/> at the hero's pace, turning from any walk under way.</summary>
        void TravelTo(RectTransform point);
    }
}
