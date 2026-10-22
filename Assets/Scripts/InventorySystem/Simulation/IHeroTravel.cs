using Submodules.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The walk a Hero figure makes outside an Encounter (ADR-0021): shown, never simulated, so nothing in the
    /// sim reads it. A figure retargets it on a click and advances it every frame.
    /// </summary>
    public interface IHeroTravel
    {
        /// <summary>Where the walk stands.</summary>
        Coordinate Position { get; }

        /// <summary>Whether a target was set whose arrival has not been reported yet.</summary>
        bool IsWalking { get; }

        /// <summary>
        /// Turn toward <paramref name="target"/> from where the walk stands; the one walk it replaces reports no
        /// arrival. A target the walk already stands on reports its arrival on the next advance.
        /// </summary>
        void Retarget(Coordinate target);

        /// <summary>Stand at <paramref name="position"/> with no walk, as when the figure appears at an Exit.</summary>
        void Place(Coordinate position);

        /// <summary>
        /// Walk <paramref name="speed"/> units per second for <paramref name="deltaSeconds"/>, stopping on the
        /// target and never past it. Returns true on the one advance that reaches it. A speed that cannot walk
        /// (zero, negative, NaN) stands still.
        /// </summary>
        bool Advance(float speed, float deltaSeconds);
    }
}
