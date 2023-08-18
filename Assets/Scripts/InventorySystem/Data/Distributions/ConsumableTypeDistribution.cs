using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Consumable Type Distribution", menuName = "Inventory System/Probability Distributions/Consumable Type")]
    public class ConsumableTypeDistribution : AbstractProbabilityDistribution<ConsumableType> { }
}
