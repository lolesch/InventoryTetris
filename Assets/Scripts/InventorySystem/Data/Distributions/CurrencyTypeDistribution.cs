using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Currency Type Distribution", menuName = "Inventory System/Probability Distributions/Currency Type")]
    public class CurrencyTypeDistribution : AbstractProbabilityDistribution<CurrencyType> { }
}
