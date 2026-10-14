using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// What decides one enemy's moves (ADR-0020). The Encounter asks it once a tick, before anyone moves, and carries
    /// out what it adds to <c>intents</c> (an empty list is a fighter that does nothing). It sees a read-only
    /// <see cref="EnemyView"/> and gets no rolls.
    /// </summary>
    internal interface IEnemyBrain
    {
        void Decide(in EnemyView view, List<Intent> intents);
    }
}
