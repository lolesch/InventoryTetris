using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Equipment Type Distribution", menuName = "Inventory System/Items/Distributions/Equipment Type")]
    public class EquipmentTypeDistribution : AbstractProbabilityDistribution<EquipmentType> { }
}
