using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Equipment Category Distribution", menuName = "Inventory System/Probability Distributions/Equipment Category")]
    public sealed class EquipmentCategoryDistribution : AbstractProbabilityDistribution<EquipmentCategory> { }
}
