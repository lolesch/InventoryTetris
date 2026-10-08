using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Equipment Category Distribution", menuName = AssetMenus.Distributions + "Equipment Category")]
    public sealed class EquipmentCategoryDistribution : AbstractProbabilityDistribution<EquipmentCategory> { }
}
