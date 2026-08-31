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

        // TODO: real player detection. Until multiplayer exists, the preview fields drive
        // this identically in edit and play mode — the old `Application.isPlaying ? 0`
        // branch made the two disagree (spec defect #3).
        private int AlliesWithinRange() =>
            Mathf.FloorToInt(Mathf.Min(exampleTotalPlayerCount - 1f, exampleAlliedPlayerCount));

        private int RemainingPlayers() => exampleTotalPlayerCount - 1 - AlliesWithinRange();

        protected override float GetFailExponent() =>
            1f                              // 1 for the killing player
            + AlliesWithinRange() * 1f      // 1 more for each partied player within two screens
            + RemainingPlayers() * 0.5f;    // 0.5 for each remaining player (unpartied or far)
    }
}
