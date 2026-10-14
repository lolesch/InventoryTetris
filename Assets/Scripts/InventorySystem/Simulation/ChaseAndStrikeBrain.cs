using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The default enemy Brain: close in on the hero and Strike whenever the swing is ready. It asks for the Strike
    /// before the enemy has arrived; the Encounter refuses it until the enemy is in reach.
    /// </summary>
    internal sealed class ChaseAndStrikeBrain : IEnemyBrain
    {
        public static readonly ChaseAndStrikeBrain Instance = new();

        private ChaseAndStrikeBrain() { }

        public void Decide(in EnemyView view, List<Intent> intents)
        {
            intents.Add(Intent.Approach(view.Hero));
            if (view.StrikeReady)
                intents.Add(Intent.Strike(view.Hero));
        }
    }
}
