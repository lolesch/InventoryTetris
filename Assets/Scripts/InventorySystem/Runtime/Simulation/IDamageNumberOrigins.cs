using Submodules.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Where a damage number rises from (issue #213): the arena knows how the sim's arena is drawn, the numbers
    /// only know the place their hit carries. Implemented by <see cref="EnemyArena"/>.
    /// </summary>
    public interface IDamageNumberOrigins
    {
        /// <summary>
        /// The arena's anchored point where <paramref name="position"/>, an arena position, is drawn. False when
        /// nothing is drawn to rise from (no Encounter, no anchor).
        /// </summary>
        bool TryGetNumberOrigin(Coordinate position, out Vector2 origin);
    }
}
