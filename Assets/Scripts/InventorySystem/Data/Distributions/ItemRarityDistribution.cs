using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Item Rarity Distribution", menuName = "Inventory System/Probability Distributions/Item Rarity")]
    public class ItemRarityDistribution : AbstractProbabilityDistribution<ItemRarity>
    {
        [SerializeField, Range(1, 8)] private int exampleTotalPlayerCount = 1;
        [SerializeField, Range(0, 7)] private int exampleAlliedPlayerCount = 7;

        private int AlliesWithinRange() => Application.isPlaying ? 0 : Mathf.FloorToInt(Mathf.Min(exampleTotalPlayerCount - 1f, exampleAlliedPlayerCount)); // TODO: requires real inplementation

        private int RemainingPlayers() => exampleTotalPlayerCount - 1 - AlliesWithinRange();

        protected override int GetFailExponent() =>
            Mathf.FloorToInt(1f                         // 1 for the killing player
            + AlliesWithinRange() * 1f                  // 1 more for each player that is a) partied with the killing player && b) within two screens
            + RemainingPlayers() * 0.5f);               // 0.5 for each remaining player (either unpartied or far away).
                                                        // => rounded down   
    }
}
