using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// What decides the hero's moves (ADR-0020). The Encounter asks it once a tick, with the enemies' Brains and
    /// before anyone moves, and carries out what it adds to <c>intents</c> (an empty list is a hero that stands
    /// still). It sees a read-only <see cref="HeroView"/> and gets no rolls.
    /// </summary>
    internal interface IHeroBrain
    {
        void Decide(in HeroView view, List<Intent> intents);

        /// <summary>The enemy it would name if asked now, or null. Changes nothing; it keeps the target ring off a corpse between ticks.</summary>
        Enemy PeekTarget(in HeroView view);
    }
}
