using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Item Category Distribution", menuName = AssetMenus.Distributions + "Item Category")]
    public sealed class ItemCategoryDistribution : AbstractProbabilityDistribution<ItemCategory> { }
}
