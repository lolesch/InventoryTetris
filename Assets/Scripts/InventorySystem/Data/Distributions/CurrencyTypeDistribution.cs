using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Currency Type Distribution", menuName = AssetMenus.Distributions + "Currency Type")]
    public sealed class CurrencyTypeDistribution : AbstractProbabilityDistribution<CurrencyType> { }
}
