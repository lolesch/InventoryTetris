using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Where a damage number rises from (issue #213): the arena knows how the sim's arena is drawn, the numbers
    /// only know who was hit. Implemented by <see cref="EnemyArena"/>.
    /// </summary>
    public interface IDamageNumberOrigins
    {
        /// <summary>
        /// The arena's anchored point where a number for a hit on <paramref name="target"/> starts: the hero figure for
        /// the hero, the projected arena position for an enemy - also one that has just fallen, since the killing
        /// blow's number still rises where it stood. False when nothing is drawn to rise from (no Encounter, no anchor).
        /// </summary>
        bool TryGetNumberOrigin(ICombatant target, out Vector2 origin);
    }
}
