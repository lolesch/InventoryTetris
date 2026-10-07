using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Equipment Type Distribution", menuName = AssetMenus.Distributions + "Equipment Type")]
    public sealed class EquipmentTypeDistribution : AbstractProbabilityDistribution<EquipmentType> { }
}
